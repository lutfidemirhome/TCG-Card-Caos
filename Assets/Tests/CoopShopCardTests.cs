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

    void StartSession(ulong local, bool synchronized = true)
    {
        session = new CoopShopCards(local, Host, new[] { Host, Guest },
            (_, message) => delivered.Add(message), _ => root.transform);
        if (local == Guest && synchronized)
            session.Receive(Host, new CoopCardMessage { Action = CoopCardAction.Progress });
    }

    [Test]
    public void LateGuestWaitsForMissingCardChangesBeforePickingUp()
    {
        StartSession(Guest, synchronized: false);
        var progress = new CoopCardMessage { Action = CoopCardAction.Progress, Revision = 2, Position = Vector3.one };
        session.Receive(Host, progress);
        Assert.That(session.RequestPickup(first), Is.False);
        var placement = new CoopCardMessage { Action = CoopCardAction.Ground, Card = Id(first), Revision = 2,
            Position = Vector3.up, Rotation = Quaternion.identity };
        session.Receive(Host, placement);
        Assert.That(session.ReadyForInteraction, Is.False);
        var earlier = placement;
        earlier.Revision = 1;
        earlier.Card = Id(second);
        session.Receive(Host, earlier);
        Assert.That(session.ReadyForInteraction, Is.False);
        session.Receive(Host, placement);
        Assert.That(session.ReadyForInteraction, Is.True);
        Assert.That(session.TryGetProgress(out var received) && received.cardsPlaced == 1, Is.True);
        Assert.That(session.RequestPickup(first), Is.True);
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
    public void RefreshingRegularCardArtRepairsAnEmptyMaterialArrayAfterScriptReload()
    {
        StartSession(Host);
        Assert.That(session.RequestPickup(first), Is.True);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var cache = typeof(WorldCard).GetField("_singlePassMaterials", flags);
        cache.SetValue(first, Array.Empty<Material>());
        var refresh = typeof(WorldCard).GetMethod("ApplyCardVisualTextureQuality", flags);
        refresh.Invoke(first, null);
        var materials = (Material[])cache.GetValue(first);
        Assert.That(materials.Length, Is.EqualTo(1));
        Assert.That(materials[0], Is.Not.Null);
    }

    static WorldMinorSkillKey CreateKey(GameObject owner)
    {
        owner.SetActive(false);
        owner.transform.position = Vector3.up;
        owner.AddComponent<BoxCollider>().size = Vector3.one * .15f;
        var visual = new GameObject("Key model").transform;
        visual.SetParent(owner.transform, false);
        var key = owner.AddComponent<WorldMinorSkillKey>();
        key.Configure(0, visual);
        owner.SetActive(true);
        return key;
    }

    [TestCase(Host)]
    [TestCase(Guest)]
    public void InteractTakesTheKeyInFrontOfACardWithoutReservingACardSlot(ulong local)
    {
        var keyObject = new GameObject("Skill key");
        var cameraObject = new GameObject("Key view", typeof(Camera));
        var inShop = typeof(CoopSession).GetProperty(nameof(CoopSession.InShop));
        var ready = typeof(CardInstancedRenderManager).GetProperty(nameof(CardInstancedRenderManager.IsGameplayReady));
        bool previousShop = CoopSession.InShop, previousReady = CardInstancedRenderManager.IsGameplayReady;
        bool skillsReady = MinorSkillProgress.Ready;
        var skills = MinorSkillProgress.Capture();
        try
        {
            MinorSkillProgress.Restore(null);
            var key = CreateKey(keyObject);
            inShop.SetValue(null, true);
            ready.SetValue(null, true);
            cameraObject.transform.SetParent(hand.transform, false);
            cameraObject.transform.position = key.transform.position + Vector3.up;
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            first.transform.position = key.transform.position + Vector3.down * .25f;
            CardGroundStack.Track(first);
            var interaction = cameraObject.AddComponent<InteractionController>();
            var update = (Action<bool, bool, bool>)Delegate.CreateDelegate(typeof(Action<bool, bool, bool>), interaction,
                typeof(InteractionController).GetMethod("UpdateInteraction", BindingFlags.Instance | BindingFlags.NonPublic));
            StartSession(local);
            Physics.SyncTransforms();
            update(true, true, false);
            if (local == Guest)
            {
                var take = delivered.Single(m => m.Action == CoopCardAction.TakeKey);
                Assert.That(session.RequestKeyPickup(key), Is.False);
                Assert.That(CoopShopCards.ReservedSlots, Is.Zero);
                Assert.That(key.IsHeld, Is.False);
                take.Action = CoopCardAction.KeyHeld;
                take.Revision = 1;
                take.Actor = local;
                session.Receive(Host, take);
            }
            Assert.That(hand.SelectedHeldKey, Is.SameAs(key));
            Assert.That(key.IsHeld && MinorSkillProgress.HasKey(0), Is.True);
            Assert.That(first.IsHeld, Is.False);
        }
        finally
        {
            inShop.SetValue(null, previousShop);
            ready.SetValue(null, previousReady);
            CardInteractionFocus.ClearFocus();
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(keyObject);
            if (skillsReady) MinorSkillProgress.Restore(skills); else MinorSkillProgress.PrepareForSceneReload();
        }
    }

    [Test]
    public void DepartingPlayerDropsKeyAndLatestUnlockSurvivesAnInFlightCheckpoint()
    {
        string path = System.IO.Path.Combine(Application.temporaryCachePath, Guid.NewGuid() + ".json");
        var keyObject = new GameObject("Shared key");
        var chestObject = new GameObject("Matching chest");
        var skills = MinorSkillProgress.Capture();
        bool skillsReady = MinorSkillProgress.Ready;
        try
        {
            MinorSkillProgress.Restore(null);
            var key = CreateKey(keyObject);
            var chest = chestObject.AddComponent<WorldMinorSkillChest>();
            chest.Configure(0, null);
            StartSession(Guest);
            Assert.That(session.RequestKeyPickup(key), Is.True);
            var take = delivered.Single(m => m.Action == CoopCardAction.TakeKey);
            session.Dispose();
            var members = new List<ulong> { Host, Guest };
            session = new CoopShopCards(Host, Host, members, (_, m) => delivered.Add(m), _ => root.transform);
            Assert.That(session.UseCheckpoint(path, false), Is.True);
            session.Receive(Guest, take);
            Assert.That(keyObject.GetComponent<Collider>().enabled, Is.False);
            Assert.That(session.RequestKeyPickup(key) || session.RequestKeyUse(chest), Is.False);
            session.Tick();
            members.Remove(Guest);
            session.Tick();
            Assert.That(keyObject.GetComponent<Collider>().enabled, Is.True);
            Assert.That(session.RequestKeyPickup(key), Is.True);
            Assert.That(session.RequestKeyDrop(key), Is.True);
            Assert.That(hand.SelectedHeldKey, Is.Null);
            Assert.That(session.RequestKeyPickup(key), Is.True);
            chestObject.transform.position = Vector3.forward * 100f;
            Assert.That(session.RequestKeyUse(chest), Is.False);
            chestObject.transform.position = root.transform.position;
            Assert.That(session.RequestKeyUse(chest), Is.True);
            Assert.That(MinorSkillProgress.IsUnlocked(0) && !MinorSkillProgress.HasKey(0), Is.True);
            session.Dispose();
            MinorSkillProgress.Restore(null);
            StartSession(Host);
            Assert.That(session.UseCheckpoint(path, true), Is.True);
            Assert.That(MinorSkillProgress.IsUnlocked(0), Is.True);
            Assert.That(session.RequestKeyPickup(key), Is.False);
            delivered.Clear();
            session.Receive(Guest, new CoopCardMessage { Action = CoopCardAction.Sync, Request = 1 });
            session.Tick();
            var replay = delivered.ToArray();
            session.Dispose();
            MinorSkillProgress.Restore(null);
            StartSession(Guest, synchronized: false);
            int notices = 0;
            void CountNotice(int index) => notices++;
            MinorSkillProgress.Unlocked += CountNotice;
            try
            {
                foreach (var message in replay) session.Receive(Host, message);
                Assert.That(session.ReadyForInteraction && MinorSkillProgress.IsUnlocked(0), Is.True);
                Assert.That(notices, Is.Zero);
                Assert.That(keyObject.GetComponent<Collider>().enabled, Is.False);
            }
            finally { MinorSkillProgress.Unlocked -= CountNotice; }
        }
        finally
        {
            session?.Dispose();
            session = null;
            UnityEngine.Object.DestroyImmediate(keyObject);
            UnityEngine.Object.DestroyImmediate(chestObject);
            if (skillsReady) MinorSkillProgress.Restore(skills); else MinorSkillProgress.PrepareForSceneReload();
            System.IO.File.Delete(path);
            System.IO.File.Delete(path + ".bak");
        }
    }

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
    public void PickupDuringLandingSucceedsOnceAndDoesNotTransferTheNextOwnersCard()
    {
        StartSession(Host);
        var id = Id(first);
        session.Receive(Guest, new CoopCardMessage { Action = CoopCardAction.Sync });
        Assert.That(session.RequestPickup(first), Is.True);
        Assert.That(session.RequestThrow(first, Vector3.up), Is.True);
        session.Tick();
        var flight = delivered.Single(m => m.Card == id && m.Action == CoopCardAction.Flight);
        first.GetComponent<Rigidbody>().Sleep();
        // A fixed step normally emits the landing. Commit the pose now to control packet order.
        var commit = typeof(CoopShopCards).GetMethod("Commit", BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(CoopCardMessage) }, null);
        commit.Invoke(session, new object[] { new CoopCardMessage { Action = CoopCardAction.Ground, Card = id,
            Position = Vector3.up, Rotation = Quaternion.identity } });
        var take = new CoopCardMessage { Action = CoopCardAction.Take, Card = id, Revision = flight.Revision, Request = 23 };
        session.Receive(Guest, take);
        session.Tick();
        Assert.That(delivered.Count(m => m.Action == CoopCardAction.Held && m.Actor == Guest), Is.EqualTo(1));
        session.Receive(Guest, take);
        Assert.That(delivered.Last().Action, Is.EqualTo(CoopCardAction.Denied));
    }

    [Test]
    public void SlowGuestAcknowledgesBoundedHistoryAndCanRequestMissingChanges()
    {
        StartSession(Host);
        var id = Id(first);
        for (uint i = 0; i < 160; i += 2)
        {
            session.Receive(Guest, new CoopCardMessage { Action = CoopCardAction.Take, Card = id, Revision = i, Request = i + 1 });
            session.Receive(Guest, new CoopCardMessage { Action = CoopCardAction.Return, Card = id, Revision = i + 1, Request = i + 2 });
        }
        var sync = new CoopCardMessage { Action = CoopCardAction.Sync, Card = Guid.NewGuid() };
        for (int tick = 0; tick < 16; tick++) { session.Receive(Guest, sync); session.Tick(); }
        uint[] Changes() => delivered.Where(m => m.Action == CoopCardAction.Held || m.Action == CoopCardAction.Ground)
            .Select(m => m.Revision).ToArray();
        Assert.That(Changes(), Is.EqualTo(Enumerable.Range(1, 128)));
        delivered.Clear();
        sync.Revision = 100;
        session.Receive(Guest, sync);
        for (int tick = 0; tick < 4; tick++) session.Tick();
        Assert.That(Changes(), Is.EqualTo(Enumerable.Range(129, 32)));
        delivered.Clear();
        sync.Revision = 145;
        sync.Request = 2;
        session.Receive(Guest, sync);
        session.Tick();
        Assert.That(Changes(), Is.EqualTo(Enumerable.Range(146, 15)));
        delivered.Clear();
        sync.Card = Guid.NewGuid(); sync.Revision = sync.Request = 0;
        session.Receive(Guest, sync);
        session.Tick();
        Assert.That(Changes(), Is.EqualTo(Enumerable.Range(1, 16)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CorruptPrimaryRestoresAndPreservesTheLastGoodBackup(bool slabOnRegularShelf)
    {
        string path = System.IO.Path.Combine(Application.temporaryCachePath, Guid.NewGuid() + ".json");
        WorldCard slab = null;
        try
        {
            StartSession(Host);
            Assert.That(session.UseCheckpoint(path, false), Is.True);
            Assert.That(session.RequestPickup(first), Is.True);
            Assert.That(session.RequestShelf(first, slot), Is.True);
            Assert.That(session.FlushCheckpoint(), Is.True);
            string good = System.IO.File.ReadAllText(path);
            System.IO.File.WriteAllText(path + ".bak", good);
            string firstId = PersistentId.Resolve(first), secondId = PersistentId.Resolve(second);
            TearDown();
            SetUp();
            PersistentId.GetOrCreate(first.gameObject).AssignExisting(firstId);
            PersistentId.GetOrCreate(second.gameObject).AssignExisting(secondId);
            slab = CardFactory.CreateWorldPsaCard(Vector3.up, Quaternion.identity, 8, 2);
            string invalid = slabOnRegularShelf ? good.Replace(Id(first).ToString("N"), Id(slab).ToString("N")) : "{\"version\":-1}";
            Assert.That(invalid, Is.Not.EqualTo(good));
            System.IO.File.WriteAllText(path, invalid);
            StartSession(Host);
            Assert.That(session.UseCheckpoint(path, true), Is.True);
            Assert.That(slot.OccupiedCard, Is.SameAs(first));
            Assert.That(first.IsFlyingToShelf, Is.False);
            Assert.That(System.IO.File.ReadAllText(path + ".bak"), Is.EqualTo(good));
            Assert.That(System.IO.File.ReadAllText(path), Does.Contain(firstId.Replace("-", string.Empty)));
        }
        finally
        {
            session?.Dispose(); session = null;
            if (slab) UnityEngine.Object.DestroyImmediate(slab.gameObject);
            System.IO.File.Delete(path);
            System.IO.File.Delete(path + ".bak");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DisconnectDuringPackRevealKeepsEitherThePackOrAllFiveProducts(bool collected)
    {
        var packObject = new GameObject("Interrupted pack");
        packObject.transform.SetParent(root.transform, false);
        var pack = fixturePack = packObject.AddComponent<WorldBoosterPack>();
        pack.Initialize(null, preRolledContents: CardCatalog.All.Take(CardDimensions.CardsPerBoosterPack).ToArray());
        StartSession(Guest);
        Assert.That(session.PickupPack(pack), Is.True);
        var take = delivered.Single();
        session.Dispose();
        delivered.Clear();
        var roster = new List<ulong> { Host, Guest };
        session = new CoopShopCards(Host, Host, roster, (_, m) => delivered.Add(m), _ => root.transform);
        session.Receive(Guest, new CoopCardMessage { Action = CoopCardAction.Sync });
        session.Receive(Guest, take);
        session.Tick();
        var held = delivered.Single(m => m.Action == CoopCardAction.Held);
        session.Receive(Guest, new CoopCardMessage { Action = CoopCardAction.OpenPack, Card = held.Card, Revision = held.Revision, Request = 2 });
        session.Tick();
        Assert.That(delivered.Count(m => m.Action == CoopCardAction.PackClaimed), Is.EqualTo(1));
        var layout = typeof(CoopShopCards).GetField("layout", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
        var ids = (Guid[])layout.GetType().GetMethod("Products").Invoke(layout, new object[] { held.Card });
        var claim = new CoopCardMessage { Action = CoopCardAction.Collect, Card = ids[0], Request = 3 };
        if (collected) { session.Receive(Guest, claim); session.Receive(Guest, claim); }
        roster.Remove(Guest);
        session.Tick();
        session.Receive(Guest, claim);
        var products = UnityEngine.Object.FindObjectsByType<WorldCard>().Where(c => ids.Contains(Id(c))).ToArray();
        try
        {
            Assert.That(products.Length, Is.EqualTo(collected ? CardDimensions.CardsPerBoosterPack : 0));
            Assert.That(products.All(c => !c.IsInHand), Is.True);
            Assert.That(pack && pack.gameObject.activeSelf && !pack.IsInHand, Is.EqualTo(!collected));
        }
        finally { foreach (var item in products) UnityEngine.Object.DestroyImmediate(item.gameObject); }
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
