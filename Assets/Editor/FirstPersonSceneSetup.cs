using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Empty card-shop sandbox: floor, walls, player, light, a few test cards.
/// Drop ModernSupermarket prefabs (cabinets, shelves, etc.) into this scene yourself.
/// Menu: TCG Card Caos → Create Empty Card Shop Scene
/// </summary>
public static class FirstPersonSceneSetup
{
    const string ScenePath = "Assets/Scenes/MainScene.unity";
    const string PrefabPath = "Assets/Prefabs/FirstPersonPlayer.prefab";

    // Plane is 10 units; scale 2 → ~20x20 m (fits 100-card scatter + furniture).
    const float FloorScale = 2f;
    const float WallHeight = 3f;

    [MenuItem("TCG Card Caos/Create Empty Card Shop Scene")]
    [MenuItem("TCG Card Caos/Setup First Person Playground")]
    public static void SetupPlayground()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Prefabs");

        if (System.IO.File.Exists(ScenePath))
        {
            // Keep walking settings + existing ~100 cards; only rebuild the empty room shell.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            UpgradeToEmptyRoomKeepingGameplay(scene);
            return;
        }

        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        RemoveDefaultMainCamera();
        CreateEmptyRoom();
        CreateDirectionalLightIfNeeded();

        GameObject player = CreatePlayer();
        WirePlayerGameplay(player);

        CardInstancedRenderManager.EnsureExists();
        EnsureCardArtReady();
        CardScatterUtility.ClearTestCards();
        CardScatterUtility.SpawnScatteredCards(CardScatterUtility.DefaultScatterCount);

