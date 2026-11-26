using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CoopShopCardTests
{
    const ulong Host = 11, Guest = 22;
    readonly List<CoopCardMessage> delivered = new List<CoopCardMessage>();
    GameObject root;
    WorldCard first, second;
    WorldBoosterPack fixturePack;
    CardShelfSlot slot;
    CoopShopCards session;
    PlayerCardHand hand;

    [SetUp]
    public void SetUp()
    {
        delivered.Clear();
        root = new GameObject("Co-op fixture");
        hand = PlayerCardHand.Instance;
        if (!hand) hand = root.AddComponent<PlayerCardHand>();
        var shelf = root.AddComponent<CardShelf>();
        var marker = new GameObject("Fixture slot");
        marker.transform.SetParent(root.transform, false);
        slot = marker.AddComponent<CardShelfSlot>();
        shelf.RefreshSlotCache();
        var art = CardCatalog.All.First(definition => definition.ShelfCategoryId == shelf.CategoryId
            && definition.ShelfSlotNumber == slot.SlotNumber);
        first = CardFactory.CreateWorldCard(Vector3.up, Quaternion.identity, art, 0);
        second = CardFactory.CreateWorldCard(Vector3.up + Vector3.right, Quaternion.identity, art, 0);
    }

    void StartSession(ulong local)
    {
        session = new CoopShopCards(local, Host, new[] { Host, Guest },
            (_, message) => delivered.Add(message), _ => root.transform);
    }

    [TestCase(Host)]
    [TestCase(Guest)]
    public void LookingAtAGroundCardAndPressingInteractUsesTheRoomPickup(ulong local)
    {
        var cameraObject = new GameObject("Player view", typeof(Camera));
        cameraObject.transform.SetParent(hand.transform, false);
        cameraObject.transform.position = first.transform.position + Vector3.up * 2f;
        cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        var interaction = cameraObject.AddComponent<InteractionController>();
        var frame = (Action<bool, bool, bool>)Delegate.CreateDelegate(typeof(Action<bool, bool, bool>), interaction,
            typeof(InteractionController).GetMethod("UpdateInteraction", BindingFlags.Instance | BindingFlags.NonPublic));
        var inShop = typeof(CoopSession).GetProperty(nameof(CoopSession.InShop));
        bool previous = CoopSession.InShop;
        try
        {
            inShop.SetValue(null, true);
            StartSession(local);
            CardGroundStack.Track(first);
            Physics.SyncTransforms();
            frame(true, false, false);
            Assert.That(CardInteractionFocus.FocusedCard, Is.SameAs(first));
            frame(true, true, false);
            if (local == Host) Assert.That(hand.SelectedHeldCard, Is.SameAs(first));
            else
            {
                var request = delivered.Single(m => m.Action == CoopCardAction.Take);
                Assert.That(first.IsHeld, Is.False);
                session.Receive(Host, new CoopCardMessage { Action = CoopCardAction.Held, Card = request.Card,
                    Actor = Guest, Revision = 1, Request = request.Request });
                Assert.That(hand.SelectedHeldCard, Is.SameAs(first));
            }
        }
        finally
        {
            inShop.SetValue(null, previous);
            CardInteractionFocus.ClearFocus();
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    static Guid Id(WorldCard card) => Guid.Parse(PersistentId.Resolve(card));

    [Test]
    public void CompetingPickupsAndShelfRequestsLeaveOneOwnerPerCardAndSlot()
    {
        StartSession(Host);
        session.Receive(Guest, new CoopCardMessage { Action = CoopCardAction.Sync });
        Assert.That(hand.TryPickup(first), Is.True);
        var take = new CoopCardMessage { Action = CoopCardAction.Take, Card = Id(first), Request = 1 };
        session.Receive(Guest, take);
        Assert.That(delivered.Last().Action, Is.EqualTo(CoopCardAction.Denied));
        take.Card = Id(second);
        session.Receive(Guest, take);
        Assert.That(second.IsHeld, Is.True);
        Assert.That(hand.SelectedHeldCard, Is.SameAs(first));

        Assert.That(slot.GetComponentInParent<CardShelf>().IsCorrectPlacement(first, slot), Is.True);
        Assert.That(session.RequestShelf(first, slot), Is.True);
        Assert.That(slot.OccupiedCard, Is.SameAs(first));
        session.Tick();
        var guestCard = delivered.Single(m => m.Card == Id(second) && m.Action == CoopCardAction.Held);
        var shelf = delivered.Single(m => m.Action == CoopCardAction.OnShelf);
        session.Receive(Guest, new CoopCardMessage { Action = CoopCardAction.Shelve, Request = 2,
            Card = guestCard.Card, Revision = guestCard.Revision, Slot = shelf.Slot });
        Assert.That(delivered.Last().Action, Is.EqualTo(CoopCardAction.Denied));
        Assert.That(slot.OccupiedCard, Is.SameAs(first));
        Assert.That(second.IsHeld, Is.True);
    }

    [Test]
    public void AClaimedPackCanOnlyBeReleasedByItsOpener()
    {
        var packObject = new GameObject("Fixture pack");
        packObject.transform.SetParent(root.transform, false);
        var pack = fixturePack = packObject.AddComponent<WorldBoosterPack>();
        pack.Initialize(null, preRolledContents: CardCatalog.All.Take(CardDimensions.CardsPerBoosterPack).ToArray());
        StartSession(Guest);
        Assert.That(session.PickupPack(pack), Is.True);
        var request = delivered.Single();
        session.Dispose();
        delivered.Clear();

        StartSession(Host);
        session.Receive(Guest, new CoopCardMessage { Action = CoopCardAction.Sync });
        session.Receive(Guest, request);
        session.Tick();
        var heldPack = delivered.Single(m => m.Action == CoopCardAction.Held);
        Assert.That(session.PickupPack(pack), Is.False);
        var open = new CoopCardMessage { Card = heldPack.Card, Action = CoopCardAction.OpenPack,
            Revision = heldPack.Revision, Request = 2 };
        session.Receive(Guest, open);
        session.Receive(Guest, open);
        session.Tick();
        var claim = delivered.Single(m => m.Action == CoopCardAction.PackClaimed);
        Assert.That(claim.Actor, Is.EqualTo(Guest));
        var denial = delivered.Single(m => m.Action == CoopCardAction.Denied && m.Request == open.Request);
        Assert.That(denial.Revision, Is.EqualTo(claim.Revision));
        Assert.That(session.OpenPack(pack, hand), Is.False);
        var cancel = new CoopCardMessage { Action = CoopCardAction.CancelPack, Card = claim.Card,
            Revision = claim.Revision, Request = 2 };
        session.Receive(Host, cancel);
        Assert.That(session.OpenPack(pack, hand), Is.False);
        session.Receive(Guest, cancel);
        session.Tick();
        Assert.That(delivered.Count(m => m.Action == CoopCardAction.PackReleased), Is.EqualTo(1));
    }

    [Test]
    public void GuestOpensPackWhenRetryDenialArrivesBeforeApproval()
    {
        var camera = new GameObject("Pack camera", typeof(Camera));
        camera.transform.SetParent(root.transform, false);
        camera.tag = "MainCamera";
        var packObject = new GameObject("Retried pack");
        packObject.transform.SetParent(root.transform, false);
        var pack = fixturePack = packObject.AddComponent<WorldBoosterPack>();
        pack.Initialize(null, preRolledContents: CardCatalog.All.Take(CardDimensions.CardsPerBoosterPack).ToArray());
        StartSession(Guest);
        Assert.That(session.PickupPack(pack), Is.True);
        var held = delivered.Single();
        held.Action = CoopCardAction.Held;
        held.Actor = Guest;
        held.Revision = 1;
        session.Receive(Host, held);
        Assert.That(hand.TryOpenHeldPackFromInput(), Is.True);
        var reply = delivered.Single(m => m.Action == CoopCardAction.OpenPack);
        reply.Action = CoopCardAction.Denied;
        reply.Revision = 2;
        session.Receive(Host, reply);
        Assert.That(hand.IsOpeningPack, Is.False);
        reply.Action = CoopCardAction.PackClaimed;
        reply.Actor = Guest;
        session.Receive(Host, reply);
        Assert.That(hand.IsOpeningPack, Is.True);
    }

    [Test]
    public void DisconnectReleasesAnUnopenedPackForTheRemainingPlayer()
    {
        var packObject = new GameObject("Held pack");
        packObject.transform.SetParent(root.transform, false);
        var pack = fixturePack = packObject.AddComponent<WorldBoosterPack>();
        pack.Initialize(null, preRolledContents: CardCatalog.All.Take(CardDimensions.CardsPerBoosterPack).ToArray());
        StartSession(Guest);
        Assert.That(session.PickupPack(pack), Is.True);
        var request = delivered.Single();
        session.Dispose();
        var roster = new List<ulong> { Host, Guest };
        session = new CoopShopCards(Host, Host, roster, (_, m) => delivered.Add(m), _ => root.transform);
        session.Receive(Guest, request);
        Assert.That(pack.IsHeld, Is.True);
        roster.Remove(Guest);
        session.Tick();
        Assert.That(pack.IsInHand, Is.False);
        Assert.That(session.PickupPack(pack), Is.True);
        Assert.That(hand.HasHeldPack, Is.True);
    }

    [Test]
    public void SavedShelfAndHandRestoreIntoAFreshShopAndReplayForALateGuest()
    {
        string path = System.IO.Path.Combine(Application.temporaryCachePath, Guid.NewGuid() + ".json");
        string firstId = PersistentId.Resolve(first), secondId = PersistentId.Resolve(second);
        try
        {
            StartSession(Host);
            Assert.That(session.UseCheckpoint(path, false), Is.True);
            Assert.That(hand.TryPickup(first), Is.True);
            Assert.That(session.RequestShelf(first, slot), Is.True);
            Assert.That(hand.TryPickup(second), Is.True);
            Assert.That(session.FlushCheckpoint(), Is.True);
            TearDown();
            SetUp();
            PersistentId.GetOrCreate(first.gameObject).AssignExisting(firstId);
            PersistentId.GetOrCreate(second.gameObject).AssignExisting(secondId);
            StartSession(Host);
            Assert.That(session.UseCheckpoint(path, true), Is.True);
            Assert.That(slot.OccupiedCard, Is.SameAs(first));
            Assert.That(second.IsInHand, Is.False);
            session.Receive(Guest, new CoopCardMessage { Action = CoopCardAction.Sync });
            session.Tick();
            Assert.That(delivered.Count(m => m.Action == CoopCardAction.OnShelf), Is.EqualTo(1));
            Assert.That(delivered.Count(m => m.Action == CoopCardAction.Ground && m.Card == Id(second)), Is.EqualTo(1));
        }
        finally
        {
            session?.Dispose();
            session = null;
            System.IO.File.Delete(path);
            System.IO.File.Delete(path + ".bak");
        }
    }

    [Test]
    public void ReloadingDuringAThrowRestoresVelocityAndSpin()
    {
        string path = System.IO.Path.Combine(Application.temporaryCachePath, Guid.NewGuid() + ".json");
        string id = PersistentId.Resolve(first);
        var speed = new Vector3(1, 2, 3);
        var spin = new Vector3(3, -2, 1);
        try
        {
            StartSession(Host);
            Assert.That(session.UseCheckpoint(path, false), Is.True);
            Assert.That(hand.TryPickup(first), Is.True);
            Assert.That(session.RequestThrow(first, speed), Is.True);
            var body = first.GetComponent<Rigidbody>();
            body.linearVelocity = speed;
            body.angularVelocity = spin;
            Assert.That(session.FlushCheckpoint(), Is.True);
            TearDown();
            SetUp();
            PersistentId.GetOrCreate(first.gameObject).AssignExisting(id);
            StartSession(Host);
            Assert.That(session.UseCheckpoint(path, true), Is.True);
            body = first.GetComponent<Rigidbody>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.linearVelocity, Is.EqualTo(speed));
            Assert.That(body.angularVelocity, Is.EqualTo(spin));
        }
        finally
        {
            session?.Dispose();
            session = null;
            System.IO.File.Delete(path);
            System.IO.File.Delete(path + ".bak");
        }
    }

    [TestCase(PsaCardSet.English)]
    [TestCase(PsaCardSet.Japanese)]
    public void PsaSlabKeepsItsPrintWhenARegularShelfRejectsIt(PsaCardSet set)
    {
        var slab = CardFactory.CreateWorldPsaCard(Vector3.up, Quaternion.identity, 8, 2, cardSet: set);
        try
        {
            StartSession(Host);
            Assert.That(hand.TryPickup(slab), Is.True);
            Assert.That(session.RequestShelf(slab, slot), Is.False);
            Assert.That(hand.SelectedHeldCard, Is.SameAs(slab));
            Assert.That(slab.IsJapanesePsa, Is.EqualTo(set == PsaCardSet.Japanese));
            Assert.That(slab.PsaSlotNumber, Is.EqualTo(8));
            Assert.That(slab.PsaVariantIndex, Is.EqualTo(2));
        }
        finally
        {
            session?.Dispose();
            session = null;
            UnityEngine.Object.DestroyImmediate(slab.gameObject);
        }
    }

    [Test]
    public void DelayedFlightSamplesCannotMoveALandedCard()
    {
        StartSession(Guest);
        var packet = new CoopCardMessage { Action = CoopCardAction.Flight, Card = Id(first), Revision = 1,
            Rotation = Quaternion.identity, Position = Vector3.up };
        session.Receive(Host, packet);
        Assert.That(first.HasActivePhysics, Is.False);
        packet.Action = CoopCardAction.Motion;
        packet.Position = Vector3.one;
        packet.Request = 2;
        session.Receive(Host, packet);
        var landing = new CoopCardMessage { Action = CoopCardAction.Ground, Card = packet.Card, Revision = 2,
            Position = new Vector3(2, .5f, 3), Rotation = Quaternion.Euler(18, 45, 173) };
        session.Receive(Host, landing);
        session.Receive(Host, packet);
        session.Tick();
        Assert.That(Vector3.Distance(first.transform.position, landing.Position), Is.LessThan(.0001f));
        Assert.That(Quaternion.Angle(first.transform.rotation, landing.Rotation), Is.LessThan(.05f));
    }

    [Test]
    public void VoiceUsesRoomMembershipAndSpeakerIdentity()
    {
        var peers = new List<ulong> { Host, Guest, 33, 44 };
        Assert.That(CoopVoiceRoute.Accept(7, 7, Host, Host, Guest, Guest, peers), Is.True);
        Assert.That(CoopVoiceRoute.Accept(7, 7, 33, Host, Host, Guest, peers), Is.True);
        Assert.That(CoopVoiceRoute.Accept(7, 7, Host, Host, Guest, 33, peers), Is.False);
        Assert.That(CoopVoiceRoute.Accept(7, 7, 33, Host, Guest, Guest, peers), Is.False);
        Assert.That(CoopVoiceRoute.Accept(7, 8, 33, Host, Host, Guest, peers), Is.False);
        peers.Remove(Guest);
        Assert.That(CoopVoiceRoute.Accept(7, 7, 33, Host, Host, Guest, peers), Is.False);
    }

    [Test]
    public void VoiceBuffersJitterAndProducesSilenceAfterTheSpeakerStops()
    {
        var audio = new CoopVoiceBuffer();
        var pcm = new byte[2400];
        for (int i = 1; i < pcm.Length; i += 2) pcm[i] = 64;
        var output = new float[120];
        audio.Write(pcm, 240);
        audio.Read(output);
        Assert.That(output.All(s => s == 0f), Is.True);
        audio.Write(pcm, 2160);
        for (int chunk = 0; chunk < 10; chunk++)
        {
            audio.Read(output);
            Assert.That(output.All(s => s == .5f), Is.True);
        }
        audio.Read(output);
        Assert.That(output.All(s => s == 0f), Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        session?.Dispose();
        if (first) UnityEngine.Object.DestroyImmediate(first.gameObject);
        if (second) UnityEngine.Object.DestroyImmediate(second.gameObject);
        if (fixturePack) UnityEngine.Object.DestroyImmediate(fixturePack.gameObject);
        if (root) UnityEngine.Object.DestroyImmediate(root);
    }
}
