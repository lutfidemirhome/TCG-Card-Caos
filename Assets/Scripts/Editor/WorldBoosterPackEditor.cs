#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldBoosterPack))]
public class WorldBoosterPackEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

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
