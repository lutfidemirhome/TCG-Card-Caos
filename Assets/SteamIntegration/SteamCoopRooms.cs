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
    const string Protocol = "1";
    const string HostKey = "tcg_host";
    const string PhaseKey = "tcg_phase";
    const string Waiting = "waiting", Playing = "playing";

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
    CSteamID lobby, host, invite, joinTarget;
    bool pendingEntry, abandonEntry, searching, returnToMenu, wasBackground, backgroundChanged;
    bool lastOnline;
    public bool Online => SteamUser.BLoggedOn() && SteamUser.GetSteamID() == local;
    public bool Busy => pendingEntry || searching;
    public bool InRoom => lobby.IsValid();
    public bool IsHost => InRoom && local == host;
    public IReadOnlyList<CoopRoomInfo> PublicRooms => rooms;
    public IReadOnlyList<string> Members => names;
    public string StatusKey { get; private set; }

    public SteamCoopRooms(CSteamID local)
    {
        this.local = local;
        created = CallResult<LobbyCreated_t>.Create(OnCreated);
        entered = CallResult<LobbyEnter_t>.Create(OnEntered);
        listed = CallResult<LobbyMatchList_t>.Create(OnList);
        chat = Callback<LobbyChatUpdate_t>.Create(e => { if (e.m_ulSteamIDLobby == lobby.m_SteamID) RefreshLobby(); });
        data = Callback<LobbyDataUpdate_t>.Create(e => { if (e.m_ulSteamIDLobby == lobby.m_SteamID) RefreshLobby(); });
        invited = Callback<GameLobbyJoinRequested_t>.Create(e => QueueInvite(e.m_steamIDLobby));
        disconnected = Callback<SteamServersDisconnected_t>.Create(e => OnDisconnect());
        presence = new SteamCoopPresence();
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
        if (InRoom && !CoopSession.InShop && !CoopSession.CanEnter) Leave();
        if (online && invite.IsValid() && CoopSession.CanEnter && !InRoom && !pendingEntry)
        {
            CSteamID target = invite;
            invite = default;
            CancelSearch();
            CoopSession.ShowLobby(true);
            Join(target.m_SteamID);
        }
        if (InRoom && CoopSession.InShop && online)
            presence.Tick(lobby.m_SteamID);
    }

    bool CanRequest => Online && CoopSession.CanEnter && !InRoom && !Busy;

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
                if (!Compatible(id) || count >= capacity || SteamMatchmaking.GetLobbyData(id, PhaseKey) != Waiting) continue;
                string name = SteamMatchmaking.GetLobbyData(id, "name");
                rooms.Add(new CoopRoomInfo(id.m_SteamID, name, count, capacity));
            }
        SetStatus(failure ? LocalizationKeys.CoopFailed : rooms.Count == 0 ? LocalizationKeys.CoopEmpty : null);
    }

    public void Host()
    {
        if (!CanRequest) return;
        var call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, CoopSession.Capacity);
        if (call == SteamAPICall_t.Invalid) { SetStatus(LocalizationKeys.CoopFailed); return; }
        pendingEntry = true;
        created.Set(call);
        SetStatus(LocalizationKeys.CoopWorking);
    }

    void OnCreated(LobbyCreated_t result, bool failure)
    {
        var id = new CSteamID(result.m_ulSteamIDLobby);
        if (!AcceptEntry(id, !failure && result.m_eResult == EResult.k_EResultOK)) return;
        bool configured = SteamMatchmaking.SetLobbyData(id, ProtocolKey, Protocol)
            && SteamMatchmaking.SetLobbyData(id, HostKey, local.m_SteamID.ToString(CultureInfo.InvariantCulture))
            && SteamMatchmaking.SetLobbyData(id, "name", SteamFriends.GetPersonaName())
            && SteamMatchmaking.SetLobbyData(id, PhaseKey, Waiting);
        if (!configured) { SteamMatchmaking.LeaveLobby(id); SetStatus(LocalizationKeys.CoopFailed); return; }
        Enter(id, local);
    }

    public void Join(ulong id)
    {
        if (!CanRequest) return;
        joinTarget = new CSteamID(id);
        if (!joinTarget.IsLobby()) { SetStatus(LocalizationKeys.CoopFailed); return; }
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
        if (id != joinTarget || !Compatible(id) || !HostMatches(id, owner) || (phase != Waiting && phase != Playing))
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
        lobby = id;
        host = owner;
        wasBackground = Application.runInBackground;
        Application.runInBackground = true;
        backgroundChanged = true;
        StatusKey = null;
        SteamNetworkingUtils.InitRelayNetworkAccess();
        CoopSession.ShowLobby(true);
        RefreshLobby();
    }

    void RefreshLobby()
    {
        if (!InRoom) return;
        if (SteamMatchmaking.GetLobbyOwner(lobby) != host || !HostMatches(lobby, host))
        {
            EndRoom(LocalizationKeys.CoopHostLeft);
            return;
        }
        peers.Clear();
        names.Clear();
        int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
        for (int i = 0; i < count; i++)
        {
            var member = SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
            peers.Add(member.m_SteamID);
            names.Add(SteamFriends.GetFriendPersonaName(member));
        }
        if (!peers.Contains(local.m_SteamID) || !peers.Contains(host.m_SteamID))
        {
            EndRoom(LocalizationKeys.CoopHostLeft);
            return;
        }
        presence.SetMembers(local.m_SteamID, host.m_SteamID, peers);
        if (!CoopSession.InShop && SteamMatchmaking.GetLobbyData(lobby, PhaseKey) == Playing)
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
        if (!Busy && !InRoom) return;
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
        presence.Clear();
        if (InRoom)
        {
            if (IsHost && Online) SteamMatchmaking.SetLobbyJoinable(lobby, false);
            SteamMatchmaking.LeaveLobby(lobby);
        }
        lobby = host = default;
        rooms.Clear();
        names.Clear();
        peers.Clear();
        if (backgroundChanged) Application.runInBackground = wasBackground;
        backgroundChanged = false;
        if (CoopSession.InShop) returnToMenu = true;
        SetStatus(reason);
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
