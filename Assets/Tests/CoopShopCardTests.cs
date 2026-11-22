using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class CoopShopCardTests
{
    const ulong Host = 11, Guest = 22;
    readonly List<CoopCardMessage> delivered = new List<CoopCardMessage>();
    GameObject root;
    WorldCard first, second;
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
        var art = CardCatalog.All[0];
        first = CardFactory.CreateWorldCard(Vector3.up, Quaternion.identity, art, 0);
        second = CardFactory.CreateWorldCard(Vector3.up + Vector3.right, Quaternion.identity, art, 0);
        root.AddComponent<CardShelf>();
        var marker = new GameObject("Fixture slot");
        marker.transform.SetParent(root.transform, false);
        slot = marker.AddComponent<CardShelfSlot>();
    }

    void StartSession(ulong local)
    {
        session = new CoopShopCards(local, Host, new[] { Host, Guest },
            (_, message) => delivered.Add(message), _ => root.transform);
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

        Assert.That(session.RequestShelf(first, slot), Is.True);
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
        var pack = packObject.AddComponent<WorldBoosterPack>();
        pack.Initialize(null, preRolledContents: CardCatalog.All.Take(CardDimensions.CardsPerBoosterPack).ToArray());
        StartSession(Guest);
        Assert.That(session.OpenPack(pack, hand), Is.True);
        var request = delivered.Single();
        session.Dispose();
        delivered.Clear();

        StartSession(Host);
        session.Receive(Guest, new CoopCardMessage { Action = CoopCardAction.Sync });
        session.Receive(Guest, request);
        session.Tick();
        var claim = delivered.Single(m => m.Action == CoopCardAction.PackClaimed);
        Assert.That(claim.Actor, Is.EqualTo(Guest));
        Assert.That(session.OpenPack(pack, hand), Is.False);
        var cancel = new CoopCardMessage { Action = CoopCardAction.CancelPack, Card = claim.Card,
            Revision = claim.Revision, Request = 2 };
        session.Receive(Host, cancel);
        Assert.That(session.OpenPack(pack, hand), Is.False);
        session.Receive(Guest, cancel);
        session.Tick();
        Assert.That(delivered.Count(m => m.Action == CoopCardAction.PackReleased), Is.EqualTo(1));
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

    [TearDown]
    public void TearDown()
    {
        session?.Dispose();
        if (first) UnityEngine.Object.DestroyImmediate(first.gameObject);
        if (second) UnityEngine.Object.DestroyImmediate(second.gameObject);
        if (root) UnityEngine.Object.DestroyImmediate(root);
    }
}
