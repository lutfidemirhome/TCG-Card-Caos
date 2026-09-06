using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class LedLambaPrefabEditor
{
    const string ModelPath = "Assets/Art/Models/LedLamba/LedLamba.fbx";
    const string PrefabPath = "Assets/Prefabs/Environment/LedLamba.prefab";
    const string BodyMaterialPath = "Assets/Art/Materials/LedLambaBody.mat";
    const string EmissiveMaterialPath = "Assets/Art/Materials/LedLightWarmEmissive.mat";
    const string CookiePath = "Assets/PolyKebap/LED Light Essentials Pack/Textures/Cookie.png";

    [MenuItem("TCG Card Chaos/Create Led Lamba Prefab")]
    public static void CreateOrUpdatePrefabFromMenu()
    {
        GameObject prefab = CreateOrUpdatePrefab();
        EditorUtility.DisplayDialog(
            "Led Lamba",
            prefab != null
                ? $"Prefab hazir:\n{PrefabPath}"
                : "Led Lamba prefab olusturulamadi.",
            "Tamam");
    }

    public static void BatchCreatePrefab()
    {
        GameObject prefab = CreateOrUpdatePrefab();
        if (prefab == null)
        {
            Debug.LogError("Led Lamba prefab olusturulamadi.");
            EditorApplication.Exit(1);
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Led Lamba prefab saved: " + PrefabPath);
        EditorApplication.Exit(0);
    }

    static GameObject CreateOrUpdatePrefab()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("Model yuklenemedi: " + ModelPath);
            return null;
        }

        Material bodyMaterial = EnsureBodyMaterial();
        Material emissiveMaterial = AssetDatabase.LoadAssetAtPath<Material>(EmissiveMaterialPath);
        Texture cookie = AssetDatabase.LoadAssetAtPath<Texture>(CookiePath);

        GameObject instance = Object.Instantiate(modelAsset);
        instance.name = "LedLamba";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        ApplyMaterials(instance, bodyMaterial, emissiveMaterial);
        OptimizeRenderers(instance);
        EnsureStripLight(instance, cookie);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    static void ApplyMaterials(GameObject root, Material bodyMaterial, Material emissiveMaterial)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            Material[] slots = renderer.sharedMaterials;
            for (int m = 0; m < slots.Length; m++)
            {
                if (IsEmissiveSlot(renderer.gameObject.name, m, slots.Length))
                    slots[m] = emissiveMaterial;
                else
                    slots[m] = bodyMaterial;
            }

            renderer.sharedMaterials = slots;
        }
    }

    static bool IsEmissiveSlot(string objectName, int slotIndex, int slotCount)
    {
        string lower = objectName.ToLowerInvariant();
        if (lower.Contains("light") || lower.Contains("bulb") || lower.Contains("lamp") || lower.Contains("led"))
            return true;

        return slotCount > 1 && slotIndex == slotCount - 1;
    }

    static void OptimizeRenderers(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }

    static void EnsureStripLight(GameObject root, Texture cookie)
    {
        LedStripSpotLight stripLight = root.GetComponent<LedStripSpotLight>();
        if (stripLight == null)
            stripLight = root.AddComponent<LedStripSpotLight>();

        SerializedObject serialized = new SerializedObject(stripLight);
        serialized.FindProperty("cookie").objectReferenceValue = cookie;
        serialized.FindProperty("intensity").floatValue = 3.5f;
        serialized.FindProperty("range").floatValue = 8f;
        serialized.FindProperty("spotAngle").floatValue = 100f;
        serialized.FindProperty("localLightEuler").vector3Value = new Vector3(75f, 0f, 0f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        stripLight.Refresh();
    }

    static Material EnsureBodyMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, BodyMaterialPath);
        }

        Color bodyColor = new Color(0.12f, 0.12f, 0.13f, 1f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", bodyColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", bodyColor);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.35f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.45f);

        EditorUtility.SetDirty(material);
        return material;
    }
}
