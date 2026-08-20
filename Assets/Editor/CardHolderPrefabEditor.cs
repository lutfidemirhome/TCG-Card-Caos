using System.IO;
using UnityEditor;
using UnityEngine;

public static class CardHolderPrefabEditor
{
    const string Tutucu1ModelPath = "Assets/Art/PsaCabinet/CardHolders/kart_tutucu_1/plastic_card_stand.fbx";
    static readonly string[] Tutucu2ModelPaths =
    {
        "Assets/Art/PsaCabinet/CardHolders/kart_tutucu_2/trading_card_stand.glb",
        "Assets/Art/PsaCabinet/CardHolders/kart_tutucu_2/scene.gltf",
    };
    const string Tutucu1PrefabPath = "Assets/Prefabs/PsaCabinet/KartTutucu_1.prefab";
    const string Tutucu2PrefabPath = "Assets/Prefabs/PsaCabinet/KartTutucu_2.prefab";

    static readonly Vector3 Tutucu2VisualScale = Vector3.one * 0.007579446f;
    static readonly Vector3 Tutucu2VisualEuler = new Vector3(-89.715f, 0f, -90f);

    [MenuItem("TCG Card Caos/Create PSA Card Holder Prefabs")]
    public static void CreateOrUpdatePrefabs()
    {
        CreateOrUpdatePrefab(
            Tutucu1ModelPath,
            Tutucu1PrefabPath,
            "KartTutucu_1",
            "Plastic card stand (kart_tutucu_1)",
            visualScale: Vector3.one,
            visualEuler: Vector3.zero);

        CreateOrUpdatePrefab(
            Tutucu2ModelPaths,
            Tutucu2PrefabPath,
            "KartTutucu_2",
            "Trading card stand (kart_tutucu_2)",
            visualScale: Tutucu2VisualScale,
            visualEuler: Tutucu2VisualEuler);

        CreateOrUpdatePrefab(
            Tutucu2ModelPaths,
            Tutucu1PrefabPath,
            "KartTutucu_1",
            "PSA card holder (tutucu_2 mesh in KartTutucu_1)",
            visualScale: Tutucu2VisualScale,
            visualEuler: Tutucu2VisualEuler);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TCG Card Caos: PSA card holder prefabs ready under Assets/Prefabs/PsaCabinet/");
    }

    static void CreateOrUpdatePrefab(
        string modelPath,
        string prefabPath,
        string rootName,
        string logLabel,
        Vector3 visualScale,
        Vector3 visualEuler)
    {
        CreateOrUpdatePrefab(new[] { modelPath }, prefabPath, rootName, logLabel, visualScale, visualEuler);
    }

    static void CreateOrUpdatePrefab(
        string[] modelPaths,
        string prefabPath,
        string rootName,
        string logLabel,
        Vector3 visualScale,
        Vector3 visualEuler)
    {
        GameObject modelPrefab = LoadModelPrefab(modelPaths);
        if (modelPrefab == null)
        {
            Debug.LogError("Card holder model not found. Tried:\n- " + string.Join("\n- ", modelPaths));
            return;
        }

        var root = new GameObject(rootName);
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root.transform);
        visual.name = "Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(visualEuler);
        visual.transform.localScale = visualScale;

        var slotMarker = new GameObject("PsaSlotMarker");
        slotMarker.transform.SetParent(root.transform, false);
        slotMarker.transform.localPosition = Vector3.zero;
        slotMarker.transform.localRotation = Quaternion.identity;
        slotMarker.AddComponent<PsaCabinetSlot>();

        EnsureFolder("Assets/Prefabs/PsaCabinet");
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Selection.activeObject = prefabAsset;
        EditorGUIUtility.PingObject(prefabAsset);
        Debug.Log($"TCG Card Caos: Created {logLabel} prefab at {prefabPath}");
    }

    static GameObject LoadModelPrefab(string[] modelPaths)
    {
        for (int i = 0; i < modelPaths.Length; i++)
        {
            string path = modelPaths[i];
            if (!File.Exists(path))
                continue;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (modelPrefab != null)
                return modelPrefab;
        }

        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
