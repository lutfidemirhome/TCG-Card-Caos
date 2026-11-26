using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class CoopLobbyNavigationTests
{
    sealed class GuestRoom : ICoopRooms, ICoopRoomControls, ICoopRoomInvites
    {
        public bool Online => true;
        public bool Busy => false;
        public bool InRoom { get; private set; }
        public bool IsHost => false;
        public bool NeedsPassword { get; private set; }
        public bool RequiresPassword;
        public bool IsPrivate => false;
        public bool IsLocked => false;
        public string StatusKey => null;
        public ulong LocalId => 2;
        public ulong HostId => 1;
        public bool AllowJoin => true;
        public bool CanResume => false;
        public bool CanReconnect => false;
        public string RoomCode => "00000001-12345678-00000001";
        public IReadOnlyList<ulong> Players => new ulong[] { 1, 2 };
        public IReadOnlyList<string> Members => new[] { "Host", "Guest" };
        readonly List<CoopRoomInfo> results = new List<CoopRoomInfo>();
        public IReadOnlyList<CoopRoomInfo> PublicRooms => results;
        public int Searches;
        public bool GuestReady;
        public ulong Invited;
        public bool Ready(ulong id) => id == HostId || GuestReady;
        public void ToggleReady() { GuestReady = !GuestReady; CoopSession.Notify(); }
        public void Search()
        {
            Searches++;
            results.Clear();
            results.Add(new CoopRoomInfo(12, "Public shop", 1, 4, RequiresPassword));
            CoopSession.Notify();
        }
        public void Join(ulong lobby)
        {
            Assert.That(lobby, Is.EqualTo(12));
            NeedsPassword = RequiresPassword;
            InRoom = !NeedsPassword;
            CoopSession.Notify();
        }
        public void JoinByCode(string code) { Assert.That(code, Is.EqualTo(RoomCode)); Join(12); }
        public void GetOnlineFriends(List<CoopFriend> friends) { friends.Clear(); friends.Add(new CoopFriend(3, "Friend")); }
        public bool InviteFriend(ulong id) { Invited = id; return true; }
        public void Leave() { InRoom = GuestReady = NeedsPassword = false; results.Clear(); CoopSession.Notify(); }
        public void Host(bool inviteOnly, string password) => Assert.Fail();
        public void StartShop() => Assert.Fail();
        public void SubmitPassword(string password) => Assert.Fail();
        public void Invite() => Assert.Fail();
        public void ToggleJoin() => Assert.Fail();
        public void RemovePlayer(ulong id) => Assert.Fail();
        public void Reconnect() => Assert.Fail();
        public void ContinueShop() => Assert.Fail();
    }

    GuestRoom room;
    MainMenuView menu;
    Scene previousScene;
    ICoopRooms previousRooms;

    [UnitySetUp]
    public IEnumerator OpenMultiplayer()
    {
        previousScene = SceneManager.GetActiveScene();
        previousRooms = CoopSession.Rooms;
        yield return SceneManager.LoadSceneAsync(GameScenes.MainMenu, LoadSceneMode.Additive);
        var scene = SceneManager.GetSceneByName(GameScenes.MainMenu);
        SceneManager.SetActiveScene(scene);
        menu = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MainMenuView>(true)).Single();
        room = new GuestRoom();
        CoopSession.SetRooms(room);
        var entry = menu.GetComponentsInChildren<Button>().Single(b => b.name == "Button_Coop");
        Assert.That(entry.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(Localization.Get(LocalizationKeys.CoopTitle)));
        entry.onClick.Invoke();
        yield return null;
        Assert.That(room.Searches, Is.Zero);
    }

    Button ButtonWith(string text) => menu.GetComponentsInChildren<Button>()
        .Single(b => b.GetComponentInChildren<TMP_Text>()?.text == text);
    void Click(string key) => ButtonWith(Localization.Get(key)).onClick.Invoke();

    [UnityTest]
    public IEnumerator BrowseJoinReadyAndLeave()
    {
        Click("coop.browse");
        yield return null;
        Assert.That(room.Searches, Is.EqualTo(1));
        ButtonWith("Public shop   1 / 4").onClick.Invoke();
        yield return null;
        Assert.That(menu.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains(room.RoomCode)), Is.False);
        Click("coop.show_code");
        yield return null;
        Assert.That(ButtonWith(room.RoomCode), Is.Not.Null);
        Click("coop.ready");
        yield return null;
        Assert.That(room.GuestReady, Is.True);
        Click("coop.unready");
        yield return null;
        Click(LocalizationKeys.CoopLeave);
        yield return null;
        Assert.That(room.InRoom, Is.False);
        Assert.That(ButtonWith(Localization.Get("coop.browse")), Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator BackFromPasswordReturnsToPopulatedRoomList()
    {
        room.RequiresPassword = true;
        Click("coop.browse");
        yield return null;
        string label = "Public shop   1 / 4 · " + Localization.Get(LocalizationKeys.CoopLocked);
        ButtonWith(label).onClick.Invoke();
        yield return null;
        Assert.That(room.NeedsPassword, Is.True);
        Click(LocalizationKeys.CoopBack);
        yield return null;
        Assert.That(room.NeedsPassword, Is.False);
        Assert.That(room.Searches, Is.EqualTo(2));
        Assert.That(ButtonWith(label).interactable, Is.True);
    }

    [UnityTest]
    public IEnumerator PasteCodeAndInviteAnOnlineFriend()
    {
        var input = menu.GetComponentsInChildren<TMP_InputField>().Single();
        Assert.That(ButtonWith(Localization.Get(LocalizationKeys.CoopJoin)).interactable, Is.False);
        input.text = room.RoomCode;
        Click(LocalizationKeys.CoopJoin);
        yield return null;
        Click(LocalizationKeys.CoopInvite);
        yield return null;
        ButtonWith("Friend").onClick.Invoke();
        yield return null;
        Assert.That(room.Invited, Is.EqualTo(3UL));
        Assert.That(ButtonWith("Friend  · " + Localization.Get("coop.invited")).interactable, Is.False);
        Click(LocalizationKeys.CoopBack);
        yield return null;
        Assert.That(room.InRoom, Is.True);
    }

    [UnityTearDown]
    public IEnumerator CloseMenu()
    {
        CoopSession.ShowLobby(false);
        CoopSession.SetRooms(previousRooms);
        SceneManager.SetActiveScene(previousScene);
        yield return SceneManager.UnloadSceneAsync(GameScenes.MainMenu);
    }
}
