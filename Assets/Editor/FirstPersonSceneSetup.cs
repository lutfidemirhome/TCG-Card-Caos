using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click test scene: player, camera, ground, light.
/// Menu: TCG Card Caos → Setup First Person Playground
/// </summary>
public static class FirstPersonSceneSetup
{
    const string ScenePath = "Assets/Scenes/MainScene.unity";
    const string PrefabPath = "Assets/Prefabs/FirstPersonPlayer.prefab";

    [MenuItem("TCG Card Caos/Setup First Person Playground")]
    public static void SetupPlayground()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Prefabs");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        RemoveDefaultMainCamera();
        CreateGround();
        CreateDirectionalLightIfNeeded();
        GameObject player = CreatePlayer();
        CreateTestCards();
        Selection.activeGameObject = player;

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        var buildScenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        EditorBuildSettings.scenes = buildScenes;

        Debug.Log("TCG Card Caos: First-person playground ready. Press Play to test WASD + mouse look. Escape unlocks cursor.");
    }

    [MenuItem("TCG Card Caos/Fix Current Scene")]
    public static void FixCurrentScene()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("No Main Camera in the open scene.");
            return;
        }

        if (camera.GetComponent<CrosshairUI>() == null)
            camera.gameObject.AddComponent<CrosshairUI>();

        if (camera.GetComponent<InteractionController>() == null)
            camera.gameObject.AddComponent<InteractionController>();

        FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();
        if (player != null && player.GetComponent<PlayerCardHand>() == null)
            player.gameObject.AddComponent<PlayerCardHand>();

        GameObject oldCube = GameObject.Find("TestInteractable");
        if (oldCube != null)
            Object.DestroyImmediate(oldCube);

        CardScatterUtility.ClearTestCards();
        EnsureCardArtReady();
        CardScatterUtility.SpawnScatteredCards();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        MaterialFixEditor.FixPinkMaterials();
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
        if (main != null)
            Object.DestroyImmediate(main.gameObject);
    }

    static void CreateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        ground.isStatic = true;
        ground.GetComponent<MeshRenderer>().sharedMaterial =
            MaterialFixEditor.GetOrCreateLitMaterial("Assets/Art/Materials/Ground.mat", new Color(0.35f, 0.32f, 0.28f));
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
        directionalLight.shadows = LightShadows.None;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    static void CreateTestCards()
    {
        CardScatterUtility.SpawnScatteredCards();
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
