using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class CoopTeamPanelTests
{
    sealed class Room : ICoopRooms, ICoopRoomControls, ICoopVoiceSettings
    {
        public ulong LocalId { get; set; } = 1;
        public ulong HostId => 1;
        public IReadOnlyList<ulong> Players { get; } = new ulong[] { 1, 2 };
        public IReadOnlyList<string> Members { get; } = new[] { "Host", "Guest" };
        public bool AllowJoin { get; private set; }
        public bool VoiceEnabled { get; private set; } = true;
        public ulong Muted { get; private set; }
        public ulong Removed { get; private set; }
        public void RemovePlayer(ulong id) => Removed = id;
        public bool IsMuted(ulong id) => Muted == id;
        public void ToggleMute(ulong id) => Muted = IsMuted(id) ? 0 : id;
        public void SetVoiceEnabled(bool enabled) => VoiceEnabled = enabled;
        public void ToggleJoin() => AllowJoin = !AllowJoin;
        public bool Online => true;
        public bool InRoom => true;
        public bool IsHost => LocalId == HostId;
        public bool Busy => false;
        public bool IsPrivate => false;
        public bool IsLocked => false;
        public bool NeedsPassword => false;
        public bool CanResume => false;
        public bool CanReconnect => false;
        public IReadOnlyList<CoopRoomInfo> PublicRooms => Array.Empty<CoopRoomInfo>();
        public string StatusKey => null;
        public bool Ready(ulong id) => true;
        public void ToggleReady() { }
        public void Search() { }
        public void Host(bool inviteOnly, string password) { }
        public void SubmitPassword(string password) { }
        public void Join(ulong lobby) { }
        public void Invite() { }
        public void StartShop() { }
        public void ContinueShop() { }
        public void Reconnect() { }
        public void Leave() { }
    }

    GameObject canvas;
    ICoopRooms previous;
    Room room;

    [SetUp]
    public void OpenPanel()
    {
        previous = CoopSession.Rooms;
        room = new Room();
        CoopSession.SetRooms(room);
        canvas = new GameObject("Pause UI test", typeof(Canvas));
    }

    Button Find(string key) => canvas.GetComponentsInChildren<Button>().SingleOrDefault(b => b.name == key);

    [UnityTest]
    public IEnumerator HostCanMuteOnePlayerAndDisableAndRestoreVoice()
    {
        CoopTeamPanel.Create(canvas.transform);
        yield return null;
        Find("coop.mute").onClick.Invoke();
        Assert.That(room.Muted, Is.EqualTo(2));
        yield return null;
        Find("coop.voice_on").onClick.Invoke();
        Assert.That(room.VoiceEnabled, Is.False);
        yield return null;
        Find("coop.voice_off").onClick.Invoke();
        Assert.That(room.VoiceEnabled, Is.True);
        Assert.That(room.IsMuted(2), Is.True);
        Find("coop.unmute").onClick.Invoke();
        Assert.That(room.IsMuted(2), Is.False);
        Find("coop.open_entries").onClick.Invoke();
        Assert.That(room.AllowJoin, Is.True);
        yield return null;
        Find("coop.close_entries").onClick.Invoke();
        Find("coop.remove").onClick.Invoke();
        Assert.That(room.AllowJoin, Is.False);
        Assert.That(room.Removed, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator GuestCanMuteHostAndCannotRemovePlayersOrControlAdmission()
    {
        room.LocalId = 2;
        CoopTeamPanel.Create(canvas.transform);
        yield return null;
        Assert.That(Find("coop.remove"), Is.Null);
        Assert.That(Find("coop.open_entries"), Is.Null);
        Find("coop.mute").onClick.Invoke();
        Assert.That(room.Muted, Is.EqualTo(1));
    }

    [TearDown]
    public void ClosePanel()
    {
        UnityEngine.Object.DestroyImmediate(canvas);
        CoopSession.SetRooms(previous);
    }
}
