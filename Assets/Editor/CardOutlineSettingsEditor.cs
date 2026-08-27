using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardOutlineSettings))]
public class CardOutlineSettingsEditor : Editor
{
    public const string AssetPath = "Assets/Resources/Settings/CardOutlineSettings.asset";

    public override void OnInspectorGUI()
    {
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Renkler bu asset'ten okunur. Değiştirmek için Play'i durdurun.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(true))
                DrawDefaultInspector();

            return;
        }

        EditorGUILayout.HelpBox(
            "Renkleri buradan ayarlayın. Değişiklikler asset'e kaydedilir.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (!EditorGUI.EndChangeCheck())
            return;

        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssetIfDirty(target);
    }

    [MenuItem("TCG Card Chaos/Ensure Card Outline Settings Asset")]
    public static void EnsureAsset()
    {
        CardOutlineSettings existing = AssetDatabase.LoadAssetAtPath<CardOutlineSettings>(AssetPath);
        if (existing == null)
            existing = Resources.Load<CardOutlineSettings>(CardOutlineSettings.ResourcePath);

        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("TCG Card Chaos: Card outline settings already exist at " + AssetPath);
            return;
        }

        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Settings");

        var settings = ScriptableObject.CreateInstance<CardOutlineSettings>();
        AssetDatabase.CreateAsset(settings, AssetPath);
        AssetDatabase.SaveAssetIfDirty(settings);
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
        Debug.Log("TCG Card Chaos: Created " + AssetPath);
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
