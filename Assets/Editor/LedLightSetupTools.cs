using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class LedLightSetupTools
{
    const string ScenePath = "Assets/Scenes/MainScene.unity";
    const string SourcePrefabPath =
        "Assets/PolyKebap/LED Light Essentials Pack/Prefabs/ONEmissiveOnly/LED_Light_3.prefab";
    const string OutputPrefabPath = "Assets/Prefabs/Environment/LED_Light_3.prefab";
    const string EmissiveMaterialPath = "Assets/Art/Materials/LedLightWarmEmissive.mat";
    const string CookiePath = "Assets/PolyKebap/LED Light Essentials Pack/Textures/Cookie.png";

    const float StripSpacing = 2.05f;
    static readonly Vector3 SampleLocalPosition = new(0f, 2.85f, -6.5f);
    static readonly Vector3 SampleLocalEuler = new(0f, 0f, 0f);

    [MenuItem("TCG Card Chaos/Setup LED Light 3 Prefab")]
    public static void SetupLedLightPrefabFromMenu()
    {
        GameObject prefab = BuildLedLightPrefab();
        EditorUtility.DisplayDialog(
            "LED Light 3",
            prefab != null
                ? $"Prefab guncellendi:\nEmission + 1 spot light (strip basina en ucuz aydinlatma)\n{OutputPrefabPath}"
                : "LED prefab olusturulamadi.",
            "Tamam");
    }

    [MenuItem("TCG Card Chaos/Place Sample LED Light 3")]
    public static void PlaceSampleLedFromMenu()
    {
        OpenMainSceneIfNeeded();
        GameObject prefab = BuildLedLightPrefab();
        if (prefab == null)
            return;

        Transform room = FindRoomTransform();
        GameObject placed = PlaceLedInstance(prefab, room, SampleLocalPosition, SampleLocalEuler, "LED_Light_3_Sample");
        Selection.activeGameObject = placed;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "LED Light 3",
            "Ornek LED eklendi.\nEmission + tek spot light kullanir.",
            "Tamam");
    }

    [MenuItem("TCG Card Chaos/Place LED Light 3 Row (5)")]
    public static void PlaceLedRowFromMenu()
    {
        OpenMainSceneIfNeeded();
        GameObject prefab = BuildLedLightPrefab();
        if (prefab == null)
            return;

        Transform room = FindRoomTransform();
        Vector3 start = SampleLocalPosition + Vector3.left * StripSpacing * 2f;
        for (int i = 0; i < 5; i++)
        {
            Vector3 localPos = start + Vector3.right * (StripSpacing * i);
            PlaceLedInstance(prefab, room, localPos, SampleLocalEuler, $"LED_Light_3 ({i + 1})");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("TCG Card Chaos/Add Strip Light To Selected LEDs")]
    public static void AddStripLightToSelection()
    {
        Texture cookie = AssetDatabase.LoadAssetAtPath<Texture>(CookiePath);
        Material emissiveMaterial = AssetDatabase.LoadAssetAtPath<Material>(EmissiveMaterialPath);
        int updated = 0;

        foreach (GameObject selected in Selection.gameObjects)
        {
            if (!selected.name.Contains("LED_Light"))
                continue;

            ApplyVisualSettings(selected, emissiveMaterial);
            EnsureStripLightComponent(selected, cookie);
            EditorUtility.SetDirty(selected);
            updated++;
        }

        EditorUtility.DisplayDialog(
            "LED strip light",
            updated > 0
                ? $"{updated} LED guncellendi (emission + spot light)."
                : "Secili objeler arasinda LED_Light bulunamadi.",
            "Tamam");
    }

    public static void BatchSetupAndPlaceSampleLed()
    {
        OpenMainSceneIfNeeded();
        GameObject prefab = BuildLedLightPrefab();
        if (prefab == null)
        {
            Debug.LogError("LED prefab setup failed.");
            EditorApplication.Exit(1);
            return;
        }

        Transform room = FindRoomTransform();
        PlaceLedInstance(prefab, room, SampleLocalPosition, SampleLocalEuler, "LED_Light_3_Sample");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("LED Light 3 prefab and sample instance saved.");
        EditorApplication.Exit(0);
    }

    static GameObject BuildLedLightPrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        Material emissiveMaterial = AssetDatabase.LoadAssetAtPath<Material>(EmissiveMaterialPath);
        Texture cookie = AssetDatabase.LoadAssetAtPath<Texture>(CookiePath);
        if (source == null || emissiveMaterial == null)
        {
            Debug.LogError("LED source prefab or emissive material missing.");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
            return null;

        instance.name = "LED_Light_3";
        ApplyVisualSettings(instance, emissiveMaterial);
        EnsureStripLightComponent(instance, cookie);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, OutputPrefabPath);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    static void ApplyVisualSettings(GameObject root, Material emissiveMaterial)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            if (renderer.gameObject.name == "light_ON")
                renderer.sharedMaterial = emissiveMaterial;
        }

        Light[] legacyLights = root.GetComponentsInChildren<Light>(true);
        for (int i = legacyLights.Length - 1; i >= 0; i--)
        {
            if (legacyLights[i].GetComponent<LedStripSpotLight>() != null)
                continue;

            Object.DestroyImmediate(legacyLights[i].gameObject);
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = colliders.Length - 1; i >= 0; i--)
        {
            if (colliders[i].gameObject.name is "glass" or "light_ON")
                Object.DestroyImmediate(colliders[i]);
        }
    }

    static void EnsureStripLightComponent(GameObject root, Texture cookie)
    {
        LedStripSpotLight stripLight = root.GetComponent<LedStripSpotLight>();
        if (stripLight == null)
            stripLight = Undo.AddComponent<LedStripSpotLight>(root);

        SerializedObject serialized = new SerializedObject(stripLight);
        serialized.FindProperty("cookie").objectReferenceValue = cookie;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        stripLight.Refresh();
    }

    static GameObject PlaceLedInstance(
        GameObject prefab,
        Transform parent,
        Vector3 localPosition,
        Vector3 localEuler,
        string objectName)
    {
        GameObject placed = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        placed.name = objectName;
        Transform transform = placed.transform;
        transform.SetParent(parent, false);
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(localEuler);
        return placed;
    }

    static Transform FindRoomTransform()
    {
        GameObject room = GameObject.Find("Room");
        return room != null ? room.transform : null;
    }

    static void OpenMainSceneIfNeeded()
    {
        if (EditorSceneManager.GetActiveScene().path == ScenePath)
            return;

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }
}
