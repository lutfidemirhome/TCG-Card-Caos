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
            if (cards[i].name.StartsWith(CardScatterUtility.TestCardPrefix))
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