        Selection.activeGameObject = player;
        FinishEmptyShopScene(newScene);
    }

    static void UpgradeToEmptyRoomKeepingGameplay(UnityEngine.SceneManagement.Scene scene)
    {
        DestroyNamedRoots("Room", "Ground", "Floor");
        CreateEmptyRoom();
        CreateDirectionalLightIfNeeded();

        FirstPersonController existingPlayer = Object.FindFirstObjectByType<FirstPersonController>();
        GameObject player = existingPlayer != null ? existingPlayer.gameObject : CreatePlayer();
        WirePlayerGameplay(player);

        CardInstancedRenderManager.EnsureExists();
        EnsureCardArtReady();

        int cardCount = CountTestCards();
        if (cardCount != CardScatterUtility.DefaultScatterCount)
        {
            CardScatterUtility.ClearTestCards();
            CardScatterUtility.SpawnScatteredCards(CardScatterUtility.DefaultScatterCount);
        }

        Selection.activeGameObject = player;
        FinishEmptyShopScene(scene);
    }

    static void WirePlayerGameplay(GameObject player)
    {
        Camera camera = player.GetComponentInChildren<Camera>();
        if (camera == null)
        {
            Debug.LogError("TCG Card Caos: Player has no camera.");
            return;
        }

        camera.tag = "MainCamera";
        if (camera.GetComponent<CrosshairUI>() == null)
            camera.gameObject.AddComponent<CrosshairUI>();
        if (camera.GetComponent<InteractionController>() == null)
            camera.gameObject.AddComponent<InteractionController>();
        if (camera.GetComponent<AudioListener>() == null)
            camera.gameObject.AddComponent<AudioListener>();
        CardInspectPreview.EnsureOn(camera);

        if (player.GetComponent<PlayerCardHand>() == null)
            player.AddComponent<PlayerCardHand>();

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera other in cameras)
        {
            if (other == camera)
                continue;
            other.enabled = false;
            other.tag = "Untagged";
            AudioListener listener = other.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
        }
    }

    static void FinishEmptyShopScene(UnityEngine.SceneManagement.Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        var buildScenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        EditorBuildSettings.scenes = buildScenes;

        Debug.Log(
            "TCG Card Caos: Empty card shop ready at "
            + ScenePath
            + " with walk settings + "
            + CardScatterUtility.DefaultScatterCount
            + " cards. Drop furniture from Assets/ModernSupermarket/Prefabs. Press Play to test.");
    }

    static int CountTestCards()
    {
        int count = 0;
        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            if (CardScatterUtility.IsScatterCardObject(cards[i].name))
                count++;
        }

        return count;
    }

    static void DestroyNamedRoots(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            GameObject go = GameObject.Find(names[i]);
            if (go != null)
                Object.DestroyImmediate(go);
        }
    }

    [MenuItem("TCG Card Caos/Setup Gameplay In Open Scene")]
    public static void SetupGameplayInOpenScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("TCG Card Caos: Open a scene first, then run this again.");
            return;
        }

        FirstPersonController existingPlayer = Object.FindFirstObjectByType<FirstPersonController>();
        GameObject player = existingPlayer != null ? existingPlayer.gameObject : CreatePlayer();
        player.transform.position = new Vector3(0f, 0f, 0f);

        Camera camera = player.GetComponentInChildren<Camera>();
        if (camera == null)
        {
            Debug.LogError("TCG Card Caos: Player has no camera.");
            return;
        }

        camera.tag = "MainCamera";
        if (camera.GetComponent<CrosshairUI>() == null)
            camera.gameObject.AddComponent<CrosshairUI>();
        if (camera.GetComponent<InteractionController>() == null)
            camera.gameObject.AddComponent<InteractionController>();
        if (camera.GetComponent<AudioListener>() == null)
            camera.gameObject.AddComponent<AudioListener>();
        CardInspectPreview.EnsureOn(camera);

        if (player.GetComponent<PlayerCardHand>() == null)
            player.AddComponent<PlayerCardHand>();

        // Disable other cameras so first-person stays primary.
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera other in cameras)
        {
            if (other == camera)
                continue;
            other.enabled = false;
            other.tag = "Untagged";
            AudioListener listener = other.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
        }

        CardInstancedRenderManager.EnsureExists();
        EnsureCardArtReady();
        CardScatterUtility.ClearTestCards();
        CardScatterUtility.SpawnScatteredCards();

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();

        var buildScenes = new[] { new EditorBuildSettingsScene(scene.path, true) };
        EditorBuildSettings.scenes = buildScenes;

        Debug.Log(
            "TCG Card Caos: Gameplay ready in '"
            + scene.name
            + "'. Player + 100 cards added. Delete unused shop props in Hierarchy, then Save. Press Play to test.");
    }

    [MenuItem("TCG Card Caos/Fix Current Scene")]
    public static void FixCurrentScene()
    {
        SetupGameplayInOpenScene();
        MaterialFixEditor.FixPinkMaterials();
    }

    [MenuItem("TCG Card Caos/Snap Cards To Floor")]
    public static void SnapCardsToFloorMenu()
    {
        CardFactory.InvalidateGroundCache();
        int snapped = CardScatterUtility.SnapCardsToFloor();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log(
            "Snapped "
            + snapped
            + " cards onto floor (surface Y="
            + CardFactory.GroundSurfaceY().ToString("0.###")
            + ").");
    }

    [MenuItem("TCG Card Caos/Setup Card Shelf On Selection")]
    public static void SetupCardShelfOnSelection()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("TCG Card Caos: Select your cabinet/shelf (scene or prefab), then run this again.");
            return;
        }

        CardShelf shelf = selected.GetComponent<CardShelf>();
        if (shelf == null)
            shelf = selected.AddComponent<CardShelf>();

        shelf.RefreshSlotCache();
        MarkShelfAuthoringDirty(selected);

        Debug.Log(
            "TCG Card Caos: CardShelf on '"
            + selected.name
            + "'. Add Sign_04h2fw as CategorySign child, assign category material, then create slot grid and save.");
    }

    [MenuItem("TCG Card Caos/Add Card Shelf Slot")]
    public static void AddCardShelfSlot()
    {
        GameObject parent = Selection.activeGameObject;
        if (parent == null)
        {
            Debug.LogWarning("TCG Card Caos: Select the cabinet (or a child) first.");
            return;
        }

        CardShelf shelf = parent.GetComponentInParent<CardShelf>();
        if (shelf == null)
        {
            shelf = parent.AddComponent<CardShelf>();
            Debug.Log("TCG Card Caos: Added CardShelf to '" + parent.name + "'.");
        }

        var slotGo = new GameObject("CardShelfSlot");
        Undo.RegisterCreatedObjectUndo(slotGo, "Add Card Shelf Slot");
        slotGo.transform.SetParent(shelf.transform, false);
        slotGo.transform.localPosition = new Vector3(0f, 1f, 0f);
        slotGo.transform.localRotation = Quaternion.identity;
        slotGo.AddComponent<CardShelfSlot>();

        shelf.RefreshSlotCache();
        Selection.activeGameObject = slotGo;
        MarkShelfAuthoringDirty(shelf.gameObject);
        Debug.Log("TCG Card Caos: Slot added. Move/rotate the yellow gizmo — blue arrow (forward) should face the room.");
    }

    [MenuItem("TCG Card Caos/Create Card Shelf Slot Grid")]
    public static void CreateCardShelfSlotGrid()
    {
        GameObject parent = Selection.activeGameObject;
        if (parent == null)
        {
            Debug.LogWarning("TCG Card Caos: Select the cabinet (or an existing ShelfSlots_Level) first.");
            return;
        }

        CardShelf shelf = parent.GetComponentInParent<CardShelf>();
        if (shelf == null)
            shelf = parent.AddComponent<CardShelf>();

        // One row of seats side-by-side along local X, with side margins (no depth stacking).
        int columns = Mathf.Clamp(shelf.SlotsPerRow, 1, CardShelfCategories.MaxSlotNumber);
        const float usableWidth = 1.8f; // ~0.1m inset each side on a ~2m board
        float spacingX = columns > 1 ? usableWidth / (columns - 1) : 0f;
        float originX = -0.5f * usableWidth;

        Transform levelRoot = null;
        if (parent.name.StartsWith("ShelfSlots_Level", System.StringComparison.Ordinal))
        {
            levelRoot = parent.transform;
            for (int i = levelRoot.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(levelRoot.GetChild(i).gameObject);
        }
        else
        {
            float startY = 1f;
            BoxCollider[] boxes = shelf.GetComponentsInChildren<BoxCollider>();
            float bestY = float.MaxValue;
            bool foundBoard = false;
            for (int i = 0; i < boxes.Length; i++)
            {
                BoxCollider box = boxes[i];
                if (box.size.y > 0.12f || Mathf.Max(box.size.x, box.size.z) < 0.25f)
                    continue;
                float top = box.transform.localPosition.y + box.center.y + box.size.y * 0.5f;
                if (top < bestY)
                {
                    bestY = top;
                    foundBoard = true;
                }
            }

            if (foundBoard)
                startY = bestY;

            var root = new GameObject("ShelfSlots_Level");
            Undo.RegisterCreatedObjectUndo(root, "Create Card Shelf Slot Grid");
            root.transform.SetParent(shelf.transform, false);
            root.transform.localPosition = new Vector3(0f, startY, 0f);
            root.transform.localRotation = Quaternion.identity;
            levelRoot = root.transform;
        }

        int levelRow = 0;
        if (levelRoot != null && CardShelfSlotNaming.TryParseLevelRowIndex(levelRoot.name, out int parsedRow))
            levelRow = parsedRow;

        for (int col = 0; col < columns; col++)
        {
            var slotGo = new GameObject(CardShelfSlotNaming.BuildName(levelRow, col));
            Undo.RegisterCreatedObjectUndo(slotGo, "Create Card Shelf Slot");
            slotGo.transform.SetParent(levelRoot, false);
            slotGo.transform.localPosition = new Vector3(originX + col * spacingX, 0f, 0f);
            slotGo.transform.localRotation = Quaternion.identity;
            slotGo.AddComponent<CardShelfSlot>();
        }

        shelf.RefreshSlotCache();
        Selection.activeGameObject = levelRoot.gameObject;
        MarkShelfAuthoringDirty(shelf.gameObject);
        Debug.Log(
            "TCG Card Caos: "
            + columns
            + " slots in one row (side margins on a "
            + usableWidth.ToString("0.##")
            + "m span). Duplicate 'ShelfSlots_Level' for other boards and move Y. Save the prefab.");
    }

    [MenuItem("TCG Card Caos/Equalize ShelfSlots Spacing")]
    [MenuItem("TCG Card Caos/Equalize Selected ShelfSlots Spacing")]
    public static void EqualizeSelectedShelfSlotsSpacing()
    {
        // Finds ShelfSlots_Level / ShelfSlots_Level (n) under selection (or whole scene).
        // Spacing = pos(Level1) - pos(Level0). Then pos(n) = pos(0) + n * delta.
        var levels = CollectShelfSlotLevelGroups(Selection.activeTransform);
        if (levels.Count < 2)
        {
            Debug.LogWarning(
                "TCG Card Caos: Need at least 'ShelfSlots_Level' and 'ShelfSlots_Level (1)' in the scene "
                + "(under the selected cabinet, or anywhere in the open scene).");
            return;
        }

        levels.Sort((a, b) => a.index.CompareTo(b.index));

        Transform baseLevel = null;
        Transform nextLevel = null;
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].index == 0)
                baseLevel = levels[i].transform;
            if (levels[i].index == 1)
                nextLevel = levels[i].transform;
        }

        if (baseLevel == null || nextLevel == null)
        {
            Debug.LogWarning(
                "TCG Card Caos: Found "
                + levels.Count
                + " groups, but missing base names. Keep one named exactly 'ShelfSlots_Level' and one 'ShelfSlots_Level (1)'.");
            return;
        }

        bool useLocal = baseLevel.parent != null && baseLevel.parent == nextLevel.parent;
        Vector3 basePos = useLocal ? baseLevel.localPosition : baseLevel.position;
        Vector3 nextPos = useLocal ? nextLevel.localPosition : nextLevel.position;
        Vector3 delta = nextPos - basePos;

        if (delta.sqrMagnitude < 0.0000001f)
        {
            Debug.LogWarning("TCG Card Caos: Level and Level (1) are in the same place — move Level (1) to set the gap first.");
            return;
        }

        var undoTargets = new UnityEngine.Object[levels.Count];
        for (int i = 0; i < levels.Count; i++)
            undoTargets[i] = levels[i].transform;
        Undo.RecordObjects(undoTargets, "Equalize ShelfSlots Spacing");

        for (int i = 0; i < levels.Count; i++)
        {
            int index = levels[i].index;
            Transform t = levels[i].transform;
            Vector3 target = basePos + delta * index;
            if (useLocal)
                t.localPosition = target;
            else
                t.position = target;
            EditorUtility.SetDirty(t);
        }

        MarkShelfAuthoringDirty(baseLevel.gameObject);
        Debug.Log(
            "TCG Card Caos: Equalized "
            + levels.Count
            + " ShelfSlots_Level groups. Step = "
            + delta
            + " ("
            + (useLocal ? "local" : "world")
            + "). Save the prefab/scene (Ctrl/Cmd+S).");
    }

    static System.Collections.Generic.List<(int index, Transform transform)> CollectShelfSlotLevelGroups(Transform hint)
    {
        var levels = new System.Collections.Generic.List<(int index, Transform transform)>();
        var seen = new System.Collections.Generic.HashSet<int>();

        void TryAdd(Transform t)
        {
            if (t == null || !TryParseShelfSlotsLevelIndex(t.name, out int index))
                return;
            if (!seen.Add(t.GetInstanceID()))
                return;
            levels.Add((index, t));
        }

        // 1) Under selected object / its CardShelf parent
        if (hint != null)
        {
            Transform root = hint;
            CardShelf shelf = hint.GetComponentInParent<CardShelf>();
            if (shelf != null)
                root = shelf.transform;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
                TryAdd(children[i]);
        }

        // 2) Fallback: whole open scene
        if (levels.Count < 2)
        {
            levels.Clear();
            seen.Clear();
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] children = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                    TryAdd(children[i]);
            }
        }

        return levels;
    }

    static bool TryParseShelfSlotsLevelIndex(string name, out int index)
    {
        index = 0;
        const string prefix = "ShelfSlots_Level";
        if (string.IsNullOrEmpty(name) || !name.StartsWith(prefix, System.StringComparison.Ordinal))
            return false;

        if (name.Length == prefix.Length)
            return true;

        // "ShelfSlots_Level (3)"
        int open = name.LastIndexOf('(');
        int close = name.LastIndexOf(')');
        if (open < 0 || close <= open)
            return false;

        return int.TryParse(name.Substring(open + 1, close - open - 1), out index);
    }

    [MenuItem("TCG Card Caos/Place Cabinet Normal Common In Scene")]
    public static void PlaceNormalCommonCabinetInScene()
    {
        PlaceCabinetInScene(
            "Assets/Prefabs/Cabinets/Cabinet_NormalCommon.prefab",
            "Cabinet_NormalCommon");
    }

    [MenuItem("TCG Card Caos/Place Cabinet Normal Uncommon In Scene")]
    public static void PlaceNormalUncommonCabinetInScene()
    {
        PlaceCabinetInScene(
            "Assets/Prefabs/Cabinets/Cabinet_NormalUncommon.prefab",
            "Cabinet_NormalUncommon");
    }

    static void PlaceCabinetInScene(string prefabPath, string instanceName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("TCG Card Caos: Missing cabinet prefab at " + prefabPath);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = instanceName;
        instance.transform.position = new Vector3(2f, 0f, 3f);

        FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();
        if (player != null)
        {
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            instance.transform.position = player.transform.position + forward.normalized * 2.2f;
            instance.transform.rotation = Quaternion.LookRotation(-forward.normalized, Vector3.up);
        }

        CardShelf shelf = instance.GetComponent<CardShelf>();
        if (shelf == null)
            shelf = instance.AddComponent<CardShelf>();
        shelf.RefreshSlotCache();

        Selection.activeGameObject = instance;
        MarkShelfAuthoringDirty(instance);
        Debug.Log(
            "TCG Card Caos: Placed "
            + instanceName
            + ". Edit the prefab at Assets/Prefabs/Cabinets/, then save.");
    }

    [MenuItem("TCG Card Caos/Place Cabinet Normal Common In Scene", true)]
    [MenuItem("TCG Card Caos/Place Cabinet Normal Uncommon In Scene", true)]
    static bool PlaceCabinetInSceneValidate()
    {
        return !Application.isPlaying;
    }

    static void MarkShelfAuthoringDirty(GameObject target)
    {
        if (target == null)
            return;

        EditorUtility.SetDirty(target);
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            EditorSceneManager.MarkSceneDirty(stage.scene);
            return;
        }

        if (!EditorSceneManager.GetActiveScene().IsValid())
            return;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("TCG Card Caos/Spawn Test Cards In Scene")]
    public static void SpawnTestCardsMenu()
    {
        EnsureCardArtReady();
        CardScatterUtility.ClearTestCards();
        CardScatterUtility.SpawnScatteredCards();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Spawned " + CardScatterUtility.DefaultScatterCount + " test cards on the ground.");
    }

    [MenuItem("TCG Card Caos/Spawn 5000 Stress Test Cards")]
    public static void SpawnStressTestCardsMenu()
    {
        EnsureCardArtReady();
        CardScatterUtility.ClearTestCards();
        CardScatterUtility.SpawnScatteredCards(CardScatterUtility.StressTestScatterCount);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Spawned " + CardScatterUtility.StressTestScatterCount + " instanced stress-test cards.");
    }

    [MenuItem("TCG Card Caos/Setup Card Art")]
    public static void SetupCardArtMenu()
    {
        CardArtSetup.SetupCardArt();
    }

    [MenuItem("TCG Card Caos/Respawn Cards With New Model")]
    public static void RespawnCardsWithNewModelMenu()
    {
        SpawnTestCardsMenu();
    }

    static void EnsureCardArtReady()
    {
        CardArtLibrary.ResetCache();
        CardArtLibrary.EnsureLoaded();
        if (CardArtLibrary.CardMesh != null)
            return;

        CardArtSetup.SetupCardArt();
        CardArtLibrary.ResetCache();
        CardArtLibrary.EnsureLoaded();
    }

    [MenuItem("TCG Card Caos/Add Crosshair To Scene")]
    public static void AddCrosshairToScene()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("No Main Camera in the scene.");
            return;
        }

        if (camera.GetComponent<CrosshairUI>() == null)
            camera.gameObject.AddComponent<CrosshairUI>();

        if (camera.GetComponent<InteractionController>() == null)
            camera.gameObject.AddComponent<InteractionController>();

        Debug.Log("Crosshair and interaction prompt added to Main Camera.");
    }

    [MenuItem("TCG Card Caos/Save First Person Player Prefab")]
    public static void SavePlayerPrefab()
    {
        FirstPersonController existing = Object.FindFirstObjectByType<FirstPersonController>();
        if (existing == null)
        {
            Debug.LogWarning("No FirstPersonController in the scene.");
            return;
        }

        EnsureFolder("Assets/Prefabs");
        PrefabUtility.SaveAsPrefabAsset(existing.gameObject, PrefabPath);
        Debug.Log("Saved player prefab to " + PrefabPath);
    }

    static void RemoveDefaultMainCamera()
    {
        Camera main = Camera.main;
        if (main == null)
            return;

        // Never delete the FPS player's camera when upgrading an existing scene.
        if (main.GetComponentInParent<FirstPersonController>() != null)
            return;

        Object.DestroyImmediate(main.gameObject);
    }

    static void CreateEmptyRoom()
    {
        Material floorMat = MaterialFixEditor.GetOrCreateLitMaterial(
            "Assets/Art/Materials/Ground.mat",
            new Color(0.35f, 0.32f, 0.28f));
        Material wallMat = MaterialFixEditor.GetOrCreateLitMaterial(
            "Assets/Art/Materials/Wall.mat",
            new Color(0.72f, 0.70f, 0.66f));

        var room = new GameObject("Room");

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Floor";
        ground.transform.SetParent(room.transform, false);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(FloorScale, 1f, FloorScale);
        ground.isStatic = true;
        ground.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

        // Unity Plane default size is 10x10; after scale the half-extent is 5 * FloorScale.
        float half = 5f * FloorScale;
        float wallThickness = 0.2f;
        float wallY = WallHeight * 0.5f;

        CreateWall(room.transform, "Wall_North", wallMat,
            new Vector3(0f, wallY, half),
            new Vector3(half * 2f + wallThickness, WallHeight, wallThickness));
        CreateWall(room.transform, "Wall_South", wallMat,
            new Vector3(0f, wallY, -half),
            new Vector3(half * 2f + wallThickness, WallHeight, wallThickness));
        CreateWall(room.transform, "Wall_East", wallMat,
            new Vector3(half, wallY, 0f),
            new Vector3(wallThickness, WallHeight, half * 2f));
        CreateWall(room.transform, "Wall_West", wallMat,
            new Vector3(-half, wallY, 0f),
            new Vector3(wallThickness, WallHeight, half * 2f));
    }

    static void CreateWall(Transform parent, string name, Material material, Vector3 position, Vector3 size)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.localScale = size;
        wall.isStatic = true;
        wall.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static void CreateDirectionalLightIfNeeded()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light existingLight in lights)
        {
            if (existingLight.type == LightType.Directional)
                return;
        }

        var lightGo = new GameObject("Directional Light");
        var directionalLight = lightGo.AddComponent<Light>();
        directionalLight.type = LightType.Directional;
        directionalLight.intensity = 1.1f;
        directionalLight.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    static GameObject CreatePlayer()
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0f, 0f);

        var controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.3f;
        controller.center = new Vector3(0f, 0.9f, 0f);

        player.AddComponent<FirstPersonController>();
        player.AddComponent<PlayerCardHand>();

        var cameraGo = new GameObject("Main Camera");
        cameraGo.transform.SetParent(player.transform, false);
        cameraGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        cameraGo.tag = "MainCamera";

        var camera = cameraGo.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        cameraGo.AddComponent<AudioListener>();
        cameraGo.AddComponent<CrosshairUI>();
        cameraGo.AddComponent<InteractionController>();

        return player;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folderName = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
