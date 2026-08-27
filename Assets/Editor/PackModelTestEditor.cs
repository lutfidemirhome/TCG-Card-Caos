using System.Text;
using UnityEditor;
using UnityEngine;

public static class PackModelTestEditor
{
    const string PrefabPath = "Assets/Prefabs/BoosterPackModelTest.prefab";
    const string ModelAssetPath = "Assets/Art/BoosterPack/TradingCard_BoosterPack.fbx";

    [MenuItem("TCG Card Chaos/Create Pack Model Test Prefab")]
    public static void CreateOrUpdatePrefab()
    {
        CardArtLibrary.EnsureLoaded();

        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelAssetPath);
        if (modelPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Pack Model Test",
                "Model not found at:\n" + ModelAssetPath,
                "OK");
            return;
        }

        var root = new GameObject("BoosterPackModelTest");
        root.transform.localScale = Vector3.one * CardDimensions.GroundCardScale;

        var collider = root.AddComponent<BoxCollider>();
        PackFactory.ApplyFlatPackCollider(collider);

        var tuning = root.AddComponent<PackModelTestTuning>();

        GameObject visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        visualInstance.name = "PackVisual";
        visualInstance.transform.SetParent(root.transform, false);
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localRotation = CardArtLibrary.WorldVisualRotation;
        visualInstance.transform.localScale = Vector3.one;

        StripColliders(visualInstance.transform);
        tuning.EnsureVisualReference();

        EnsureFolder("Assets/Prefabs");

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = prefabAsset;
        EditorGUIUtility.PingObject(prefabAsset);

        Debug.Log("TCG Card Chaos: Created pack model test prefab at " + PrefabPath);
    }

    [MenuItem("GameObject/TCG Card Chaos/Pack Model Test", false, 12)]
    public static void PlaceInScene()
    {
        PlaceTestInstance(ResolveEditModeSpawnPosition());
    }

    [MenuItem("TCG Card Chaos/Spawn Pack Model Test In Play", false, 11)]
    public static void SpawnInPlay()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Pack Model Test",
                "Enter Play mode first, then run this menu again.\n\n"
                + "Adjust PackVisual while playing, click Copy Tuning Values, "
                + "then send the values before stopping Play.",
                "OK");
            return;
        }

        PlaceTestInstance(ResolvePlaySpawnPosition());
    }

    [MenuItem("TCG Card Chaos/Spawn Pack Model Test In Play", true)]
    public static bool SpawnInPlayValidate()
    {
        return Application.isPlaying;
    }

    static void PlaceTestInstance(Vector3 worldPosition)
    {
        GameObject prefab = LoadOrCreatePrefab();
        if (prefab == null)
            return;

        GameObject instance = Application.isPlaying
            ? (GameObject)Object.Instantiate(prefab, worldPosition, Quaternion.identity)
            : (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        if (!Application.isPlaying)
            Undo.RegisterCreatedObjectUndo(instance, "Create Pack Model Test");

        instance.transform.SetPositionAndRotation(worldPosition, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        Selection.activeGameObject = instance;

        Debug.Log("TCG Card Chaos: Pack model test spawned. Tweak PackVisual, copy values, then stop Play.");
    }

    static GameObject LoadOrCreatePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
            return prefab;

        if (!EditorUtility.DisplayDialog(
                "Pack Model Test",
                "Prefab not found. Create it now?",
                "Create",
                "Cancel"))
            return null;

        CreateOrUpdatePrefab();
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    static Vector3 ResolveEditModeSpawnPosition()
    {
        if (Selection.activeTransform != null)
            return Selection.activeTransform.position;

        return new Vector3(0f, CardFactory.GroundHeightOffset(), 0f);
    }

    static Vector3 ResolvePlaySpawnPosition()
    {
        WorldCard nearestCard = FindNearestGroundCard();
        if (nearestCard != null)
        {
            Transform cardTransform = nearestCard.transform;
            Vector3 offset = cardTransform.right * (CardDimensions.Width * CardDimensions.GroundCardScale * 1.35f);
            return cardTransform.position + offset;
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            Vector3 target = camera.transform.position + forward * 1.1f;
            target.y = CardFactory.GroundHeightOffset();
            return target;
        }

        return new Vector3(0f, CardFactory.GroundHeightOffset(), 0f);
    }

    static WorldCard FindNearestGroundCard()
    {
        Camera camera = Camera.main;
        Vector3 reference = camera != null ? camera.transform.position : Vector3.zero;

        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None);
        WorldCard nearest = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < cards.Length; i++)
        {
            WorldCard card = cards[i];
            if (card == null || card.IsInHand)
                continue;

            Vector3 delta = card.transform.position - reference;
            delta.y = 0f;
            float distance = delta.sqrMagnitude;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            nearest = card;
        }

        return nearest;
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

    static void StripColliders(Transform root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                Object.DestroyImmediate(colliders[i]);
        }
    }
}

