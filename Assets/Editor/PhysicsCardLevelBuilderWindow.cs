using System.Collections.Generic;
using Grabbit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Spawns real game cards into Demo / Main folders. Grabbit Fall does the physics placement.
/// </summary>
public class PhysicsCardLevelBuilderWindow : EditorWindow
{
    const int SpawnOverlapTries = 12;
    const string AuthoringFloorName = "GrabbitAuthoringFloor";
    const float AuthoringFloorThickness = 0.6f;
    const float FloorGuardBelowMargin = 0.25f;
    const float MinSpawnClearance = 0.35f;

    Vector2 _scroll;
    int _selectedBatchIndex = 1;
    List<PhysicsLevelItem> _guardItems;
    PhysicsCardSpawnVolume _guardVolume;
    GameObject _authoringFloor;
    bool _floorGuardActive;
    bool _sawGrabbitActive;
    bool _promotingSelection;

    [MenuItem("TCG Card Caos/Card Physics Level Builder")]
    public static void Open()
    {
        GetWindow<PhysicsCardLevelBuilderWindow>("Card Physics Level Builder");
    }

    void OnEnable()
    {
        PhysicsLevelLayout layout = PhysicsLevelLayout.FindExisting();
        if (layout != null)
            _selectedBatchIndex = layout.CurrentMainBatchIndex;
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
            "This tool only creates real cards (CardFactory / PackFactory). "
            + "Drop them with Grabbit Fall. Fall starts paused — hold Left Shift in the Scene view to let gravity run, then Bake and save the Scene.",
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
        DrawDemo(layout);
        EditorGUILayout.Space(10);
        DrawMain(layout);
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
        int demoItems = CountArea(PhysicsLevelItem.AreaKind.Demo, -1);
        int mainItems = CountArea(PhysicsLevelItem.AreaKind.Main, -1);
        EditorGUILayout.LabelField("Live counts", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Demo items in scene", demoItems.ToString());
        EditorGUILayout.LabelField("Demo configured total", layout.DemoConfiguredTotal.ToString());
        EditorGUILayout.LabelField("Main level items", mainItems.ToString());
        EditorGUILayout.LabelField("Scene authored items", (demoItems + mainItems).ToString());
    }

    void DrawDemo(PhysicsLevelLayout layout)
    {
        EditorGUILayout.LabelField("Demo Area", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        layout.DemoVolume = ObjectFieldVolume("Demo Spawn Volume", layout.DemoVolume);
        layout.DemoRegularCount = EditorGUILayout.IntField("Regular Card Count", layout.DemoRegularCount);
        layout.DemoPsaCount = EditorGUILayout.IntField("PSA Card Count", layout.DemoPsaCount);
        layout.DemoPackCount = EditorGUILayout.IntField("Booster Pack Count", layout.DemoPackCount);
        EditorGUILayout.LabelField("Demo Total Cards", layout.DemoConfiguredTotal.ToString());

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate Demo Cards", GUILayout.Height(24)))
            GenerateDemo(layout);
        if (GUILayout.Button("Delete Demo Cards", GUILayout.Height(24)))
            DeleteArea(layout.DemoCardsRoot, "Delete Demo Cards");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select Demo Cards"))
            SelectChildren(layout.DemoCardsRoot);
        if (GUILayout.Button("Grabbit Fall Demo"))
            ScheduleDrop(layout, layout.DemoVolume, layout.DemoCardsRoot);
        if (GUILayout.Button("Bake Demo"))
            BakeFolder(layout.DemoCardsRoot, "Bake Demo Cards");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    void DrawMain(PhysicsLevelLayout layout)
    {
        EditorGUILayout.LabelField("Main Level Batches", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        layout.MainVolume = ObjectFieldVolume("Main Spawn Volume", layout.MainVolume);
        layout.MainBatchSize = EditorGUILayout.IntField("Batch Size", layout.MainBatchSize);
        layout.MainPsaCount = EditorGUILayout.IntField("PSA Per Batch", layout.MainPsaCount);
        layout.MainPackCount = EditorGUILayout.IntField("Packs Per Batch", layout.MainPackCount);
        _selectedBatchIndex = EditorGUILayout.IntField("Current Batch Index", Mathf.Max(1, _selectedBatchIndex));
        layout.CurrentMainBatchIndex = _selectedBatchIndex;

        Transform current = FindBatch(layout, _selectedBatchIndex);
        int currentCount = current != null ? current.childCount : 0;
        EditorGUILayout.LabelField("Current Batch", PhysicsLevelLayout.FormatBatchName(_selectedBatchIndex));
        EditorGUILayout.LabelField("Current Batch Cards", currentCount.ToString());
        EditorGUILayout.LabelField("Total Main Level Cards", CountArea(PhysicsLevelItem.AreaKind.Main, -1).ToString());

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Create Main Batch", GUILayout.Height(24)))
            CreateMainBatch(layout, _selectedBatchIndex);
        if (GUILayout.Button("Create Next Batch", GUILayout.Height(24)))
        {
            _selectedBatchIndex = NextFreeBatchIndex(layout);
            layout.CurrentMainBatchIndex = _selectedBatchIndex;
            CreateMainBatch(layout, _selectedBatchIndex);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select Current Batch"))
            SelectChildren(current);
        if (GUILayout.Button("Grabbit Fall Current"))
            ScheduleDrop(layout, layout.MainVolume, current);
        if (GUILayout.Button("Bake Current Batch"))
            BakeFolder(current, "Bake Main Batch");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Delete Current Batch"))
            DeleteBatch(layout, _selectedBatchIndex);
        if (GUILayout.Button("Delete Selected Batch"))
        {
            if (Selection.activeTransform != null && Selection.activeTransform.name.StartsWith(PhysicsLevelLayout.BatchPrefix))
                DeleteFolder(Selection.activeTransform, "Delete Selected Batch");
        }
        EditorGUILayout.EndHorizontal();

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
            "After Generate: Grabbit Fall Current → hold Left Shift in the Scene view until piles settle → release to freeze → Bake. "
            + "Limitation Zone is sized to the spawn volume so older batches stay kinematic. "
            + "Fall uses a primitive floor collider so thin cards cannot tunnel through the MeshCollider floor.",
            MessageType.None);
        EditorGUILayout.EndVertical();
    }

    PhysicsCardSpawnVolume ObjectFieldVolume(string label, PhysicsCardSpawnVolume current)
    {
        return (PhysicsCardSpawnVolume)EditorGUILayout.ObjectField(label, current, typeof(PhysicsCardSpawnVolume), true);
    }

    void GenerateDemo(PhysicsLevelLayout layout)
    {
        if (layout.DemoVolume == null || layout.DemoCardsRoot == null)
        {
            EditorUtility.DisplayDialog("Demo", "Demo spawn volume / Demo_Cards folder missing.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Generate Demo Cards");
        int undo = Undo.GetCurrentGroup();
        DeleteArea(layout.DemoCardsRoot, "Clear Demo Cards", registerUndoOnly: true);
        SpawnGroup(
            layout,
            layout.DemoVolume,
            layout.DemoCardsRoot,
            PhysicsLevelItem.AreaKind.Demo,
            batchIndex: 0,
            layout.DemoRegularCount,
            layout.DemoPsaCount,
            layout.DemoPackCount);
        Undo.CollapseUndoOperations(undo);
        MarkDirty(layout);
        SelectChildren(layout.DemoCardsRoot);
    }

    void CreateMainBatch(PhysicsLevelLayout layout, int batchIndex)
    {
        if (layout.MainVolume == null || layout.MainLevelRoot == null)
        {
            EditorUtility.DisplayDialog("Main Level", "Main spawn volume / Main_Level folder missing.", "OK");
            return;
        }

        Transform folder = FindBatch(layout, batchIndex);
        if (folder != null && folder.childCount > 0)
        {
            if (!EditorUtility.DisplayDialog(
                    "Batch exists",
                    PhysicsLevelLayout.FormatBatchName(batchIndex) + " already has cards. Replace this batch only?",
                    "Replace",
                    "Cancel"))
                return;

            DeleteFolder(folder, "Replace Main Batch", registerUndoOnly: true);
        }

        folder = GetOrCreateBatchFolder(layout, batchIndex);
        Undo.SetCurrentGroupName("Create Main Batch");
        int undo = Undo.GetCurrentGroup();
        SpawnGroup(
            layout,
            layout.MainVolume,
            folder,
            PhysicsLevelItem.AreaKind.Main,
            batchIndex,
            layout.MainBatchSize,
            layout.MainPsaCount,
            layout.MainPackCount);
        Undo.CollapseUndoOperations(undo);
        MarkDirty(layout);
        SelectChildren(folder);
    }

    void SpawnGroup(
        PhysicsLevelLayout layout,
        PhysicsCardSpawnVolume volume,
        Transform parent,
        PhysicsLevelItem.AreaKind area,
        int batchIndex,
        int regularCount,
        int psaCount,
        int packCount)
    {
        CardArtLibrary.EnsureLoaded();
        var occupied = new List<Vector3>(regularCount + psaCount + packCount);

        List<CardDefinition> regulars = CardScatterUtility.AllocateLiveGroundCards(regularCount);
        for (int i = 0; i < regulars.Count; i++)
        {
            WorldCard card = CardFactory.CreateWorldCard(
                NextSpawnPose(layout, volume, occupied, out Quaternion rotation),
                rotation,
                regulars[i],
                paletteIndex: 0,
                cardName: "Card_" + regulars[i].DefinitionId);
            FinishCard(card, parent, area, batchIndex);
        }

        for (int i = 0; i < psaCount; i++)
        {
            int slot = PsaArtLibrary.CabinetSlotNumbers[i % PsaArtLibrary.CabinetSlotCount];
            WorldCard card = CardFactory.CreateWorldPsaCard(
                NextSpawnPose(layout, volume, occupied, out Quaternion rotation),
                rotation,
                slot,
                variantIndex: 1,
                cardName: "PSA_" + slot + "_1");
            FinishCard(card, parent, area, batchIndex);
        }

        if (packCount <= 0)
            return;

        int packedCards = packCount * CardDimensions.CardsPerBoosterPack;
        List<CardDefinition> packPool = CardScatterUtility.AllocateLiveGroundCards(packedCards);
        BoosterPackDefinition packDefinition = Resources.Load<BoosterPackDefinition>("Cards/BoosterPackDefinition");
        for (int i = 0; i < packCount; i++)
        {
            var contents = new List<CardDefinition>(CardDimensions.CardsPerBoosterPack);
            int start = i * CardDimensions.CardsPerBoosterPack;
            for (int c = 0; c < CardDimensions.CardsPerBoosterPack && start + c < packPool.Count; c++)
                contents.Add(packPool[start + c]);

            WorldBoosterPack pack = PackFactory.CreateWorldPack(
                NextSpawnPose(layout, volume, occupied, out Quaternion rotation),
                rotation,
                packDefinition,
                packName: "BoosterPack_" + (i + 1),
                packVariantIndex: (i % 5) + 1,
                preRolledContents: contents);
            FinishPack(pack, parent, area, batchIndex);
        }
    }

    Vector3 NextSpawnPose(
        PhysicsLevelLayout layout,
        PhysicsCardSpawnVolume volume,
        List<Vector3> occupied,
        out Quaternion rotation)
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
        float spin = 360f * layout.RotationRandomness;
        rotation = Quaternion.Euler(
            Random.Range(0f, spin),
            Random.Range(0f, spin),
            Random.Range(0f, spin));
        return point;
    }

    void FinishCard(WorldCard card, Transform parent, PhysicsLevelItem.AreaKind area, int batchIndex)
    {
        Undo.RegisterCreatedObjectUndo(card.gameObject, "Spawn Physics Card");
        Undo.SetTransformParent(card.transform, parent, "Parent Physics Card");
        card.PrepareEditorPhysicsPlacement();
        PhysicsLevelItem item = Undo.AddComponent<PhysicsLevelItem>(card.gameObject);
        item.Configure(area, batchIndex, isBaked: false);
        EditorUtility.SetDirty(card);
    }

    void FinishPack(WorldBoosterPack pack, Transform parent, PhysicsLevelItem.AreaKind area, int batchIndex)
    {
        Undo.RegisterCreatedObjectUndo(pack.gameObject, "Spawn Physics Pack");
        Undo.SetTransformParent(pack.transform, parent, "Parent Physics Pack");
        pack.PrepareEditorPhysicsPlacement();
        PhysicsLevelItem item = Undo.AddComponent<PhysicsLevelItem>(pack.gameObject);
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

        WorldCard card = item.GetComponent<WorldCard>();
        if (card != null)
        {
            if (!string.IsNullOrEmpty(undoName))
                Undo.RecordObject(card, undoName);
            card.StripEditorRigidbody();
            card.ApplySolidEditorCollider();
        }

        WorldBoosterPack pack = item.GetComponent<WorldBoosterPack>();
        if (pack != null)
        {
            if (!string.IsNullOrEmpty(undoName))
                Undo.RecordObject(pack, undoName);
            pack.StripEditorRigidbody();
            pack.PrepareEditorPhysicsPlacement();
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

    void DeleteArea(Transform folder, string undoName, bool registerUndoOnly = false)
    {
        if (folder == null)
            return;

        for (int i = folder.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(folder.GetChild(i).gameObject);

        if (!registerUndoOnly)
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    void DeleteFolder(Transform folder, string undoName, bool registerUndoOnly = false)
    {
        if (folder == null)
            return;

        Undo.DestroyObjectImmediate(folder.gameObject);
        if (!registerUndoOnly)
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    void DeleteBatch(PhysicsLevelLayout layout, int batchIndex)
    {
        Transform folder = FindBatch(layout, batchIndex);
        if (folder == null)
            return;

        DeleteFolder(folder, "Delete Current Batch");
    }

    Transform FindBatch(PhysicsLevelLayout layout, int batchIndex)
    {
        if (layout.MainLevelRoot == null)
            return null;

        string name = PhysicsLevelLayout.FormatBatchName(batchIndex);
        return layout.MainLevelRoot.Find(name);
    }

    Transform GetOrCreateBatchFolder(PhysicsLevelLayout layout, int batchIndex)
    {
        Transform existing = FindBatch(layout, batchIndex);
        if (existing != null)
            return existing;

        var go = new GameObject(PhysicsLevelLayout.FormatBatchName(batchIndex));
        Undo.RegisterCreatedObjectUndo(go, "Create Batch Folder");
        Undo.SetTransformParent(go.transform, layout.MainLevelRoot, "Parent Batch Folder");
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    int NextFreeBatchIndex(PhysicsLevelLayout layout)
    {
        int index = 1;
        while (FindBatch(layout, index) != null && FindBatch(layout, index).childCount > 0)
            index++;
        return index;
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
