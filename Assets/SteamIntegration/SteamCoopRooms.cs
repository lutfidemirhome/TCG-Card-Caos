#if TCG_STEAMWORKS_NET && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
using System;
using System.Collections.Generic;
using System.Globalization;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class SteamCoopRooms : ICoopRooms, IDisposable
{
    const string ProtocolKey = "tcg_coop_protocol";
    const string Protocol = "2";
    const string HostKey = "tcg_host";
    const string PhaseKey = "tcg_phase";
    const string Waiting = "waiting", Playing = "playing";
    const string LockedKey = "tcg_locked", PrivateKey = "tcg_private", RosterKey = "tcg_admitted";
    enum JoinStep { Idle, Metadata, Password, Verification }

    readonly CSteamID local;
    readonly List<CoopRoomInfo> rooms = new List<CoopRoomInfo>();
    readonly List<string> names = new List<string>();
    readonly List<ulong> peers = new List<ulong>();
    readonly CallResult<LobbyCreated_t> created;
    readonly CallResult<LobbyEnter_t> entered;
    readonly CallResult<LobbyMatchList_t> listed;
    readonly Callback<LobbyChatUpdate_t> chat;
    readonly Callback<LobbyDataUpdate_t> data;
    readonly Callback<GameLobbyJoinRequested_t> invited;
    readonly Callback<SteamServersDisconnected_t> disconnected;
    readonly SteamCoopPresence presence;
    readonly SteamRoomAdmission admission = new SteamRoomAdmission();
    readonly List<ulong> steamMembers = new List<ulong>();
    JoinStep joinStep;
    CSteamID expectedOwner;
    byte[] createKey;
    bool createPrivate;
    float deadline, rosterDeadline;
    public bool IsPrivate { get; private set; }
    public bool IsLocked { get; private set; }
    public bool NeedsPassword => joinStep == JoinStep.Password || joinStep == JoinStep.Verification;
    CSteamID lobby, host, invite, joinTarget;
    bool pendingEntry, abandonEntry, searching, returnToMenu, wasBackground, backgroundChanged;
    bool lastOnline;
    public bool Online => SteamUser.BLoggedOn() && SteamUser.GetSteamID() == local;
    public bool Busy => pendingEntry || searching || joinStep == JoinStep.Metadata || joinStep == JoinStep.Verification
        || (HasLobby && !InRoom);
    bool HasLobby => lobby.IsValid();
    public bool InRoom => HasLobby && (!IsLocked || peers.Contains(local.m_SteamID));
    public bool IsHost => HasLobby && local == host;
    public IReadOnlyList<CoopRoomInfo> PublicRooms => rooms;
    public IReadOnlyList<string> Members => names;
    public string StatusKey { get; private set; }

    public SteamCoopRooms(CSteamID local)
    {
        this.local = local;
        created = CallResult<LobbyCreated_t>.Create(OnCreated);
        entered = CallResult<LobbyEnter_t>.Create(OnEntered);
        listed = CallResult<LobbyMatchList_t>.Create(OnList);
        chat = Callback<LobbyChatUpdate_t>.Create(OnMembersChanged);
        data = Callback<LobbyDataUpdate_t>.Create(OnLobbyData);
        invited = Callback<GameLobbyJoinRequested_t>.Create(e => QueueInvite(e.m_steamIDLobby));
        disconnected = Callback<SteamServersDisconnected_t>.Create(e => OnDisconnect());
        presence = new SteamCoopPresence(admission);
        lastOnline = Online;
        var args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length - 1; i++)
            if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong id)) QueueInvite(new CSteamID(id));
    }

    void QueueInvite(CSteamID id)
    {
        if (id.IsLobby() && id != lobby) invite = id;
    }

    public void Tick()
    {
        bool online = Online;
        if (online != lastOnline)
        {
            lastOnline = online;
            if (!online) OnDisconnect();
            CoopSession.Notify();
        }
        if (returnToMenu && !GameSceneLoader.IsLoading)
        {
            returnToMenu = false;
            if (GameScenes.IsActiveGameScene()) SceneManager.LoadScene(GameScenes.MainMenu);
            return;
        }
        if ((HasLobby || joinStep != JoinStep.Idle) && !CoopSession.InShop && !CoopSession.CanEnter) Leave();
        if (online && invite.IsValid() && CoopSession.CanEnter && !HasLobby && !pendingEntry && joinStep == JoinStep.Idle)
        {
            CSteamID target = invite;
            invite = default;
            CancelSearch();
            CoopSession.ShowLobby(true);
            Join(target.m_SteamID);
        }
        if (online && (admission.CanReceiveRequests || joinStep == JoinStep.Verification))
            admission.Poll(local.m_SteamID, OnPasswordReply);
        if ((joinStep == JoinStep.Metadata || joinStep == JoinStep.Verification) && Time.realtimeSinceStartup > deadline)
        {
            if (joinStep == JoinStep.Verification) OnPasswordReply(false, false);
            else { CancelGate(); SetStatus(LocalizationKeys.CoopFailed); }
        }
        if (HasLobby && !InRoom && Time.realtimeSinceStartup > rosterDeadline) EndRoom(LocalizationKeys.CoopFailed);
        if (InRoom && CoopSession.InShop && online)
            presence.Tick(lobby.m_SteamID);
    }

    bool CanRequest => Online && CoopSession.CanEnter && !HasLobby && joinStep == JoinStep.Idle && !Busy;

    public void Search()
    {
        if (!CanRequest) return;
        rooms.Clear();
        SteamMatchmaking.AddRequestLobbyListStringFilter(ProtocolKey, Protocol, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter(PhaseKey, Waiting, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);
        var call = SteamMatchmaking.RequestLobbyList();
        searching = call != SteamAPICall_t.Invalid;
        if (searching) listed.Set(call);
        SetStatus(searching ? LocalizationKeys.CoopWorking : LocalizationKeys.CoopFailed);
    }

    void OnList(LobbyMatchList_t result, bool failure)
    {
        searching = false;
        rooms.Clear();
        if (!failure)
            for (int i = 0; i < result.m_nLobbiesMatching; i++)
            {
                CSteamID id = SteamMatchmaking.GetLobbyByIndex(i);
                int capacity = SteamMatchmaking.GetLobbyMemberLimit(id);
                int count = SteamMatchmaking.GetNumLobbyMembers(id);
                if (!Compatible(id) || count >= capacity || SteamMatchmaking.GetLobbyData(id, PhaseKey) != Waiting
                    || SteamMatchmaking.GetLobbyData(id, PrivateKey) != "0") continue;
                string name = SteamMatchmaking.GetLobbyData(id, "name");
                rooms.Add(new CoopRoomInfo(id.m_SteamID, name, count, capacity, SteamMatchmaking.GetLobbyData(id, LockedKey) == "1"));
            }
        SetStatus(failure ? LocalizationKeys.CoopFailed : rooms.Count == 0 ? LocalizationKeys.CoopEmpty : null);
    }

    public void Host(bool inviteOnly, string password)
    {
        if (!CanRequest || (password != null && password.Length > CoopSession.MaxPasswordLength)) return;
        createPrivate = inviteOnly;
        var type = inviteOnly ? ELobbyType.k_ELobbyTypePrivate : ELobbyType.k_ELobbyTypePublic;
        var call = SteamMatchmaking.CreateLobby(type, CoopSession.Capacity);
        if (call == SteamAPICall_t.Invalid) { SetStatus(LocalizationKeys.CoopFailed); return; }
        createKey = SteamRoomAdmission.PasswordKey(password);
        pendingEntry = true;
        created.Set(call);
        SetStatus(LocalizationKeys.CoopWorking);
    }

    void OnCreated(LobbyCreated_t result, bool failure)
    {
        var id = new CSteamID(result.m_ulSteamIDLobby);
        if (!AcceptEntry(id, !failure && result.m_eResult == EResult.k_EResultOK)) { ClearCreateKey(); return; }
        bool configured = SteamMatchmaking.SetLobbyData(id, ProtocolKey, Protocol)
            && SteamMatchmaking.SetLobbyData(id, HostKey, local.m_SteamID.ToString(CultureInfo.InvariantCulture))
            && SteamMatchmaking.SetLobbyData(id, "name", SteamFriends.GetPersonaName())
            && SteamMatchmaking.SetLobbyData(id, PrivateKey, createPrivate ? "1" : "0")
            && SteamMatchmaking.SetLobbyData(id, LockedKey, createKey != null ? "1" : "0")
            && SteamMatchmaking.SetLobbyData(id, RosterKey, local.m_SteamID.ToString(CultureInfo.InvariantCulture))
            && SteamMatchmaking.SetLobbyData(id, PhaseKey, Waiting);
        if (!configured) { ClearCreateKey(); SteamMatchmaking.LeaveLobby(id); SetStatus(LocalizationKeys.CoopFailed); return; }
        if (createKey != null)
        {
            admission.Open(id.m_SteamID, local.m_SteamID, createKey);
            createKey = null; // The admission gate now owns and clears this array.
        }
        Enter(id, local);
    }

    public void Join(ulong id)
    {
        if (!CanRequest) return;
        joinTarget = new CSteamID(id);
        if (!joinTarget.IsLobby()) { SetStatus(LocalizationKeys.CoopFailed); return; }
        if (Compatible(joinTarget)) { ReadJoinSettings(); return; }
        joinStep = JoinStep.Metadata;
        deadline = Time.realtimeSinceStartup + 15f;
        if (!SteamMatchmaking.RequestLobbyData(joinTarget)) { CancelGate(); SetStatus(LocalizationKeys.CoopFailed); return; }
        SetStatus(LocalizationKeys.CoopWorking);
    }

    void OnLobbyData(LobbyDataUpdate_t e)
    {
        if (joinStep == JoinStep.Metadata && e.m_ulSteamIDLobby == joinTarget.m_SteamID && e.m_ulSteamIDMember == e.m_ulSteamIDLobby)
        {
            if (e.m_bSuccess != 0) ReadJoinSettings();
            else { CancelGate(); SetStatus(LocalizationKeys.CoopFailed); }
        }
        else if (HasLobby && e.m_ulSteamIDLobby == lobby.m_SteamID) RefreshLobby();
    }

    void ReadJoinSettings()
    {
        string locked = SteamMatchmaking.GetLobbyData(joinTarget, LockedKey);
        if (!Compatible(joinTarget) || (locked != "0" && locked != "1")
            || SteamMatchmaking.GetLobbyData(joinTarget, PhaseKey) != Waiting
            || !ulong.TryParse(SteamMatchmaking.GetLobbyData(joinTarget, HostKey), out ulong owner) || owner == 0)
        {
            CancelGate();
            SetStatus(LocalizationKeys.CoopFailed);
            return;
        }
        expectedOwner = new CSteamID(owner);
        if (locked == "0") { JoinSteamLobby(); return; }
        joinStep = JoinStep.Password;
        SetStatus(null);
    }

    public void SubmitPassword(string password)
    {
        if (!Online || !CoopSession.CanEnter || joinStep != JoinStep.Password || string.IsNullOrEmpty(password)
            || password.Length > CoopSession.MaxPasswordLength) return;
        joinStep = JoinStep.Verification;
        deadline = Time.realtimeSinceStartup + 12f;
        if (!admission.Submit(joinTarget.m_SteamID, local.m_SteamID, expectedOwner.m_SteamID, password))
        {
            OnPasswordReply(false, false);
            return;
        }
        SetStatus(LocalizationKeys.CoopWorking);
    }

    void OnPasswordReply(bool granted, bool incorrect)
    {
        if (joinStep != JoinStep.Verification) return;
        admission.CancelRequest();
        if (granted) { JoinSteamLobby(); return; }
        joinStep = JoinStep.Password;
        SetStatus(incorrect ? LocalizationKeys.CoopWrongPassword : LocalizationKeys.CoopFailed);
    }

    void JoinSteamLobby()
    {
        joinStep = JoinStep.Idle;
        var call = SteamMatchmaking.JoinLobby(joinTarget);
        if (call == SteamAPICall_t.Invalid) { SetStatus(LocalizationKeys.CoopFailed); return; }
        pendingEntry = true;
        entered.Set(call);
        SetStatus(LocalizationKeys.CoopWorking);
    }

    void OnEntered(LobbyEnter_t result, bool failure)
    {
        var id = new CSteamID(result.m_ulSteamIDLobby);
        bool joined = !failure && result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;
        if (!AcceptEntry(id, joined)) return;
        CSteamID owner = SteamMatchmaking.GetLobbyOwner(id);
        string phase = SteamMatchmaking.GetLobbyData(id, PhaseKey);
        string locked = SteamMatchmaking.GetLobbyData(id, LockedKey);
        string privacy = SteamMatchmaking.GetLobbyData(id, PrivateKey);
        if (id != joinTarget || !Compatible(id) || expectedOwner != owner || !HostMatches(id, owner) || (phase != Waiting && phase != Playing)
            || (locked != "0" && locked != "1") || (privacy != "0" && privacy != "1"))
        {
            SteamMatchmaking.LeaveLobby(id);
            SetStatus(LocalizationKeys.CoopFailed);
            return;
        }
        Enter(id, owner);
    }

    bool AcceptEntry(CSteamID id, bool success)
    {
        pendingEntry = false;
        bool abandoned = abandonEntry || !CoopSession.CanEnter || !Online;
        abandonEntry = false;
        if (abandoned && success) SteamMatchmaking.LeaveLobby(id);
        if (success && !abandoned) return true;
        SetStatus(abandoned ? StatusKey : LocalizationKeys.CoopFailed);
        return false;
    }

    static bool Compatible(CSteamID id) => SteamMatchmaking.GetLobbyData(id, ProtocolKey) == Protocol;
    static bool HostMatches(CSteamID id, CSteamID owner) => owner.IsValid()
        && ulong.TryParse(SteamMatchmaking.GetLobbyData(id, HostKey), out ulong original) && original == owner.m_SteamID;

    void Enter(CSteamID id, CSteamID owner)
    {
        invite = default;
        lobby = id;
        host = owner;
        IsPrivate = SteamMatchmaking.GetLobbyData(id, PrivateKey) == "1";
        IsLocked = SteamMatchmaking.GetLobbyData(id, LockedKey) == "1";
        rosterDeadline = Time.realtimeSinceStartup + 12f;
        wasBackground = Application.runInBackground;
        Application.runInBackground = true;
        backgroundChanged = true;
        StatusKey = null;
        SteamNetworkingUtils.InitRelayNetworkAccess();
        CoopSession.ShowLobby(true);
        RefreshLobby();
    }

    void OnMembersChanged(LobbyChatUpdate_t e)
    {
        if (!HasLobby || e.m_ulSteamIDLobby != lobby.m_SteamID) return;
        if (IsHost && IsLocked && (e.m_rgfChatMemberStateChange & ~(uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
            admission.Forget(e.m_ulSteamIDUserChanged);
        RefreshLobby();
    }

    void RefreshLobby()
    {
        if (!HasLobby) return;
        if (SteamMatchmaking.GetLobbyOwner(lobby) != host || !HostMatches(lobby, host))
        {
            EndRoom(LocalizationKeys.CoopHostLeft);
            return;
        }
        steamMembers.Clear();
        int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
        for (int i = 0; i < count; i++)
        {
            var member = SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
            steamMembers.Add(member.m_SteamID);
        }
        if (!steamMembers.Contains(local.m_SteamID) || !steamMembers.Contains(host.m_SteamID))
        {
            EndRoom(LocalizationKeys.CoopHostLeft);
            return;
        }
        peers.Clear();
        names.Clear();
        string roster = SteamMatchmaking.GetLobbyData(lobby, RosterKey);
        string[] approved = IsLocked && !IsHost ? roster.Split(',') : null;
        if (IsLocked && IsHost) admission.AdmitMembers(steamMembers);
        foreach (ulong member in steamMembers)
        {
            if (IsLocked && (IsHost ? !admission.IsAdmitted(member) : Array.IndexOf(approved, member.ToString(CultureInfo.InvariantCulture)) < 0)) continue;
            peers.Add(member);
            names.Add(SteamFriends.GetFriendPersonaName(new CSteamID(member)));
        }
        if (IsLocked && IsHost)
        {
            string updated = string.Join(",", peers);
            if (updated != roster && !SteamMatchmaking.SetLobbyData(lobby, RosterKey, updated))
            {
                EndRoom(LocalizationKeys.CoopFailed);
                return;
            }
        }
        presence.SetMembers(local.m_SteamID, host.m_SteamID, peers);
        if (InRoom && !CoopSession.InShop && SteamMatchmaking.GetLobbyData(lobby, PhaseKey) == Playing)
            CoopSession.BeginShop();
        CoopSession.Notify();
    }

    public void Invite()
    {
        if (!InRoom || !Online) return;
        if (SteamUtils.IsOverlayEnabled()) SteamFriends.ActivateGameOverlayInviteDialog(lobby);
        else SetStatus(LocalizationKeys.CoopOverlay);
    }

    public void StartShop()
    {
        if (!IsHost || !CoopSession.CanEnter || !Online) return;
        if (!SteamMatchmaking.SetLobbyJoinable(lobby, false)) { SetStatus(LocalizationKeys.CoopFailed); return; }
        if (!SteamMatchmaking.SetLobbyData(lobby, PhaseKey, Playing))
        {
            SteamMatchmaking.SetLobbyJoinable(lobby, true);
            SetStatus(LocalizationKeys.CoopFailed);
            return;
        }
        admission.CloseAdmission();
        CoopSession.BeginShop();
    }

    void SetStatus(string key) { StatusKey = key; CoopSession.Notify(); }

    void CancelSearch()
    {
        listed.Cancel();
        searching = false;
    }

    public void Leave() => EndRoom(null);

    void OnDisconnect()
    {
        if (!Busy && !HasLobby && joinStep == JoinStep.Idle) return;
        // The Steam connection owning these requests has gone away. Its call results
        // may never run; the next connection must be free to create or join again.
        created.Cancel();
        entered.Cancel();
        pendingEntry = false;
        abandonEntry = false;
        EndRoom(LocalizationKeys.CoopDisconnected);
    }

    void EndRoom(string reason)
    {
        if (pendingEntry) abandonEntry = true;
        CancelSearch();
        CancelGate();
        ClearCreateKey();
        admission.Reset();
        presence.Clear();
        if (HasLobby)
        {
            if (IsHost && Online) SteamMatchmaking.SetLobbyJoinable(lobby, false);
            SteamMatchmaking.LeaveLobby(lobby);
        }
        lobby = host = default;
        IsLocked = IsPrivate = false;
        rooms.Clear();
        names.Clear();
        peers.Clear();
        if (backgroundChanged) Application.runInBackground = wasBackground;
        backgroundChanged = false;
        if (CoopSession.InShop) returnToMenu = true;
        SetStatus(reason);
    }

    void ClearCreateKey()
    {
        if (createKey != null) Array.Clear(createKey, 0, createKey.Length);
        createKey = null;
    }

    void CancelGate()
    {
        joinStep = JoinStep.Idle;
        admission.CancelRequest();
        expectedOwner = default;
    }

    public void Dispose()
    {
        Leave();
        created.Dispose(); entered.Dispose(); listed.Dispose();
        chat.Dispose(); data.Dispose(); invited.Dispose(); disconnected.Dispose();
        presence.Dispose();
    }
}
#endif
