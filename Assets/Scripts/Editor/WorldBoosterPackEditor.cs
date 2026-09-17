#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldBoosterPack))]
public class WorldBoosterPackEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (Application.isPlaying)
            DrawDefaultInspector();
        else
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "preRolledContents", "assignmentLabel");
            serializedObject.ApplyModifiedProperties();
            var authoredPack = (WorldBoosterPack)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sabit paket içeriği", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("5 kartın sırasını, dilini, diğer paketlerde kullanımını ve yerdeki kopyalarını atama penceresinden düzenle.", MessageType.Info);
            if (!authoredPack.TryGetFixedContents(out var contents, out string error))
                EditorGUILayout.HelpBox(error, MessageType.Warning);
            if (contents != null)
                using (new EditorGUI.DisabledScope(true))
                    for (int i = 0; i < contents.Count; i++)
                        EditorGUILayout.ObjectField("Kart " + (i + 1), contents[i], typeof(CardDefinition), false);
            if (GUILayout.Button("Pack Kart Atamaları penceresini aç"))
                EditorApplication.ExecuteMenuItem("TCG Card Chaos/Pack Kart Atamalari");
        }

        if (!Application.isPlaying)
            return;

        var pack = (WorldBoosterPack)target;
        if (!pack.IsInHand)
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Canli el ayari: PackVisual altindaki MeshRenderer > Material'i Inspector'dan degistir. "
            + "Game view'da aninda gorunur. Play durunca kaybolur.",
            MessageType.Info);

        Transform visualRoot = pack.PackVisualRoot;
        if (visualRoot == null)
            return;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("PackVisual Sec"))
            Selection.activeTransform = visualRoot;

        Renderer renderer = visualRoot.GetComponentInChildren<Renderer>(true);
        if (renderer != null && GUILayout.Button("Material Sec"))
        {
            Material[] materials = renderer.materials;
            Selection.activeObject = materials.Length > 0 ? materials[0] : renderer;
        }
        EditorGUILayout.EndHorizontal();
    }
}
#endif
