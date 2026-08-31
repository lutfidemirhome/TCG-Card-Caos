using System.Collections.Generic;
using Grabbit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Spawns the full catalog into Mix_All. Grabbit Fall does the physics placement.
/// </summary>
public class PhysicsCardLevelBuilderWindow : EditorWindow
{
    const int SpawnOverlapTries = 12;
    const int MixAllPhysicsBatchIndex = 5000;
    const int MixShuffleSeed = 20260825;
    const float MixAllVolumeSide = 10f;
    const float MixAllVolumeHeight = 2.6f;
    const string AuthoringFloorName = "GrabbitAuthoringFloor";
    const float AuthoringFloorThickness = 0.6f;
    const float FloorGuardBelowMargin = 0.25f;
    const float MinSpawnClearance = 0.35f;
    const float AirborneMinClearance = 0.22f;

    Vector2 _scroll;
    MixAllPlan _cachedMixAllPlan;
    bool _hasMixAllPlan;
    List<PhysicsLevelItem> _guardItems;
    PhysicsCardSpawnVolume _guardVolume;
    GameObject _authoringFloor;
    bool _floorGuardActive;
    bool _sawGrabbitActive;
    bool _promotingSelection;

    static string ApplyTenPercentFaceDownOnExistingCards()
    {
        var faceUp = new List<WorldCard>(512);
        var faceDown = new List<WorldCard>(512);
        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            WorldCard card = cards[i];
            if (card == null || card.GetComponent<PhysicsLevelItem>() == null)
                continue;
            if (card.GetComponentInParent<CardShelfSlot>() != null)
                continue;
            if (card.GetComponentInParent<PsaCabinetSlot>() != null)
                continue;

            if (card.IsGroundFaceDown)
                faceDown.Add(card);
            else
                faceUp.Add(card);
        }

        int total = faceUp.Count + faceDown.Count;
        if (total == 0)
            return "no authored ground cards to flip.";

        int targetDown = Mathf.Clamp(
            Mathf.RoundToInt(total * CardScatterUtility.GroundFaceDownRatio),
            0,
            total);

        Random.State previous = Random.state;
        Random.InitState(MixShuffleSeed + 17);
        Shuffle(faceUp);
        Shuffle(faceDown);
        Random.state = previous;

        int flippedDown = 0;
        int flippedUp = 0;
        while (faceDown.Count > targetDown && faceDown.Count > 0)
        {
            WorldCard card = faceDown[faceDown.Count - 1];
            faceDown.RemoveAt(faceDown.Count - 1);
            FlipAuthoredCardInPlace(card);
            flippedUp++;
        }

        while (faceDown.Count < targetDown && faceUp.Count > 0)
        {
            WorldCard card = faceUp[faceUp.Count - 1];
            faceUp.RemoveAt(faceUp.Count - 1);
            FlipAuthoredCardInPlace(card);
            faceDown.Add(card);
            flippedDown++;
        }

