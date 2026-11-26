#if TCG_STEAMWORKS_NET && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
using System;
using System.Collections.Generic;
using System.Globalization;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class SteamCoopRooms : ICoopRooms, ICoopRoomControls, ICoopRoomInvites, ICoopMemberPortraits, ICoopVoiceSettings, IDisposable
{
    const string ProtocolKey = "tcg_coop_protocol";
    const string Protocol = "8";
    const string HostKey = "tcg_host";
    const string PhaseKey = "tcg_phase";
    const string Waiting = "waiting", Playing = "playing";
    const string LockedKey = "tcg_locked", PrivateKey = "tcg_private", RosterKey = "tcg_admitted";
    enum JoinStep { Idle, Code, Metadata, Password, Verification }

    readonly CSteamID local;
    readonly List<CoopRoomInfo> rooms = new List<CoopRoomInfo>();
    readonly List<string> names = new List<string>();
    readonly List<ulong> peers = new List<ulong>();
    readonly List<ulong> shopPeers = new List<ulong>();
    readonly HashSet<ulong> waitingGuests = new HashSet<ulong>();
    readonly CallResult<LobbyCreated_t> created;
    readonly CallResult<LobbyEnter_t> entered;
    readonly CallResult<LobbyMatchList_t> listed;
    readonly Callback<LobbyChatUpdate_t> chat;
    readonly Callback<LobbyDataUpdate_t> data;
    readonly Callback<GameLobbyJoinRequested_t> invited;
    readonly Callback<SteamServersDisconnected_t> disconnected;
    readonly SteamCoopPresence presence;
    readonly Callback<PersonaStateChange_t> personaChanged;
    readonly Dictionary<ulong, Texture2D> portraits = new Dictionary<ulong, Texture2D>();
    readonly HashSet<ulong> requestedProfiles = new HashSet<ulong>();
    SteamCoopCards cards;
    SteamCoopVoice voice;
    readonly HashSet<ulong> muted = new HashSet<ulong>();
    public bool VoiceEnabled { get; private set; } = PlayerPrefs.GetInt("coop.voice.enabled", 1) != 0;
    public bool IsMuted(ulong id) => muted.Contains(id);
    public bool IsSpeaking(ulong id) => VoiceEnabled && !IsMuted(id) && voice?.IsSpeaking(id) == true;
    public void ToggleMute(ulong id)
    {
        if (id == LocalId || !peers.Contains(id)) return;
        if (muted.Add(id)) voice?.Silence(id); else muted.Remove(id);
        CoopSession.Notify();
    }
    public void SetVoiceEnabled(bool enabled)
    {
        VoiceEnabled = enabled;
        PlayerPrefs.SetInt("coop.voice.enabled", enabled ? 1 : 0); PlayerPrefs.Save();
        if (!enabled) voice?.SilenceAll();
        CoopSession.Notify();
    }
    readonly SteamRoomCodes codes = new SteamRoomCodes();
    readonly Dictionary<ulong, float> codeInvites = new Dictionary<ulong, float>();
    readonly List<ulong> expiredInvites = new List<ulong>();
    bool locallyReady, startRequested;
    public string RoomCode => InRoom ? SteamMatchmaking.GetLobbyMemberData(lobby, host, "tcg_code") : null;
    bool AcceptsCode => IsHost && InRoom && OpenPhase(lobby);
    readonly SteamRoomAdmission admission = new SteamRoomAdmission();
    readonly List<ulong> steamMembers = new List<ulong>();
    JoinStep joinStep;
    CSteamID expectedOwner;
    byte[] createKey;
    bool createPrivate, resume;
    CSteamID reconnect;
    public ulong LocalId => local.m_SteamID;
    public ulong HostId => host.m_SteamID;
    public IReadOnlyList<ulong> Players => peers;
    public bool Ready(ulong id) => id == LocalId ? locallyReady : SteamMatchmaking.GetLobbyMemberData(lobby, new CSteamID(id), "tcg_ready") == "1";
    public void ToggleReady()
    {
        if (!InRoom || CoopSession.InShop) return;
        if (IsHost && !startRequested) return;
        if (IsHost) startRequested = false;
        SetReady(!locallyReady);
        RefreshLobby();
    }
    void SetReady(bool ready)
    {
        locallyReady = ready;
        SteamMatchmaking.SetLobbyMemberData(lobby, "tcg_ready", ready ? "1" : "0");
    }

    public bool AllowJoin => HasLobby && SteamMatchmaking.GetLobbyData(lobby, "tcg_open") == "1";
    public void ToggleJoin()
    {
        if (!IsHost || !InRoom || !CoopSession.InShop) return;
        bool open = !AllowJoin;
        SteamMatchmaking.SetLobbyData(lobby, "tcg_open", open ? "1" : "0");
        CoopSession.Notify();
    }
    public void RemovePlayer(ulong id)
    {
        if (!IsHost || id == LocalId || !peers.Contains(id)) return;
        if (SteamMatchmaking.SetLobbyData(lobby, "tcg_removed_" + id, "1")) RefreshLobby();
    }
    public bool CanResume => IsHost && CoopShopCards.HasSave(LocalId);
    public void ContinueShop() { if (CanResume) StartShop(true); }
    public bool CanReconnect => reconnect.IsValid() && CanRequest;
    public void Reconnect() { if (CanReconnect) Join(reconnect.m_SteamID); }
    static bool OpenPhase(CSteamID id) => SteamMatchmaking.GetLobbyData(id, PhaseKey) == Waiting ||
        (SteamMatchmaking.GetLobbyData(id, PhaseKey) == Playing && SteamMatchmaking.GetLobbyData(id, "tcg_open") == "1");
    float deadline, rosterDeadline;
    public bool IsPrivate { get; private set; }
    public bool IsLocked { get; private set; }
    public bool NeedsPassword => joinStep == JoinStep.Password || joinStep == JoinStep.Verification;
    CSteamID lobby, host, invite, joinTarget;
    bool pendingEntry, abandonEntry, searching, returnToMenu, wasBackground, backgroundChanged;
    bool lastOnline;
    public bool Online => SteamUser.BLoggedOn() && SteamUser.GetSteamID() == local;
    public bool Busy => pendingEntry || searching || joinStep == JoinStep.Code || joinStep == JoinStep.Metadata || joinStep == JoinStep.Verification
        || (HasLobby && !InRoom);
    bool HasLobby => lobby.IsValid();
    public bool InRoom => HasLobby && peers.Contains(local.m_SteamID);
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
        presence = new SteamCoopPresence(admission, () => AcceptsCode);
        personaChanged = Callback<PersonaStateChange_t>.Create(OnPersonaChanged);
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
        if (online && (IsHost || joinStep == JoinStep.Code)) codes.Poll(ResolveCode, OnCodeResolved);
        if (online && (admission.CanReceiveRequests || joinStep == JoinStep.Verification))
            admission.Poll(local.m_SteamID, OnPasswordReply);
        if ((joinStep == JoinStep.Code || joinStep == JoinStep.Metadata || joinStep == JoinStep.Verification) && Time.realtimeSinceStartup > deadline)
        {
            if (joinStep == JoinStep.Verification) OnPasswordReply(false, false);
            else { CancelGate(); SetStatus(LocalizationKeys.CoopFailed); }
        }
        if (HasLobby && !InRoom && Time.realtimeSinceStartup > rosterDeadline) EndRoom(LocalizationKeys.CoopFailed);
        if (InRoom && CoopSession.InShop && online && GameScenes.IsActiveGameScene() && CardInstancedRenderManager.IsGameplayReady)
        {
            presence.Tick(lobby.m_SteamID);
            if (cards == null)
            {
                cards = new SteamCoopCards(lobby.m_SteamID, LocalId, HostId, shopPeers, presence.PlayerBody);
                if (IsHost && !CoopShopCards.Active.UseCheckpoint(CoopShopCards.SavePath(LocalId), resume))
                { EndRoom(resume ? "coop.save_failed" : "save.failed"); return; }
            }
            cards.Tick();
            if (!IsHost && cards.ConnectionTimedOut)
            {
                reconnect = lobby;
                EndRoom("coop.host_connection_lost");
                return;
            }
            voice ??= new SteamCoopVoice(lobby.m_SteamID, LocalId, HostId, shopPeers, IsMuted);
            voice.Tick(VoiceEnabled);
        }
    }

    bool CanRequest => Online && CoopSession.CanEnter && !HasLobby && joinStep == JoinStep.Idle && !Busy;

    public void Search()
    {
        if (!CanRequest) return;
        rooms.Clear();
        SteamMatchmaking.AddRequestLobbyListStringFilter(ProtocolKey, Protocol, ELobbyComparison.k_ELobbyComparisonEqual);

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
                if (!Compatible(id) || count >= capacity || !OpenPhase(id)
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
            && SteamMatchmaking.SetLobbyData(id, "tcg_open", "1")
            && SteamMatchmaking.SetLobbyData(id, PhaseKey, Waiting);
        if (!configured) { ClearCreateKey(); SteamMatchmaking.LeaveLobby(id); SetStatus(LocalizationKeys.CoopFailed); return; }
        if (createKey != null)
        {
            admission.Open(id.m_SteamID, local.m_SteamID, createKey);
            createKey = null; // The admission gate now owns and clears this array.
        }
        SteamMatchmaking.SetLobbyMemberData(id, "tcg_code", codes.Open(LocalId));
        Enter(id, local);
    }

    public void JoinByCode(string code)
    {
        if (!CanRequest) return;
        if (!codes.Request(LocalId, code)) { SetStatus("coop.bad_code"); return; }
        joinStep = JoinStep.Code;
        deadline = Time.realtimeSinceStartup + 12f;
        SetStatus(LocalizationKeys.CoopWorking);
    }

    ulong ResolveCode(ulong sender)
    {
        if (!AcceptsCode || sender == LocalId || steamMembers.Contains(sender) ||
            SteamMatchmaking.GetNumLobbyMembers(lobby) >= CoopSession.Capacity ||
            SteamMatchmaking.GetLobbyData(lobby, "tcg_removed_" + sender) == "1") return 0;
        if (!IsPrivate) return lobby.m_SteamID;
        float now = Time.realtimeSinceStartup;
        expiredInvites.Clear();
        foreach (var entry in codeInvites)
            if (entry.Value <= now) expiredInvites.Add(entry.Key);
        foreach (ulong id in expiredInvites) codeInvites.Remove(id);
        if (codeInvites.ContainsKey(sender)) return lobby.m_SteamID;
        if (codeInvites.Count >= 64 || !SteamMatchmaking.InviteUserToLobby(lobby, new CSteamID(sender))) return 0;
        codeInvites[sender] = now + 15f;
        return lobby.m_SteamID;
    }

    void OnCodeResolved(ulong id)
    {
        if (joinStep != JoinStep.Code) return;
        joinStep = JoinStep.Idle;
        if (id == 0) SetStatus(LocalizationKeys.CoopFailed);
        else Join(id);
    }

    public void GetOnlineFriends(List<CoopFriend> friends)
    {
        friends.Clear();
        if (!InRoom || !Online) return;
        const EFriendFlags flags = EFriendFlags.k_EFriendFlagImmediate;
        int count = SteamFriends.GetFriendCount(flags);
        for (int i = 0; i < count; i++)
        {
            var id = SteamFriends.GetFriendByIndex(i, flags);
            if (CanInvite(id)) friends.Add(new CoopFriend(id.m_SteamID, SteamFriends.GetFriendPersonaName(id)));
        }
        friends.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
    }

    bool CanInvite(CSteamID id) => InRoom && Online && OpenPhase(lobby) && steamMembers.Count < CoopSession.Capacity &&
        !steamMembers.Contains(id.m_SteamID) && SteamFriends.GetFriendPersonaState(id) != EPersonaState.k_EPersonaStateOffline &&
        SteamMatchmaking.GetLobbyData(lobby, "tcg_removed_" + id.m_SteamID) != "1";

    void OnPersonaChanged(PersonaStateChange_t change)
    {
        ulong id = change.m_ulSteamID;
        if (!peers.Contains(id)) return;
        if ((change.m_nChangeFlags & EPersonaChange.k_EPersonaChangeAvatar) != 0 && portraits.TryGetValue(id, out var old))
        {
            UnityEngine.Object.Destroy(old);
            portraits.Remove(id);
        }
        RefreshLobby();
    }

    public Texture2D GetPortrait(ulong member)
    {
        if (!peers.Contains(member)) return null;
        if (portraits.TryGetValue(member, out var cached)) return cached;
        var id = new CSteamID(member);
        int image = SteamFriends.GetSmallFriendAvatar(id);
        if (image <= 0)
        {
            if (requestedProfiles.Add(member)) SteamFriends.RequestUserInformation(id, false);
            return null;
        }
        if (!SteamUtils.GetImageSize(image, out uint w, out uint h) || w == 0 || h == 0 || w > 64 || h > 64) return null;
        byte[] rgba = new byte[w * h * 4];
        if (!SteamUtils.GetImageRGBA(image, rgba, rgba.Length)) return null;
        var texture = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false);
        texture.LoadRawTextureData(rgba);
        texture.Apply(false, true);
        requestedProfiles.Add(member);
        portraits.Add(member, texture);
        return texture;
    }

    public bool InviteFriend(ulong id)
    {
        var friend = new CSteamID(id);
        bool sent = CanInvite(friend) && SteamMatchmaking.InviteUserToLobby(lobby, friend);
        SetStatus(sent ? null : LocalizationKeys.CoopFailed);
        return sent;
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
        string reason = null;
        bool validOwner = ulong.TryParse(SteamMatchmaking.GetLobbyData(joinTarget, HostKey), out ulong owner) && owner != 0;
        if (!Compatible(joinTarget) || (locked != "0" && locked != "1") || !validOwner) reason = LocalizationKeys.CoopFailed;
        else if (SteamMatchmaking.GetLobbyData(joinTarget, "tcg_removed_" + LocalId) == "1") reason = "coop.removed";
        else if (!OpenPhase(joinTarget) && SteamMatchmaking.GetLobbyData(joinTarget, "tcg_played_" + LocalId) != "1") reason = "coop.closed";
        else if (SteamMatchmaking.GetLobbyMemberLimit(joinTarget) > 0 &&
                 SteamMatchmaking.GetNumLobbyMembers(joinTarget) >= SteamMatchmaking.GetLobbyMemberLimit(joinTarget)) reason = "coop.full";
        if (reason != null)
        {
            CancelGate();
            SetStatus(reason);
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
        string rejection = !failure && result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseFull
            ? "coop.full" : LocalizationKeys.CoopFailed;
        if (!AcceptEntry(id, joined, rejection)) return;
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

    bool AcceptEntry(CSteamID id, bool success, string rejection = LocalizationKeys.CoopFailed)
    {
        pendingEntry = false;
        bool abandoned = abandonEntry || !CoopSession.CanEnter || !Online;
        abandonEntry = false;
        if (abandoned && success) SteamMatchmaking.LeaveLobby(id);
        if (success && !abandoned) return true;
        SetStatus(abandoned ? StatusKey : rejection);
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
        SetReady(false);
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
        if ((e.m_rgfChatMemberStateChange & ~(uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
        {
            waitingGuests.Remove(e.m_ulSteamIDUserChanged);
            if (IsHost && IsLocked) admission.Forget(e.m_ulSteamIDUserChanged);
        }
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
        if (SteamMatchmaking.GetLobbyData(lobby, "tcg_removed_" + LocalId) == "1")
        { reconnect = default; EndRoom("coop.removed"); return; }
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
        string[] approved = !IsHost ? roster.Split(',') : null;
        waitingGuests.RemoveWhere(id => !steamMembers.Contains(id));
        if (IsLocked && IsHost) admission.AdmitMembers(steamMembers);
        foreach (ulong member in steamMembers)
        {
            if (IsLocked && (IsHost ? !admission.IsAdmitted(member) : Array.IndexOf(approved, member.ToString(CultureInfo.InvariantCulture)) < 0)) continue;
            if (SteamMatchmaking.GetLobbyData(lobby, "tcg_removed_" + member) == "1") continue;
            if (SteamMatchmaking.GetLobbyData(lobby, PhaseKey) == Playing && SteamMatchmaking.GetLobbyData(lobby, "tcg_played_" + member) != "1")
            {
                if (AllowJoin) waitingGuests.Add(member);
                else if (!waitingGuests.Contains(member) && (IsHost || Array.IndexOf(approved, member.ToString(CultureInfo.InvariantCulture)) < 0)) continue;
                if (IsHost && Ready(member) && !SteamMatchmaking.SetLobbyData(lobby, "tcg_played_" + member, "1")) continue;
            }
            peers.Add(member);
            names.Add(SteamFriends.GetFriendPersonaName(new CSteamID(member)));
        }
        if (IsHost)
        {
            string updated = string.Join(",", peers);
            if (updated != roster && !SteamMatchmaking.SetLobbyData(lobby, RosterKey, updated))
            {
                EndRoom(LocalizationKeys.CoopFailed);
                return;
            }
        }
        foreach (var id in requestedProfiles)
            if (!peers.Contains(id) && portraits.TryGetValue(id, out var portrait))
            {
                UnityEngine.Object.Destroy(portrait);
                portraits.Remove(id);
            }
        requestedProfiles.RemoveWhere(id => !peers.Contains(id));
        // A late arrival can stay in the lobby before pressing Ready. Stream shop
        // state, poses and voice only to players who have entered the session.
        shopPeers.Clear();
        foreach (ulong id in peers)
            if (Ready(id)) shopPeers.Add(id);
        presence.SetMembers(local.m_SteamID, host.m_SteamID, shopPeers);
        voice?.UpdateMembers();
        if (!peers.Contains(LocalId) && !OpenPhase(lobby) && SteamMatchmaking.GetLobbyData(lobby, "tcg_played_" + LocalId) != "1")
        { EndRoom(LocalizationKeys.CoopFailed); return; }
        if (InRoom) reconnect = default;
        if (InRoom && locallyReady && !CoopSession.InShop)
        {
            if (SteamMatchmaking.GetLobbyData(lobby, PhaseKey) == Playing) CoopSession.BeginShop();
            else if (IsHost && startRequested && peers.TrueForAll(Ready)) LaunchShop();
        }
        CoopSession.Notify();
    }

    public void Invite()
    {
        if (!InRoom || !Online) return;
        if (SteamUtils.IsOverlayEnabled()) SteamFriends.ActivateGameOverlayInviteDialog(lobby);
        else SetStatus(LocalizationKeys.CoopOverlay);
    }

    public void StartShop() => StartShop(false);

    void StartShop(bool fromSave)
    {
        if (!IsHost || !CoopSession.CanEnter || !Online) return;
        resume = fromSave;
        startRequested = true;
        SetReady(true);
        RefreshLobby();
    }

    void LaunchShop()
    {
        startRequested = false;
        foreach (ulong id in peers)
            if (!SteamMatchmaking.SetLobbyData(lobby, "tcg_played_" + id, "1")) { SetReady(false); SetStatus(LocalizationKeys.CoopFailed); return; }
        if (!SteamMatchmaking.SetLobbyData(lobby, PhaseKey, Playing))
        {
            SetReady(false);
            SetStatus(LocalizationKeys.CoopFailed);
            return;
        }
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
        if (InRoom && !IsHost && CoopSession.InShop) reconnect = lobby;
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
        codes.Reset();
        codeInvites.Clear();
        foreach (var portrait in portraits.Values) UnityEngine.Object.Destroy(portrait);
        portraits.Clear();
        requestedProfiles.Clear();
        locallyReady = startRequested = false;
        voice?.Dispose();
        voice = null;
        cards?.Dispose();
        cards = null;
        presence.Clear();
        if (HasLobby)
        {
            if (IsHost && Online) SteamMatchmaking.SetLobbyJoinable(lobby, false);
            SteamMatchmaking.LeaveLobby(lobby);
        }
        lobby = host = default;
        IsLocked = IsPrivate = resume = false;
        rooms.Clear();
        names.Clear();
        peers.Clear();
        shopPeers.Clear();
        waitingGuests.Clear();
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
        codes.Cancel();
        expectedOwner = default;
    }

    public void Dispose()
    {
        Leave();
        created.Dispose(); entered.Dispose(); listed.Dispose();
        chat.Dispose(); data.Dispose(); invited.Dispose(); disconnected.Dispose();
        presence.Dispose();
        personaChanged.Dispose();
    }
}
#endif
