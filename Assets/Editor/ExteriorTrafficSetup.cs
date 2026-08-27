using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds ambient street traffic in front of the supermarket door using AE_New_York cars.
/// Menu: TCG Card Chaos → Add Exterior Traffic
/// </summary>
public static class ExteriorTrafficSetup
{
    const string ScenePath = "Assets/Scenes/MainScene.unity";
    const string RootName = "ExteriorTraffic";

    static readonly string[] CarPrefabPaths =
    {
        "Assets/AE_New_York/Prefabs/Car/Sedan_Taxi.prefab",
        "Assets/AE_New_York/Prefabs/Car/Sedan_Victoria.prefab",
        "Assets/AE_New_York/Prefabs/Car/Minivan_Maestro.prefab",
        "Assets/AE_New_York/Prefabs/Car/Minivan_Builders.prefab",
    };

    [MenuItem("TCG Card Chaos/Add Exterior Traffic")]
    public static void AddExteriorTraffic()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Transform existing = FindRoot();
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Exterior Traffic zaten var",
                "Sahnedeki mevcut ExteriorTraffic silinip yeniden kurulsun mu?",
                "Yeniden kur",
                "Iptal");

            if (!replace)
            {
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            Object.DestroyImmediate(existing.gameObject);
        }

        Vector3 roadCenter = FindRoadCenter();
        GameObject root = new GameObject(RootName);
        ExteriorTrafficSpawner spawner = root.AddComponent<ExteriorTrafficSpawner>();

        ExteriorTrafficPath pathMain = CreatePath(
            root.transform,
            "Path_Main",
            roadCenter + new Vector3(-30f, 0f, 0f),
            roadCenter + new Vector3(55f, 0f, 0f));

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        serializedSpawner.FindProperty("carPrefabs").arraySize = CarPrefabPaths.Length;
        for (int i = 0; i < CarPrefabPaths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPaths[i]);
            serializedSpawner.FindProperty("carPrefabs").GetArrayElementAtIndex(i).objectReferenceValue = prefab;
        }

        SerializedProperty pathsProperty = serializedSpawner.FindProperty("paths");
        pathsProperty.arraySize = 1;
        pathsProperty.GetArrayElementAtIndex(0).objectReferenceValue = pathMain;
        serializedSpawner.FindProperty("alternateDirection").boolValue = true;
        serializedSpawner.FindProperty("maxActiveCars").intValue = 1;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Exterior Traffic eklendi",
            "Kapinin onundeki yola tek serit trafik kuruldu.\n\n"
            + "Path_Main altina Start / donus / End noktalari koy.\n"
            + "Hierarchy sirasi rotayi belirler; Scene'de mavi cizgiyi takip et.\n\n"
            + "Play'e basinca trafik baslar.",
            "Tamam");
    }

    static Transform FindRoot()
    {
        GameObject existing = GameObject.Find(RootName);
        return existing != null ? existing.transform : null;
    }

    static Vector3 FindRoadCenter()
    {
        Transform door = GameObject.Find("Door_c5bu08")?.transform;
        if (door != null)
            return door.position + door.forward * 7f + Vector3.up * 0.15f;

        return new Vector3(2f, 0.15f, -21f);
    }

    static ExteriorTrafficPath CreatePath(Transform parent, string pathName, Vector3 start, Vector3 end)
    {
        GameObject pathObject = new GameObject(pathName);
        pathObject.transform.SetParent(parent, false);
        ExteriorTrafficPath path = pathObject.AddComponent<ExteriorTrafficPath>();

        Transform startPoint = CreateWaypoint(pathObject.transform, "Start", start);
        Transform endPoint = CreateWaypoint(pathObject.transform, "End", end);

        SerializedObject serializedPath = new SerializedObject(path);
        SerializedProperty waypoints = serializedPath.FindProperty("waypoints");
        waypoints.arraySize = 2;
        waypoints.GetArrayElementAtIndex(0).objectReferenceValue = startPoint;
        waypoints.GetArrayElementAtIndex(1).objectReferenceValue = endPoint;
        serializedPath.ApplyModifiedPropertiesWithoutUndo();

        return path;
    }

    static Transform CreateWaypoint(Transform parent, string name, Vector3 position)
    {
        GameObject point = new GameObject(name);
        point.transform.SetParent(parent, false);
        point.transform.position = position;
        return point.transform;
    }
}