        return "face-down set to "
            + targetDown + "/" + total
            + " (~10%). flipped down=" + flippedDown
            + " flipped up=" + flippedUp;
    }

    static void FlipAuthoredCardInPlace(WorldCard card)
    {
        if (card == null)
            return;

        Undo.RecordObject(card.transform, "Apply 10% Face-Down");
        card.transform.Rotate(180f, 0f, 0f, Space.Self);
        card.SetGroundShowsBack(false);
        EditorUtility.SetDirty(card.transform);
        EditorUtility.SetDirty(card);
    }

    [MenuItem("TCG Card Chaos/Fix Pink Card Art")]
    public static void FixPinkCardArtInScene()
    {
        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < cards.Length; i++)
        {
            WorldCard card = cards[i];
            if (card == null || card.GetComponent<PhysicsLevelItem>() == null)
                continue;

            Undo.RecordObject(card, "Fix Pink Card Art");
            card.RefreshAuthoredVisual();
            EditorUtility.SetDirty(card);
            count++;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("TCG Card Chaos: refreshed art on " + count + " authored cards.");
    }

    [MenuItem("TCG Card Chaos/Card Physics Level Builder")]
    [MenuItem("Window/TCG Card Chaos/Card Physics Level Builder")]
    public static void Open()
    {
        PhysicsCardLevelBuilderWindow window = GetWindow<PhysicsCardLevelBuilderWindow>(
            false,
            "Card Physics Level Builder",
            true);
        window.minSize = new Vector2(380f, 520f);
        Rect main = EditorGUIUtility.GetMainWindowPosition();
        Rect pos = window.position;
        bool unusable = pos.width < 80f || pos.height < 80f
            || float.IsNaN(pos.x) || float.IsNaN(pos.y)
            || pos.xMax < main.x - 80f
            || pos.yMax < main.y - 80f
            || pos.x > main.xMax + 80f
            || pos.y > main.yMax + 80f;
        if (unusable)
            window.position = new Rect(main.x + 36f, main.y + 72f, 440f, 740f);
        window.Show();
        window.Focus();
        Debug.Log("Card Physics Level Builder opened. Look for the tab named Card Physics Level Builder, or Window → TCG Card Chaos.");
    }

    void OnDisable()
    {
        StopFloorGuard();
        DestroyAuthoringFloor();
    }

    void OnSelectionChange()
    {
        if (!_promotingSelection)
            PromoteSelectionToCardRoots();
        Repaint();
    }

    void PromoteSelectionToCardRoots()
    {
        List<PhysicsLevelItem> valid = CollectValidSelectedItems();
        if (valid.Count == 0)
            return;

        GameObject[] current = Selection.gameObjects;
        if (current != null && current.Length == valid.Count)
        {
            bool alreadyRoots = true;
            for (int i = 0; i < valid.Count; i++)
            {
                if (current[i] != valid[i].gameObject)
                {
                    alreadyRoots = false;
                    break;
                }
            }

            if (alreadyRoots)
                return;
        }

        _promotingSelection = true;
        SelectItems(valid);
        _promotingSelection = false;
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "Mix All spawns every unique catalog card, every PSA, and 100 filled packs. "
            + "Move the blue square over the play area first. Drop with Grabbit Fall — hold Left Shift, then Bake Mix All and save the Scene.",
            MessageType.Info);

        PhysicsLevelLayout layout = EnsureLayout(createIfMissing: false);
        if (layout == null)
        {
            if (GUILayout.Button("Create Physics_Card_Level Hierarchy", GUILayout.Height(28)))
            {
                layout = EnsureLayout(createIfMissing: true);
                MarkDirty(layout);
            }

            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUI.BeginChangeCheck();
        DrawTotals(layout);
        EditorGUILayout.Space(8);
        DrawMixAll(layout);
        EditorGUILayout.Space(10);
        DrawSelectedCards();
        EditorGUILayout.Space(10);
        DrawSpawnSettings(layout);
        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(layout);

        EditorGUILayout.EndScrollView();
    }

    void DrawTotals(PhysicsLevelLayout layout)
    {
        Transform mixAll = FindMixAll(layout);
        int mixAllItems = mixAll != null ? mixAll.childCount : 0;
        int leftoverDemo = CountArea(PhysicsLevelItem.AreaKind.Demo, -1);
        int leftoverMix = CountLegacyMixItems(layout);
        EditorGUILayout.LabelField("Live counts", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Mix All in scene", mixAllItems.ToString());
        EditorGUILayout.LabelField("Leftover Demo items", leftoverDemo.ToString());
        EditorGUILayout.LabelField("Leftover Mix 1-10 items", leftoverMix.ToString());
    }

    void DrawMixAll(PhysicsLevelLayout layout)
    {
        MixAllPlan plan = GetCachedMixAllPlan();
        Transform folder = FindMixAll(layout);
        int inScene = folder != null ? folder.childCount : 0;

        EditorGUILayout.LabelField("Mix All", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.HelpBox(
            "Demo ve Mix 1–10 yok. Tek kare: Main Spawn Volume. "
            + "Mix All → tüm unique kartlar yerde (eksik seri yok), tüm PSA, "
            + PhysicsLevelLayout.MixPackCount
            + " pack (her biri 5 kart, unique kart çalmaz). "
            + "Yerdeki kartların %10'u arkası dönük spawn olur. "
            + "Kareyi oyun alanına taşı → Mix All → Grabbit Fall → Shift → Bake → sahneyi kaydet.",
            MessageType.Info);

        layout.MainVolume = ObjectFieldVolume("Main Spawn Volume (kare)", layout.MainVolume);
        EditorGUILayout.LabelField("Floor cards", plan.FloorCards.Count.ToString());
        EditorGUILayout.LabelField("PSA cards", plan.PsaItems.Count.ToString());
        EditorGUILayout.LabelField("Packs", PhysicsLevelLayout.MixPackCount.ToString());
        EditorGUILayout.LabelField("In scene", inScene.ToString());

        if (GUILayout.Button("Make Volume Square", GUILayout.Height(24)))
            MakeMainVolumeSquare(layout);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Mix All", GUILayout.Height(32)))
            CreateMixAll(layout);
        if (GUILayout.Button("Delete Mix + Demo", GUILayout.Height(32)))
        {
            if (EditorUtility.DisplayDialog(
                    "Delete Mix + Demo",
                    "Demo_Cards, Mix 1–10 ve Mix_All silinecek. Devam?",
                    "Sil",
                    "İptal"))
                DeleteAllSceneCards();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginDisabledGroup(folder == null || folder.childCount == 0);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select Mix All"))
            SelectChildren(folder);
        if (GUILayout.Button("Grabbit Fall"))
            ScheduleDrop(layout, layout.MainVolume, folder);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Re-drop Packs"))
            ScheduleDropPacks(folder);
        if (GUILayout.Button("Re-drop Upright"))
            ScheduleDropUpright(folder);
        if (GUILayout.Button("Bake Mix All"))
            BakeFolder(folder, "Bake Mix All");
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Settle Airborne (kart + pack + PSA)", GUILayout.Height(28)))
            ScheduleSettleAirborne(folder);
        EditorGUILayout.HelpBox(
            "Settle Airborne: sadece dik duran veya yerden yüksekte kalan kart / PSA / pack düşer. "
            + "Yerde düzgün yatanlara dokunmaz. Grabbit açılınca Scene view'de Left Shift basılı tut, "
            + "otursun, sonra Bake Mix All → sahneyi kaydet.",
            MessageType.None);
        if (GUILayout.Button("Apply 10% Face-Down (no respawn)"))
        {
            string summary = ApplyTenPercentFaceDownOnExistingCards();
            Debug.Log("TCG Card Chaos: " + summary);
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();
    }

    void DrawSelectedCards()
    {
        List<PhysicsLevelItem> valid = CollectValidSelectedItems();
        int totalSelected = Selection.objects != null ? Selection.objects.Length : 0;

        EditorGUILayout.LabelField("Selected Cards", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Selected Cards: " + valid.Count);
        if (totalSelected > valid.Count)
            EditorGUILayout.LabelField(valid.Count + " valid cards selected.");

        EditorGUILayout.HelpBox(
            "Select one or more badly placed cards in the Scene view, then Grabbit Fall Selected. "
            + "Only those cards simulate; baked piles stay kinematic with colliders on. Hold Left Shift, settle, Bake Selected.",
            MessageType.None);

        EditorGUI.BeginDisabledGroup(valid.Count == 0);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Grabbit Fall Selected", GUILayout.Height(24)))
            ScheduleDropSelected(lift: false);
        if (GUILayout.Button("Bake Selected", GUILayout.Height(24)))
            BakeItems(valid, "Bake Selected Cards");
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Wake / Re-Simulate Selected"))
            ScheduleDropSelected(lift: true);
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Fix Pink Card Art In Scene", GUILayout.Height(24)))
            FixPinkCardArtInScene();

        EditorGUILayout.EndVertical();
    }

    void DrawSpawnSettings(PhysicsLevelLayout layout)
    {
        EditorGUILayout.LabelField("Spawn randomness", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        layout.SpawnPadding = EditorGUILayout.Slider("Spawn Padding", layout.SpawnPadding, 0f, 0.4f);
        layout.RotationRandomness = EditorGUILayout.Slider("Rotation Randomness", layout.RotationRandomness, 0f, 1f);
        layout.HeightBias = EditorGUILayout.FloatField("Height Bias", layout.HeightBias);
        layout.MinSpawnSpacing = EditorGUILayout.FloatField("Min Spawn Spacing", layout.MinSpawnSpacing);
        EditorGUILayout.HelpBox(
            "Rotation Randomness is yaw + a small tilt (cards ~22°, packs ~12°). It no longer spins items onto their edge. "
            + "After Mix All: Grabbit Fall → hold Left Shift in the Scene view until piles settle → Bake Mix All → save. "
            + "Limitation Zone is sized to the spawn volume. "
            + "Fall uses a primitive floor collider so thin cards cannot tunnel through the MeshCollider floor.",
            MessageType.None);
        EditorGUILayout.EndVertical();
    }

    PhysicsCardSpawnVolume ObjectFieldVolume(string label, PhysicsCardSpawnVolume current)
    {
        return (PhysicsCardSpawnVolume)EditorGUILayout.ObjectField(label, current, typeof(PhysicsCardSpawnVolume), true);
    }

    [MenuItem("TCG Card Chaos/Clear Mix And Demo Piles")]
    public static void BatchClearMixAndDemoPiles()
    {
        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Clear Mix + Demo",
                "Demo_Cards, Mix 1–10 ve Mix_All silinir. Main_SpawnVolume kare olur. Sahne kaydedilir.",
                "Sil ve kaydet",
                "İptal"))
            return;

        const string scenePath = "Assets/Scenes/MainScene.unity";
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath);
        PhysicsLevelLayout layout = PhysicsLevelLayout.FindExisting();
        if (layout == null)
        {
            Debug.LogError("TCG Card Chaos: Physics_Card_Level missing in MainScene.");
            return;
        }

        MakeMainVolumeSquare(layout);
        DeleteAllSceneCards();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("TCG Card Chaos: all scene cards/packs deleted. Main_SpawnVolume is a 10x10 square.");
    }

    static void MakeMainVolumeSquare(PhysicsLevelLayout layout)
    {
        if (layout == null || layout.MainVolume == null)
            return;

        Transform volume = layout.MainVolume.transform;
        Undo.RecordObject(volume, "Square Mix Volume");
        volume.localRotation = Quaternion.identity;
        volume.localScale = new Vector3(MixAllVolumeSide, MixAllVolumeHeight, MixAllVolumeSide);

        SerializedObject so = new SerializedObject(volume);
        SerializedProperty constrain = so.FindProperty("m_ConstrainProportionsScale");
        if (constrain != null)
        {
            constrain.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        BoxCollider box = layout.MainVolume.Box;
        Undo.RecordObject(box, "Square Mix Volume");
        box.center = Vector3.zero;
        box.size = Vector3.one;
        box.isTrigger = true;
        EditorUtility.SetDirty(volume);
        EditorUtility.SetDirty(box);
        EditorUtility.SetDirty(layout.MainVolume);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static int DeleteAllSceneCards()
    {
        int removed = 0;

        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;
            Object.DestroyImmediate(cards[i].gameObject);
            removed++;
        }

        WorldBoosterPack[] packs = Object.FindObjectsByType<WorldBoosterPack>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < packs.Length; i++)
        {
            if (packs[i] == null)
                continue;
            Object.DestroyImmediate(packs[i].gameObject);
            removed++;
        }

        PhysicsLevelItem[] leftovers = Object.FindObjectsByType<PhysicsLevelItem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < leftovers.Length; i++)
        {
            if (leftovers[i] == null)
                continue;
            Object.DestroyImmediate(leftovers[i].gameObject);
            removed++;
        }

        PhysicsLevelLayout layout = PhysicsLevelLayout.FindExisting();
        if (layout != null)
        {
            DestroyChildrenImmediate(layout.DemoCardsRoot);
            if (layout.MainLevelRoot != null)
            {
                for (int i = layout.MainLevelRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = layout.MainLevelRoot.GetChild(i);
                    if (child == null)
                        continue;
                    if (child.name.StartsWith(PhysicsLevelLayout.BatchPrefix)
                        || child.name.StartsWith(PhysicsLevelLayout.MixBatchPrefix)
                        || child.name == PhysicsLevelLayout.MixAllName)
                        Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return removed;
    }

    static void DestroyChildrenImmediate(Transform folder)
    {
        if (folder == null)
            return;

        for (int i = folder.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(folder.GetChild(i).gameObject);
    }

    void CreateMixAll(PhysicsLevelLayout layout)
    {
        if (layout.MainVolume == null || layout.MainLevelRoot == null)
        {
            EditorUtility.DisplayDialog("Mix All", "Main spawn volume / Main_Level folder missing.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Mix All",
                "Demo_Cards ve Mix 1–10 silinir. Tüm unique kartlar + PSA + "
                + PhysicsLevelLayout.MixPackCount
                + " pack (içleri dolu, yerdeki serilerden kart çalmaz) Mix_All içine spawn olur.",
                "Spawn",
                "İptal"))
            return;

        DeleteAllSceneCards();

        MixAllPlan plan = GetCachedMixAllPlan();
        Transform folder = GetOrCreateMixAllFolder(layout);
        var occupied = new List<Vector3>(plan.FloorCards.Count + plan.PsaItems.Count + PhysicsLevelLayout.MixPackCount);
        HashSet<int> cardFaceDown = CardScatterUtility.PickBackFacingIndices(plan.FloorCards.Count);
        HashSet<int> psaFaceDown = CardScatterUtility.PickBackFacingIndices(plan.PsaItems.Count);
        HashSet<int> packFaceDown = CardScatterUtility.PickBackFacingIndices(PhysicsLevelLayout.MixPackCount);
        int total = plan.FloorCards.Count + plan.PsaItems.Count + PhysicsLevelLayout.MixPackCount;
        int done = 0;

        try
        {
            for (int i = 0; i < plan.FloorCards.Count; i++)
            {
                CardDefinition definition = plan.FloorCards[i];
                if (definition == null)
                    continue;

                bool faceDown = cardFaceDown.Contains(i);
                WorldCard card = CardFactory.CreateWorldCard(
                    NextSpawnPose(layout, layout.MainVolume, occupied, out Quaternion rotation, pack: false, faceDown),
                    rotation,
                    definition,
                    paletteIndex: 0,
                    cardName: "Card_" + definition.DefinitionId);
                FinishCard(card, folder, PhysicsLevelItem.AreaKind.Main, MixAllPhysicsBatchIndex, registerUndo: false);
                card.SetGroundShowsBack(faceDown);
                done++;
                if (done % 25 == 0)
                    EditorUtility.DisplayProgressBar("Mix All", "Spawning cards " + done + " / " + total, done / (float)total);
            }

            for (int i = 0; i < plan.PsaItems.Count; i++)
            {
                MixSpawnItem item = plan.PsaItems[i];
                bool faceDown = psaFaceDown.Contains(i);
                WorldCard card = CardFactory.CreateWorldPsaCard(
                    NextSpawnPose(layout, layout.MainVolume, occupied, out Quaternion rotation, pack: false, faceDown),
                    rotation,
                    item.PsaSlot,
                    item.PsaVariant,
                    cardName: "PSA_" + item.PsaSlot + "_" + item.PsaVariant);
                FinishCard(card, folder, PhysicsLevelItem.AreaKind.Main, MixAllPhysicsBatchIndex, registerUndo: false);
                card.SetGroundShowsBack(faceDown);
                done++;
                if (done % 10 == 0)
                    EditorUtility.DisplayProgressBar("Mix All", "Spawning PSA " + done + " / " + total, done / (float)total);
            }

            BoosterPackDefinition packDefinition = Resources.Load<BoosterPackDefinition>("Cards/BoosterPackDefinition");
            for (int i = 0; i < PhysicsLevelLayout.MixPackCount; i++)
            {
                var contents = new List<CardDefinition>(CardDimensions.CardsPerBoosterPack);
                int start = i * CardDimensions.CardsPerBoosterPack;
                for (int c = 0; c < CardDimensions.CardsPerBoosterPack && start + c < plan.PackCards.Count; c++)
                    contents.Add(plan.PackCards[start + c]);

                int variantIndex = i % PackArtLibrary.PackVariantCount + 1;
                bool faceDown = packFaceDown.Contains(i);
                WorldBoosterPack pack = PackFactory.CreateWorldPack(
                    NextSpawnPose(layout, layout.MainVolume, occupied, out Quaternion rotation, pack: true, faceDown),
                    rotation,
                    packDefinition,
                    packName: "BoosterPack_All_" + (i + 1),
                    packVariantIndex: variantIndex,
                    preRolledContents: contents);
                FinishPack(pack, folder, PhysicsLevelItem.AreaKind.Main, MixAllPhysicsBatchIndex, registerUndo: false);
                pack.SetGroundShowsBack(faceDown);
                done++;
                EditorUtility.DisplayProgressBar("Mix All", "Spawning packs " + done + " / " + total, done / (float)total);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        MarkDirty(layout);
        SelectChildren(folder);
        Debug.Log(
            "TCG Card Chaos: Mix All spawned "
            + plan.FloorCards.Count + " unique cards, "
            + plan.PsaItems.Count + " PSA, "
            + PhysicsLevelLayout.MixPackCount + " packs. Face-down ~10%.");
    }

    Transform FindMixAll(PhysicsLevelLayout layout)
    {
        if (layout == null || layout.MainLevelRoot == null)
            return null;

        return layout.MainLevelRoot.Find(PhysicsLevelLayout.FormatMixAllName());
    }

    Transform GetOrCreateMixAllFolder(PhysicsLevelLayout layout)
    {
        Transform existing = FindMixAll(layout);
        if (existing != null)
            return existing;

        var go = new GameObject(PhysicsLevelLayout.FormatMixAllName());
        go.transform.SetParent(layout.MainLevelRoot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    MixAllPlan GetCachedMixAllPlan()
    {
        if (_hasMixAllPlan && _cachedMixAllPlan.FloorCards != null)
            return _cachedMixAllPlan;

        _cachedMixAllPlan = BuildMixAllPlan();
        _hasMixAllPlan = true;
        return _cachedMixAllPlan;
    }

    static MixAllPlan BuildMixAllPlan()
    {
        CardCatalog.EnsureLoaded();
        var floorCards = new List<CardDefinition>();
        IReadOnlyList<CardDefinition> catalog = CardCatalog.All;
        for (int i = 0; i < catalog.Count; i++)
        {
            CardDefinition definition = catalog[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.DefinitionId))
                continue;
            floorCards.Add(definition);
        }

        var psaItems = new List<MixSpawnItem>();
        for (int i = 0; i < PsaArtLibrary.CabinetSlotCount; i++)
        {
            int slot = PsaArtLibrary.CabinetSlotNumbers[i];
            int variantCount = PsaArtLibrary.CountVariantsInSlot(slot);
            for (int variant = 1; variant <= variantCount; variant++)
                psaItems.Add(MixSpawnItem.Psa(slot, variant));
        }

        Random.State previousState = Random.state;
        Random.InitState(MixShuffleSeed);
        Shuffle(floorCards);
        Shuffle(psaItems);

        int packCardCount = PhysicsLevelLayout.MixPackCount * CardDimensions.CardsPerBoosterPack;
        var packCards = new List<CardDefinition>(packCardCount);
        if (floorCards.Count > 0)
        {
            for (int i = 0; i < packCardCount; i++)
                packCards.Add(floorCards[Random.Range(0, floorCards.Count)]);
            Shuffle(packCards);
        }

        Random.state = previousState;
        return new MixAllPlan(floorCards, psaItems, packCards);
    }

    struct MixAllPlan
    {
        public readonly List<CardDefinition> FloorCards;
        public readonly List<MixSpawnItem> PsaItems;
        public readonly List<CardDefinition> PackCards;

        public MixAllPlan(
            List<CardDefinition> floorCards,
            List<MixSpawnItem> psaItems,
            List<CardDefinition> packCards)
        {
            FloorCards = floorCards ?? new List<CardDefinition>();
            PsaItems = psaItems ?? new List<MixSpawnItem>();
            PackCards = packCards ?? new List<CardDefinition>();
        }
    }

    int CountLegacyMixItems(PhysicsLevelLayout layout)
    {
        if (layout == null || layout.MainLevelRoot == null)
            return 0;

        int count = 0;
        for (int i = 0; i < layout.MainLevelRoot.childCount; i++)
        {
            Transform child = layout.MainLevelRoot.GetChild(i);
            if (child == null || child.name == PhysicsLevelLayout.MixAllName)
                continue;
            if (child.name.StartsWith(PhysicsLevelLayout.MixBatchPrefix)
                || child.name.StartsWith(PhysicsLevelLayout.BatchPrefix))
                count += child.childCount;
        }

        return count;
    }

    Vector3 NextSpawnPose(
        PhysicsLevelLayout layout,
        PhysicsCardSpawnVolume volume,
        List<Vector3> occupied,
        out Quaternion rotation,
        bool pack,
        bool faceDown = false)
    {
        Vector3 point = volume.GetRandomPoint(layout.SpawnPadding);
        point.y += Random.Range(0f, layout.HeightBias);
        float minSq = layout.MinSpawnSpacing * layout.MinSpawnSpacing;
        for (int attempt = 0; attempt < SpawnOverlapTries; attempt++)
        {
            bool clear = true;
            for (int i = 0; i < occupied.Count; i++)
            {
                if ((occupied[i] - point).sqrMagnitude < minSq)
                {
                    clear = false;
                    break;
                }
            }

            if (clear)
                break;

            point = volume.GetRandomPoint(layout.SpawnPadding);
            point.y += Random.Range(0f, layout.HeightBias);
        }

        float minSpawnY = CardFactory.GroundSurfaceY() + MinSpawnClearance;
        if (point.y < minSpawnY)
            point.y = minSpawnY;

        occupied.Add(point);
        rotation = ScatterRotation(layout.RotationRandomness, pack, faceDown);
        return point;
    }

    /// <summary>
    /// Face-up or face-down with yaw, plus a small tilt. About 10% of Mix All items spawn face-down.
    /// </summary>
    static Quaternion ScatterRotation(float randomness, bool pack, bool faceDown)
    {
        float amount = Mathf.Clamp01(randomness);
        float yaw = Random.Range(0f, 360f);
        float maxTilt = pack
            ? Mathf.Lerp(3f, 12f, amount)
            : Mathf.Lerp(5f, 22f, amount);
        float pitch = Random.Range(-maxTilt, maxTilt);
        float roll = Random.Range(-maxTilt, maxTilt);
        return Quaternion.Euler(faceDown ? 180f + pitch : pitch, yaw, roll);
    }

    void FinishCard(
        WorldCard card,
        Transform parent,
        PhysicsLevelItem.AreaKind area,
        int batchIndex,
        bool registerUndo = true)
    {
        if (registerUndo)
        {
            Undo.RegisterCreatedObjectUndo(card.gameObject, "Spawn Physics Card");
            Undo.SetTransformParent(card.transform, parent, "Parent Physics Card");
        }
        else
            card.transform.SetParent(parent, true);

        card.PrepareEditorPhysicsPlacement();
        PhysicsLevelItem item = registerUndo
            ? Undo.AddComponent<PhysicsLevelItem>(card.gameObject)
            : card.gameObject.AddComponent<PhysicsLevelItem>();
        item.Configure(area, batchIndex, isBaked: false);
        EditorUtility.SetDirty(card);
    }

    void FinishPack(
        WorldBoosterPack pack,
        Transform parent,
        PhysicsLevelItem.AreaKind area,
        int batchIndex,
        bool registerUndo = true)
    {
        if (registerUndo)
        {
            Undo.RegisterCreatedObjectUndo(pack.gameObject, "Spawn Physics Pack");
            Undo.SetTransformParent(pack.transform, parent, "Parent Physics Pack");
        }
        else
            pack.transform.SetParent(parent, true);

        pack.PrepareEditorPhysicsPlacement();
        PhysicsLevelItem item = registerUndo
            ? Undo.AddComponent<PhysicsLevelItem>(pack.gameObject)
            : pack.gameObject.AddComponent<PhysicsLevelItem>();
        item.Configure(area, batchIndex, isBaked: false);
        EditorUtility.SetDirty(pack);
    }

    void ScheduleDrop(PhysicsLevelLayout layout, PhysicsCardSpawnVolume volume, Transform folder)
    {
        // Start Grabbit after IMGUI finishes this frame. Calling it from OnGUI
        // breaks layout groups (EndLayoutGroup errors) and floods the console.
        EditorApplication.delayCall += () => DropWithGrabbit(layout, volume, folder);
    }

    void ScheduleDropSelected(bool lift)
    {
        var items = new List<PhysicsLevelItem>(CollectValidSelectedItems());
        EditorApplication.delayCall += () => DropSelectedWithGrabbit(items, lift);
    }

    void ScheduleDropPacks(Transform folder)
    {
        var items = new List<PhysicsLevelItem>();
        if (folder != null)
        {
            for (int i = 0; i < folder.childCount; i++)
            {
                Transform child = folder.GetChild(i);
                if (child == null || child.GetComponent<WorldBoosterPack>() == null)
                    continue;

                PhysicsLevelItem item = child.GetComponent<PhysicsLevelItem>();
                if (item != null)
                    items.Add(item);
            }
        }

        EditorApplication.delayCall += () => DropSelectedWithGrabbit(items, lift: true);
    }

    void ScheduleDropUpright(Transform folder)
    {
        var items = CollectUprightItems(folder);
        if (items.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Re-drop Upright",
                "This mix has no standing cards or packs.",
                "OK");
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                LayItemFlatKeepingYaw(items[i].transform);
        }

        EditorApplication.delayCall += () => DropSelectedWithGrabbit(items, lift: true);
    }

    void ScheduleSettleAirborne(Transform folder)
    {
        List<PhysicsLevelItem> items = CollectAirborneOrUprightItems(folder);
        if (items.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Settle Airborne",
                "Havada veya dik duran kart / PSA / pack yok.",
                "OK");
            return;
        }

        int cards = 0;
        int psa = 0;
        int packs = 0;
        for (int i = 0; i < items.Count; i++)
        {
            PhysicsLevelItem item = items[i];
            if (item == null)
                continue;

            if (item.GetComponent<WorldBoosterPack>() != null)
            {
                packs++;
                continue;
            }

            WorldCard card = item.GetComponent<WorldCard>();
            if (card != null && card.UsesPsaSlab)
                psa++;
            else
                cards++;
        }

        if (!EditorUtility.DisplayDialog(
                "Settle Airborne",
                cards + " kart, " + psa + " PSA, " + packs + " pack düşecek.\n"
                + "Yerde düzgün yatanlara dokunulmaz.\n\n"
                + "Grabbit açılınca Scene view'de Left Shift basılı tut. Oturunca Bake Mix All, sonra sahneyi kaydet.",
                "Düşür",
                "İptal"))
            return;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                LayItemFlatKeepingYaw(items[i].transform);
        }

        EditorApplication.delayCall += () => DropSelectedWithGrabbit(items, lift: true);
    }

    static List<PhysicsLevelItem> CollectAirborneOrUprightItems(Transform folder)
    {
        var items = new List<PhysicsLevelItem>();
        if (folder == null)
            return items;

        float floorY = CardFactory.GroundSurfaceY();
        for (int i = 0; i < folder.childCount; i++)
        {
            Transform child = folder.GetChild(i);
            if (child == null || IsAuthoredCabinetOrShelfItem(child))
                continue;

            PhysicsLevelItem item = child.GetComponent<PhysicsLevelItem>();
            if (item == null)
                continue;

            if (IsStandingOnEdge(child) || IsAirborne(child, floorY))
                items.Add(item);
        }

        return items;
    }

    static bool IsAuthoredCabinetOrShelfItem(Transform t)
    {
        return t != null
            && (t.GetComponentInParent<CardShelfSlot>() != null
                || t.GetComponentInParent<PsaCabinetSlot>() != null);
    }

    static bool IsAirborne(Transform t, float floorY)
    {
        if (t == null)
            return false;

        Bounds bounds;
        Collider col = t.GetComponent<Collider>();
        if (col != null)
            bounds = col.bounds;
        else
        {
            Renderer renderer = t.GetComponentInChildren<Renderer>();
            if (renderer == null)
                return t.position.y > floorY + AirborneMinClearance;
            bounds = renderer.bounds;
        }

        return bounds.min.y > floorY + AirborneMinClearance;
    }

    static List<PhysicsLevelItem> CollectUprightItems(Transform folder)
    {
        var items = new List<PhysicsLevelItem>();
        if (folder == null)
            return items;

        for (int i = 0; i < folder.childCount; i++)
        {
            Transform child = folder.GetChild(i);
            if (child == null || !IsStandingOnEdge(child))
                continue;

            PhysicsLevelItem item = child.GetComponent<PhysicsLevelItem>();
            if (item != null)
                items.Add(item);
        }

        return items;
    }

    static bool IsStandingOnEdge(Transform t)
    {
        return t != null && Mathf.Abs(t.up.y) < 0.62f;
    }

    static void LayItemFlatKeepingYaw(Transform t)
    {
        if (t == null)
            return;

        Vector3 up = t.up;
        if (Mathf.Abs(up.y) >= 0.62f)
            return;

        Vector3 targetUp = up.y >= 0f ? Vector3.up : Vector3.down;
        Undo.RecordObject(t, "Lay Upright Cards Flat");
        t.rotation = Quaternion.FromToRotation(up, targetUp) * t.rotation;
    }

    void DropWithGrabbit(PhysicsLevelLayout layout, PhysicsCardSpawnVolume volume, Transform folder)
    {
        if (folder == null || folder.childCount == 0)
        {
            EditorUtility.DisplayDialog("Grabbit", "Nothing to drop in this folder.", "OK");
            return;
        }

        var keepDynamic = new HashSet<PhysicsLevelItem>();
        for (int i = 0; i < folder.childCount; i++)
        {
            PhysicsLevelItem item = folder.GetChild(i).GetComponent<PhysicsLevelItem>();
            if (item != null)
                keepDynamic.Add(item);
        }

        BakeOthersAsStatic(keepDynamic);
        var items = new List<PhysicsLevelItem>(keepDynamic);
        PrepareItemsForResimulate(items, lift: false);
        ConfigureGrabbitLimitation(volume, folder);
        PrepareAuthoringCollision(volume, items);
        SelectChildren(folder);
        ApplyGrabbitFallSettings();
        GrabbitEditor.ShowSceneCommandWindowAndFall();
        SceneView.lastActiveSceneView?.Focus();
    }

    void DropSelectedWithGrabbit(List<PhysicsLevelItem> items, bool lift)
    {
        if (items == null || items.Count == 0)
        {
            EditorUtility.DisplayDialog("Grabbit", "No valid authored cards selected.", "OK");
            return;
        }

        var keepDynamic = new HashSet<PhysicsLevelItem>(items);
        BakeOthersAsStatic(keepDynamic);
        PrepareItemsForResimulate(items, lift);
        ConfigureGrabbitLimitationFromItems(items);
        PrepareAuthoringCollision(FindVolumeForItems(items), items);
        SelectItems(items);
        ApplyGrabbitFallSettings();
        GrabbitEditor.ShowSceneCommandWindowAndFall();
        SceneView.lastActiveSceneView?.Focus();
    }

    void ApplyGrabbitFallSettings()
    {
        GrabbitSettings settings = GrabbitEditor.GetOrFetchSettings();
        if (settings == null)
            return;

        settings.CurrentMode = GrabbitMode.FALL;
        settings.DidConfigureLimitationRangeAtLeastOnce = true;
        // Cards already have a thin BoxCollider. Skip VHACD mesh-hull generation
        // (that path floods the console and needs a Windows native DLL).
        settings.UseDynamicNonConvexColliders = false;
        settings.ColliderGenerationMode = ColliderGenerationMode.USE_EXISTING_ONLY;

        // Grabbit Fall drives PhysX via Physics.Simulate(fixedDeltaTime * Speed).
        // Keep Speed at 1 so steps stay 20 ms. Unlimited fall velocity + zero damping
        // (LimitBodyVelocityInGravityMode off) lets thin cards punch through the floor.
        settings.Speed = 1f;
        settings.MaxPhysXIterationPerUpdate = 4;
        settings.solverIterations = 16;
        settings.velocityIterations = 8;
        settings.LimitBodyVelocityInGravityMode = true;
        settings.MaxVelocity = 6f;
        settings.MaxAngularVelocity = 12f;
        settings.MaxDepenetrationVelocity = 1f;
        settings.CollisionMaxVelocity = 2f;
        settings.Drag = 0.25f;
        settings.AngularDrag = 0.8f;
        EditorUtility.SetDirty(settings);
    }

    void BakeOthersAsStatic(HashSet<PhysicsLevelItem> keepDynamic)
    {
        PhysicsLevelItem[] items = Object.FindObjectsByType<PhysicsLevelItem>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < items.Length; i++)
        {
            PhysicsLevelItem item = items[i];
            if (item == null || !BelongsToPhysicsLevel(item))
                continue;
            if (keepDynamic != null && keepDynamic.Contains(item))
                continue;

            BakeItem(item, undoName: null);
        }
    }

    void BakeFolder(Transform folder, string undoName)
    {
        if (folder == null)
            return;

        var items = new List<PhysicsLevelItem>(folder.childCount);
        for (int i = 0; i < folder.childCount; i++)
        {
            PhysicsLevelItem item = folder.GetChild(i).GetComponent<PhysicsLevelItem>();
            if (item != null)
                items.Add(item);
        }

        BakeItems(items, undoName);
    }

    void BakeItems(List<PhysicsLevelItem> items, string undoName)
    {
        if (items == null || items.Count == 0)
            return;

        StopFloorGuard();
        DestroyAuthoringFloor();

        Undo.SetCurrentGroupName(undoName);
        for (int i = 0; i < items.Count; i++)
            BakeItem(items[i], undoName);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    void BakeItem(PhysicsLevelItem item, string undoName)
    {
        if (item == null)
            return;

        StripGrabbitRuntime(item.gameObject);
        bool alreadyBaked = item.Baked;

        WorldCard card = item.GetComponent<WorldCard>();
        if (card != null)
        {
            if (!string.IsNullOrEmpty(undoName))
                Undo.RecordObject(card, undoName);
            card.StripEditorRigidbody();
            if (alreadyBaked)
                card.ApplySolidEditorCollider();
            else
                card.PrepareEditorPhysicsPlacement();
        }

        WorldBoosterPack pack = item.GetComponent<WorldBoosterPack>();
        if (pack != null)
        {
            if (!string.IsNullOrEmpty(undoName))
                Undo.RecordObject(pack, undoName);
            pack.StripEditorRigidbody();
            if (!alreadyBaked)
            {
                pack.PrepareEditorPhysicsPlacement();
                pack.SnapAuthoredVisualOntoCollider();
            }
        }

        if (!string.IsNullOrEmpty(undoName))
            Undo.RecordObject(item, undoName);
        item.MarkBaked();
    }

    void PrepareItemsForResimulate(List<PhysicsLevelItem> items, bool lift)
    {
        const float WakeLift = 0.04f;
        for (int i = 0; i < items.Count; i++)
        {
            PhysicsLevelItem item = items[i];
            if (item == null)
                continue;

            StripGrabbitRuntime(item.gameObject);

            WorldCard card = item.GetComponent<WorldCard>();
            if (card != null)
                card.PrepareEditorPhysicsPlacement();

            WorldBoosterPack pack = item.GetComponent<WorldBoosterPack>();
            if (pack != null)
                pack.PrepareEditorPhysicsPlacement();

            if (lift)
            {
                Undo.RecordObject(item.transform, "Wake Selected Cards");
                item.transform.position += Vector3.up * WakeLift;
            }
        }
    }

    static void StripGrabbitRuntime(GameObject go)
    {
        GrabbitHandler handler = go.GetComponent<GrabbitHandler>();
        if (handler != null)
            Object.DestroyImmediate(handler);

        GrabbitData data = go.GetComponent<GrabbitData>();
        if (data != null)
        {
            data.Cleanup();
            Object.DestroyImmediate(data);
        }
    }

    void ConfigureGrabbitLimitation(PhysicsCardSpawnVolume volume, Transform folder)
    {
        Bounds bounds;
        if (volume != null)
            bounds = volume.Box.bounds;
        else
            bounds = new Bounds(folder != null ? folder.position : Vector3.zero, Vector3.one * 4f);

        bounds.Encapsulate(new Vector3(bounds.center.x, 0f, bounds.center.z));
        bounds.Expand(2.5f);
        ApplyLimitationBounds(bounds);
    }

    void ConfigureGrabbitLimitationFromItems(List<PhysicsLevelItem> items)
    {
        if (items == null || items.Count == 0)
            return;

        Bounds bounds = ItemWorldBounds(items[0]);
        for (int i = 1; i < items.Count; i++)
            bounds.Encapsulate(ItemWorldBounds(items[i]));

        bounds.Encapsulate(new Vector3(bounds.center.x, 0f, bounds.center.z));
        bounds.Expand(2.5f);
        ApplyLimitationBounds(bounds);
    }

    void ApplyLimitationBounds(Bounds bounds)
    {
        GrabbitSettings settings = GrabbitEditor.GetOrFetchSettings();
        if (settings == null)
            return;

        if (settings.LimitationRange == null)
            settings.LimitationRange = new GrabbitRange();

        settings.UseLimitationZone = true;
        settings.LimitationRange.position = bounds.center;
        settings.LimitationRange.size = bounds.size;
        settings.LimitationRange.BoxHandle = new UnityEditor.IMGUI.Controls.BoxBoundsHandle
        {
            center = bounds.center,
            size = bounds.size
        };
        settings.LimitationRange.IsInitialized = true;
        settings.DidConfigureLimitationRangeAtLeastOnce = true;
        EditorUtility.SetDirty(settings);
    }

    static Bounds ItemWorldBounds(PhysicsLevelItem item)
    {
        Collider col = item.GetComponent<Collider>();
        if (col != null)
            return col.bounds;

        Renderer renderer = item.GetComponentInChildren<Renderer>();
        if (renderer != null)
            return renderer.bounds;

        return new Bounds(item.transform.position, Vector3.one * 0.2f);
    }

    void PrepareAuthoringCollision(PhysicsCardSpawnVolume volume, List<PhysicsLevelItem> items)
    {
        EnsureSceneFloorBoxColliders();
        EnsureAuthoringFloorSlab(volume, items);
        StartFloorGuard(items, volume);
    }

    PhysicsCardSpawnVolume FindVolumeForItems(List<PhysicsLevelItem> items)
    {
        PhysicsLevelLayout layout = PhysicsLevelLayout.FindExisting();
        if (layout == null || items == null || items.Count == 0)
            return layout != null ? layout.MainVolume : null;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].Area == PhysicsLevelItem.AreaKind.Demo)
                return layout.DemoVolume;
        }

        return layout.MainVolume;
    }

    static void EnsureSceneFloorBoxColliders()
    {
        var seen = new HashSet<GameObject>();
        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            GameObject target = GetGrabbitHandlerTarget(renderer.gameObject);
            if (target == null || !seen.Add(target))
                continue;
            if (!LooksLikeFloorName(target.name) && !LooksLikeFloorName(renderer.gameObject.name))
                continue;
            if (target.name == AuthoringFloorName)
                continue;

            ConfigureFloorBoxCollider(target, renderer);
        }
    }

    static GameObject GetGrabbitHandlerTarget(GameObject go)
    {
        if (go == null)
            return null;

        if (PrefabUtility.IsPartOfAnyPrefab(go))
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (root != null)
                return root;
        }

        return go;
    }

    static void ConfigureFloorBoxCollider(GameObject floor, MeshRenderer renderer)
    {
        BoxCollider box = floor.GetComponent<BoxCollider>();
        if (box == null)
            box = Undo.AddComponent<BoxCollider>(floor);

        Bounds world = renderer.bounds;
        Renderer[] children = floor.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].enabled)
                world.Encapsulate(children[i].bounds);
        }

        Undo.RecordObject(box, "Authoring Floor BoxCollider");
        FitLocalBoxToWorldFloor(floor.transform, world, box);
        box.isTrigger = false;
        box.enabled = true;
        EditorUtility.SetDirty(box);
        EditorUtility.SetDirty(floor);
    }

    static void FitLocalBoxToWorldFloor(Transform t, Bounds world, BoxCollider box)
    {
        Vector3 min = world.min;
        Vector3 max = world.max;
        Vector3 localMin = t.InverseTransformPoint(min);
        Vector3 localMax = t.InverseTransformPoint(max);
        EncapsulateLocal(t, new Vector3(min.x, min.y, max.z), ref localMin, ref localMax);
        EncapsulateLocal(t, new Vector3(min.x, max.y, min.z), ref localMin, ref localMax);
        EncapsulateLocal(t, new Vector3(min.x, max.y, max.z), ref localMin, ref localMax);
        EncapsulateLocal(t, new Vector3(max.x, min.y, min.z), ref localMin, ref localMax);
        EncapsulateLocal(t, new Vector3(max.x, min.y, max.z), ref localMin, ref localMax);
        EncapsulateLocal(t, new Vector3(max.x, max.y, min.z), ref localMin, ref localMax);
        EncapsulateLocal(t, new Vector3(max.x, max.y, max.z), ref localMin, ref localMax);

        float localThickness = AuthoringFloorThickness / Mathf.Max(0.01f, Mathf.Abs(t.lossyScale.y));
        localMin.y = localMax.y - localThickness;
        box.center = (localMin + localMax) * 0.5f;
        box.size = new Vector3(
            Mathf.Max(0.1f, localMax.x - localMin.x),
            localThickness,
            Mathf.Max(0.1f, localMax.z - localMin.z));
    }

    static void EncapsulateLocal(Transform t, Vector3 worldPoint, ref Vector3 localMin, ref Vector3 localMax)
    {
        Vector3 local = t.InverseTransformPoint(worldPoint);
        localMin = Vector3.Min(localMin, local);
        localMax = Vector3.Max(localMax, local);
    }

    void EnsureAuthoringFloorSlab(PhysicsCardSpawnVolume volume, List<PhysicsLevelItem> items)
    {
        Bounds area;
        if (volume != null)
            area = volume.Box.bounds;
        else if (items != null && items.Count > 0)
        {
            area = ItemWorldBounds(items[0]);
            for (int i = 1; i < items.Count; i++)
                area.Encapsulate(ItemWorldBounds(items[i]));
        }
        else
            area = new Bounds(Vector3.zero, new Vector3(12f, 2f, 12f));

        area.Encapsulate(new Vector3(area.center.x, 0f, area.center.z));
        area.Expand(new Vector3(4f, 0f, 4f));

        if (_authoringFloor == null)
            _authoringFloor = GameObject.Find(AuthoringFloorName);

        if (_authoringFloor == null)
        {
            _authoringFloor = new GameObject(AuthoringFloorName);
            _authoringFloor.hideFlags = HideFlags.HideAndDontSave;
        }

        BoxCollider box = _authoringFloor.GetComponent<BoxCollider>();
        if (box == null)
            box = _authoringFloor.AddComponent<BoxCollider>();

        float floorY = CardFactory.GroundSurfaceY();
        _authoringFloor.transform.SetPositionAndRotation(
            new Vector3(area.center.x, floorY - AuthoringFloorThickness * 0.5f, area.center.z),
            Quaternion.identity);
        _authoringFloor.transform.localScale = Vector3.one;
        box.center = Vector3.zero;
        box.size = new Vector3(Mathf.Max(8f, area.size.x), AuthoringFloorThickness, Mathf.Max(8f, area.size.z));
        box.isTrigger = false;
        box.enabled = true;
    }

    void DestroyAuthoringFloor()
    {
        if (_authoringFloor == null)
            _authoringFloor = GameObject.Find(AuthoringFloorName);

        if (_authoringFloor != null)
        {
            Object.DestroyImmediate(_authoringFloor);
            _authoringFloor = null;
        }
    }

    void StartFloorGuard(List<PhysicsLevelItem> items, PhysicsCardSpawnVolume volume)
    {
        _guardItems = items != null ? new List<PhysicsLevelItem>(items) : null;
        _guardVolume = volume;
        _sawGrabbitActive = false;
        if (_floorGuardActive)
            return;

        EditorApplication.update += FloorGuardTick;
        _floorGuardActive = true;
    }

    void StopFloorGuard()
    {
        if (_floorGuardActive)
        {
            EditorApplication.update -= FloorGuardTick;
            _floorGuardActive = false;
        }

        _sawGrabbitActive = false;
        _guardItems = null;
        _guardVolume = null;
    }

    void FloorGuardTick()
    {
        GrabbitSettings settings = GrabbitEditor.GetOrFetchSettings();
        bool grabbitOn = settings != null && settings.IsGrabbitActive;
        if (grabbitOn)
            _sawGrabbitActive = true;
        else if (_sawGrabbitActive)
        {
            StopFloorGuard();
            DestroyAuthoringFloor();
            return;
        }

        if (_guardItems == null || _guardItems.Count == 0)
            return;

        float floorY = CardFactory.GroundSurfaceY();
        float abortY = floorY - FloorGuardBelowMargin;
        float restoreY = floorY + MinSpawnClearance;
        if (_guardVolume != null)
            restoreY = Mathf.Max(restoreY, _guardVolume.Box.bounds.min.y);

        for (int i = 0; i < _guardItems.Count; i++)
        {
            PhysicsLevelItem item = _guardItems[i];
            if (item == null)
                continue;

            Transform t = item.transform;
            if (t.position.y >= abortY)
                continue;

            Vector3 restored = t.position;
            restored.y = restoreY;
            t.position = restored;

            Rigidbody body = item.GetComponent<Rigidbody>();
            if (body == null)
                continue;

            body.position = restored;
            if (body.isKinematic)
                continue;

            Vector3 velocity = body.linearVelocity;
            if (velocity.y < 0f)
                velocity.y = 0f;
            body.linearVelocity = velocity;
            body.angularVelocity *= 0.25f;
        }
    }

    static bool LooksLikeFloorName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return name.StartsWith("Floor", System.StringComparison.OrdinalIgnoreCase)
            || name.Equals("Ground", System.StringComparison.OrdinalIgnoreCase);
    }

    void SelectChildren(Transform folder)
    {
        if (folder == null)
            return;

        var objects = new List<Object>(folder.childCount);
        for (int i = 0; i < folder.childCount; i++)
            objects.Add(folder.GetChild(i).gameObject);

        Selection.objects = objects.ToArray();
    }

    void SelectItems(List<PhysicsLevelItem> items)
    {
        if (items == null || items.Count == 0)
            return;

        var objects = new Object[items.Count];
        for (int i = 0; i < items.Count; i++)
            objects[i] = items[i].gameObject;

        Selection.objects = objects;
    }

    static List<PhysicsLevelItem> CollectValidSelectedItems()
    {
        var unique = new List<PhysicsLevelItem>();
        var seen = new HashSet<PhysicsLevelItem>();
        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
            return unique;

        for (int i = 0; i < selected.Length; i++)
        {
            GameObject go = selected[i];
            if (go == null)
                continue;

            PhysicsLevelItem item = go.GetComponent<PhysicsLevelItem>();
            if (item == null)
                item = go.GetComponentInParent<PhysicsLevelItem>();
            if (item == null || !BelongsToPhysicsLevel(item) || !seen.Add(item))
                continue;

            unique.Add(item);
        }

        return unique;
    }

    static bool BelongsToPhysicsLevel(PhysicsLevelItem item)
    {
        Transform current = item.transform;
        while (current != null)
        {
            if (current.GetComponent<PhysicsLevelLayout>() != null)
                return true;
            if (current.name == PhysicsLevelLayout.RootName)
                return true;
            current = current.parent;
        }

        return false;
    }

    static void Shuffle<T>(List<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            T temp = items[i];
            items[i] = items[swap];
            items[swap] = temp;
        }
    }

    struct MixSpawnItem
    {
        public int PsaSlot;
        public int PsaVariant;

        public static MixSpawnItem Psa(int slot, int variant)
        {
            return new MixSpawnItem { PsaSlot = slot, PsaVariant = variant };
        }
    }

    int CountArea(PhysicsLevelItem.AreaKind area, int batchIndex)
    {
        PhysicsLevelItem[] items = Object.FindObjectsByType<PhysicsLevelItem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null || items[i].Area != area)
                continue;
            if (batchIndex >= 0 && items[i].BatchIndex != batchIndex)
                continue;
            count++;
        }

        return count;
    }

    static void MarkDirty(Object target)
    {
        if (target == null)
            return;

        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    PhysicsLevelLayout EnsureLayout(bool createIfMissing)
    {
        PhysicsLevelLayout layout = PhysicsLevelLayout.FindExisting();
        if (layout != null)
            return layout;

        if (!createIfMissing)
            return null;

        var root = new GameObject(PhysicsLevelLayout.RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Physics Card Level");
        layout = Undo.AddComponent<PhysicsLevelLayout>(root);

        Transform demoArea = CreateChild(root.transform, PhysicsLevelLayout.DemoAreaName);
        Transform demoCards = CreateChild(demoArea, PhysicsLevelLayout.DemoCardsName);
        Transform demoVolume = CreateChild(demoArea, PhysicsLevelLayout.DemoVolumeName);
        PhysicsCardSpawnVolume demoSpawn = Undo.AddComponent<PhysicsCardSpawnVolume>(demoVolume.gameObject);
        demoSpawn.EnsureSetup(forceDefault: true);
        PlaceDemoVolumeFromScatterZone(demoVolume);

        Transform mainLevel = CreateChild(root.transform, PhysicsLevelLayout.MainLevelName);
        Transform mainVolume = CreateChild(mainLevel, PhysicsLevelLayout.MainVolumeName);
        PhysicsCardSpawnVolume mainSpawn = Undo.AddComponent<PhysicsCardSpawnVolume>(mainVolume.gameObject);
        mainSpawn.EnsureSetup(forceDefault: true);
        mainVolume.position = demoVolume.position + new Vector3(12f, 0f, 0f);
        mainVolume.localScale = demoVolume.localScale;

        layout.BindHierarchy(demoSpawn, mainSpawn, demoCards, mainLevel);
        Selection.activeGameObject = root;
        return layout;
    }

    static Transform CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        Undo.SetTransformParent(go.transform, parent, "Parent " + name);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    static void PlaceDemoVolumeFromScatterZone(Transform demoVolume)
    {
        CardScatterZone zone = Object.FindFirstObjectByType<CardScatterZone>();
        if (zone == null)
        {
            demoVolume.position = new Vector3(8.97f, 1.6f, -8.05f);
            demoVolume.localScale = new Vector3(8.8f, 2.2f, 9.4f);
            return;
        }

        Bounds bounds = zone.GetComponent<BoxCollider>().bounds;
        demoVolume.position = new Vector3(bounds.center.x, bounds.max.y + 1.4f, bounds.center.z);
        demoVolume.localScale = new Vector3(
            Mathf.Max(2f, bounds.size.x),
            2.2f,
            Mathf.Max(2f, bounds.size.z));
    }
}
