#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch only stationary shop fixtures; card, door and animated hierarchies remain movable.</summary>
public static class SceneRenderBatching
{
    static readonly HashSet<string> FixtureMaterials = new HashSet<string>
    {
        "CabinetShelf", "CabinetBody", "ceiling", "Ceiling", "Rooflight_9ecem1",
        "TileFloor_j8qkl9", "PsaCabinetTable", "Camera_fbj1by"
    };

    [MenuItem("TCG Card Chaos/Diagnostics/Batch Stationary Shop Fixtures")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/MainScene.unity")
            throw new System.InvalidOperationException("Open MainScene first.");
        int changed = 0;
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (renderer.gameObject.scene != scene || !renderer.enabled) continue;
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0) continue;
            bool fixture = true;
            foreach (var material in materials)
                if (!material || !FixtureMaterials.Contains(material.name)) { fixture = false; break; }
            if (!fixture || renderer.GetComponentInParent<WorldCard>()
                || renderer.GetComponentInParent<WorldBoosterPack>()
                || renderer.GetComponentInParent<Rigidbody>()
                || renderer.GetComponentInParent<Animator>()
                || renderer.GetComponentInParent<Animation>()) continue;
            GameObject obj = renderer.gameObject;
            var flags = GameObjectUtility.GetStaticEditorFlags(obj);
            // Skill outlines and completion effects read the original cabinet meshes.
            // Static batching replaces those meshes with world-space combined geometry.
            bool usesOriginalMesh = renderer.GetComponentInParent<CardShelf>()
                || renderer.GetComponentInParent<PsaCabinet>()
                || renderer.GetComponentInParent<PsaCabinetSlot>();
            var target = usesOriginalMesh ? flags & ~StaticEditorFlags.BatchingStatic
                : flags | StaticEditorFlags.BatchingStatic;
            if (flags == target) continue;
            Undo.RecordObject(obj, "Batch stationary shop fixture");
            GameObjectUtility.SetStaticEditorFlags(obj, target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
            changed++;
        }
        if (changed > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        Debug.Log($"[Rendering] Updated batching flags on {changed} fixture renderers (interactive cabinets excluded).");
    }
}
#endif
