using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public readonly struct CoopRoomInfo
{
    public readonly ulong Id;
    public readonly string HostName;
    public readonly int Players, Capacity;
    public readonly bool PasswordRequired;

    public CoopRoomInfo(ulong id, string hostName, int players, int capacity, bool passwordRequired = false)
    {
        Id = id;
        HostName = hostName;
        Players = players;
        Capacity = capacity;
        PasswordRequired = passwordRequired;
    }
}

// The game assembly stays independent of Steamworks.NET.
public interface ICoopRooms
{
    bool Online { get; }
    bool Busy { get; }
    bool InRoom { get; }
    bool IsHost { get; }
    bool NeedsPassword { get; }
    bool IsPrivate { get; }
    bool IsLocked { get; }
    IReadOnlyList<CoopRoomInfo> PublicRooms { get; }
    IReadOnlyList<string> Members { get; }
    string StatusKey { get; }
    void Search();
    void Host(bool inviteOnly, string password);
    void SubmitPassword(string password);
    void Join(ulong lobby);
    void Invite();
    void StartShop();
    void Leave();
}

public interface ICoopRoomControls
{
    ulong LocalId { get; }
    ulong HostId { get; }
    IReadOnlyList<ulong> Players { get; }
    bool Ready(ulong id);
    void ToggleReady();
    bool AllowJoin { get; }
    void ToggleJoin();
    void RemovePlayer(ulong id);
    bool CanResume { get; }
    void ContinueShop();
    bool CanReconnect { get; }
    void Reconnect();
}

public static class CoopSession
{
    public const int Capacity = 4;
    public const int MaxPasswordLength = 64;
    public static ICoopRooms Rooms { get; private set; }
    public static bool InShop { get; private set; }
    public static bool ShowLobbyOnMenu { get; private set; }
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Rooms = null;
        InShop = false;
        ShowLobbyOnMenu = false;
        Changed = null;
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameScenes.MainMenu || !InShop) return;
        InShop = false;
        if (Rooms?.InRoom == true) Rooms.Leave();
        Notify();
    }

    public static void SetRooms(ICoopRooms rooms)
    {
        Rooms = rooms;
        Notify();
    }

    public static void ShowLobby(bool show)
    {
        ShowLobbyOnMenu = show;
        Notify();
    }

    public static void Notify() => Changed?.Invoke();

    public static bool CanEnter => SceneManager.GetActiveScene().name == GameScenes.MainMenu
        && !GameSceneLoader.IsLoading && !InShop;

    public static void BeginShop()
    {
        if (!CanEnter) return;
        InShop = true;
        ShowLobbyOnMenu = true;
        GameSceneLoader.StartNewGame();
    }
}