[CustomEditor(typeof(PackModelTestTuning))]
public sealed class PackModelTestTuningInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tuning = (PackModelTestTuning)target;
        tuning.EnsureVisualReference();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Tuning", EditorStyles.boldLabel);

        CardArtLibrary.EnsureLoaded();
        EditorGUILayout.HelpBox(
            "Hedef kart boyutu (root local):\n"
            + "Width  = " + CardDimensions.Width.ToString("F4") + "\n"
            + "Height = " + CardDimensions.Height.ToString("F4") + "\n"
            + "Root scale = " + CardDimensions.GroundCardScale.ToString("F2") + " (değiştirme)\n\n"
            + "Sarı gizmo = kart ayak izi. PackVisual Scale X/Y/Z eşit tut (uniform).\n"
            + "Pack'i sarı kutuya oturtunca Copy Tuning Values → buraya yapıştır.",
            MessageType.Info);

        if (GUILayout.Button("Apply Placeholder Cube Orientation"))
            tuning.ApplyPlaceholderOrientation();

        if (GUILayout.Button("Reset PackVisual Rotation To Card Flat (-90,0,0)"))
        {
            tuning.EnsureVisualReference();
            if (tuning.PackVisual != null)
            {
                Undo.RecordObject(tuning.PackVisual, "Reset PackVisual Rotation");
                tuning.PackVisual.localRotation = CardArtLibrary.WorldVisualRotation;
                tuning.PackVisual.localPosition = Vector3.zero;
                EditorUtility.SetDirty(tuning.PackVisual);
            }
        }

        if (!tuning.TryGetVisualTransform(out Vector3 pos, out Quaternion rot, out Vector3 scale))
        {
            EditorGUILayout.HelpBox("PackVisual child not found.", MessageType.Warning);
            return;
        }

        Vector3 euler = rot.eulerAngles;
        float rootScale = tuning.transform.localScale.x;

        EditorGUILayout.LabelField("Root Local Scale", rootScale.ToString("F4"));
        EditorGUILayout.LabelField("PackVisual Local Position", FormatVector3(pos));
        EditorGUILayout.LabelField("PackVisual Local Rotation (Euler)", FormatVector3(euler));
        EditorGUILayout.LabelField("PackVisual Local Scale", FormatVector3(scale));

        if (GUILayout.Button("Copy Tuning Values"))
        {
            string text = BuildCopyText(tuning.name, rootScale, pos, euler, scale);
            EditorGUIUtility.systemCopyBuffer = text;
            Debug.Log("TCG Card Chaos: Copied pack tuning values to clipboard:\n" + text);
        }
    }

    static string BuildCopyText(string objectName, float rootScale, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Pack model tuning (" + objectName + ")");
        builder.AppendLine("Root Local Scale: " + rootScale.ToString("F4"));
        builder.AppendLine("PackVisual Local Position: " + FormatVector3(pos));
        builder.AppendLine("PackVisual Local Rotation (Euler): " + FormatVector3(euler));
        builder.AppendLine("PackVisual Local Scale: " + FormatVector3(scale));
        return builder.ToString();
    }

    static string FormatVector3(Vector3 value)
    {
        return "(" + value.x.ToString("F4") + ", " + value.y.ToString("F4") + ", " + value.z.ToString("F4") + ")";
    }
}
