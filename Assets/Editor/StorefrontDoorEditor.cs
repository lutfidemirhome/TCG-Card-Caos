#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class StorefrontDoorEditor
{
    const string DoorPrefabPath = "Assets/ModernSupermarket/Prefabs/Architecture/Door_c5bu08.prefab";
    const string ShopDoorPrefabPath = "Assets/Prefabs/Architecture/Door_c5bu08_Shop.prefab";

    [MenuItem("TCG Card Caos/Setup Selected Storefront Door")]
    public static void SetupSelectedDoor()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Storefront Door", "Once kapı objesini sec.", "Tamam");
            return;
        }

        Transform doorRoot = FindDoorRoot(selected.transform);
        StorefrontDoor setup = doorRoot.GetComponent<StorefrontDoor>();
        if (setup == null)
            setup = doorRoot.gameObject.AddComponent<StorefrontDoor>();

        setup.Rebuild();
        EditorUtility.SetDirty(doorRoot.gameObject);
        Debug.Log("StorefrontDoor ayarlandi: " + doorRoot.name);
    }

    [MenuItem("TCG Card Caos/Create Shop Door Prefab")]
    public static void CreateShopDoorPrefab()
    {
        EnsureFolder("Assets/Prefabs/Architecture");

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
        if (source == null)
        {
            Debug.LogError("Kapı prefab bulunamadi: " + DoorPrefabPath);
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
            return;

        StorefrontDoor setup = instance.GetComponent<StorefrontDoor>();
        if (setup == null)
            setup = instance.AddComponent<StorefrontDoor>();

        setup.Rebuild();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, ShopDoorPrefabPath);
        Object.DestroyImmediate(instance);

        Selection.activeObject = prefab;
        Debug.Log("Shop door prefab hazir: " + ShopDoorPrefabPath);
    }

    static Transform FindDoorRoot(Transform start)
    {
        Transform current = start;
        while (current != null)
        {
            if (current.name.Contains("Door_c5bu08"))
                return current;

            current = current.parent;
        }

        return start;
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
#endif
