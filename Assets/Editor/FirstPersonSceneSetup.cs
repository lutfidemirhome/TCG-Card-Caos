using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Card-shop scene bootstrap: floor, walls, player, light, and the current card scatter.
/// Menu: TCG Card Caos → Create Empty Card Shop Scene
/// </summary>
public static class FirstPersonSceneSetup
{
    const string ScenePath = "Assets/Scenes/MainScene.unity";

    // Plane is 10 units; scale 2 → ~20x20 m (fits 100-card scatter + furniture).
    const float FloorScale = 2f;
    const float WallHeight = 3f;

    [MenuItem("TCG Card Caos/Create Empty Card Shop Scene")]
    public static void SetupPlayground()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Prefabs");

        if (System.IO.File.Exists(ScenePath))
        {
            // Keep walking settings + existing cards; only rebuild the empty room shell.
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
        CardScatterUtility.SpawnScatteredCards(CardScatterUtility.FullScatterCount);

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

        int cardCount = CardScatterUtility.CountScatterCards();
        if (cardCount != CardScatterUtility.FullScatterCount)
        {
            CardScatterUtility.ClearTestCards();
            CardScatterUtility.SpawnScatteredCards(CardScatterUtility.FullScatterCount);
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
            "TCG Card Caos: Card shop ready at "
            + ScenePath
            + " with walk settings + "
            + CardScatterUtility.FullScatterCount
            + " cards. Press Play to test.");
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
