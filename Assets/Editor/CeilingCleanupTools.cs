#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Top-down view and bulk select/delete for Ceiling Modular tiles outside the play area.
/// </summary>
public static class CeilingCleanupTools
{
    const string FloorPath = "Room/Floor";
    const string CeilingName = "Ceiling";
    static readonly HashSet<string> TileNames = new HashSet<string>
    {
        "panel1", "panel1b", "panel2", "panel2b",
        "light1", "light2", "light3",
        "vent", "fire", "smoke", "sound",
    };

    static bool _boxSelectActive;
    static Vector2 _dragStart;
    static Vector2 _dragEnd;
    static bool _dragging;

    [MenuItem("TCG Card Caos/Ceiling/Top-Down View (Play Area)")]
    public static void FocusTopDownView()
    {
        if (!TryGetPlayAreaBounds(out Bounds bounds))
            return;

        SceneView view = SceneView.lastActiveSceneView;
        if (view == null)
        {
            EditorUtility.DisplayDialog("Ceiling Tools", "Open the Scene view first.", "OK");
            return;
        }

        view.orthographic = true;
        view.in2DMode = false;
        view.rotation = Quaternion.Euler(90f, 0f, 0f);
        view.pivot = bounds.center;
        view.size = Mathf.Max(bounds.size.x, bounds.size.z) * 0.55f;
        view.Repaint();

        Debug.Log("[Ceiling] Top-down view aligned to Floor.");
    }

    [MenuItem("TCG Card Caos/Ceiling/Select Outside Play Area")]
    public static void SelectOutsidePlayArea()
    {
        List<GameObject> outside = CollectTilesOutsidePlayArea();
        if (outside.Count == 0)
        {
            Debug.Log("[Ceiling] No ceiling tiles found outside the play area.");
            return;
        }

        Selection.objects = outside.ToArray();
        Debug.Log("[Ceiling] Selected " + outside.Count + " tiles outside play area. Press Delete to remove them.");
    }

    [MenuItem("TCG Card Caos/Ceiling/Delete Outside Play Area")]
    public static void DeleteOutsidePlayArea()
    {
        List<GameObject> outside = CollectTilesOutsidePlayArea();
        if (outside.Count == 0)
        {
            Debug.Log("[Ceiling] No ceiling tiles found outside the play area.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Delete ceiling tiles",
                outside.Count + " ceiling tiles are outside the Floor play area.\n\nDelete them?",
                "Delete",
                "Cancel"))
            return;

        Undo.SetCurrentGroupName("Delete Ceiling Outside Play Area");
        int group = Undo.GetCurrentGroup();
        for (int i = 0; i < outside.Count; i++)
        {
            if (outside[i] != null)
                Undo.DestroyObjectImmediate(outside[i]);
        }
        Undo.CollapseUndoOperations(group);

        Debug.Log("[Ceiling] Deleted " + outside.Count + " tiles outside play area.");
    }

    [MenuItem("TCG Card Caos/Ceiling/Enable Box Select Tool")]
    public static void ToggleBoxSelectTool()
    {
        SetBoxSelectActive(!_boxSelectActive);
    }

    [MenuItem("TCG Card Caos/Ceiling/Enable Box Select Tool", true)]
    public static bool ToggleBoxSelectToolValidate()
    {
        Menu.SetChecked("TCG Card Caos/Ceiling/Enable Box Select Tool", _boxSelectActive);
        return true;
    }

    [MenuItem("TCG Card Caos/Ceiling/Disable Box Select Tool")]
    public static void DisableBoxSelectTool()
    {
        SetBoxSelectActive(false);
    }

    static void SetBoxSelectActive(bool active)
    {
        if (_boxSelectActive == active)
            return;

        _boxSelectActive = active;
        _dragging = false;

        if (_boxSelectActive)
        {
            SceneView.duringSceneGui += OnBoxSelectSceneGui;
            FocusTopDownView();
            Debug.Log("[Ceiling] Box select ON — drag a rectangle in Scene view. Disable via the same menu item.");
        }
        else
        {
            SceneView.duringSceneGui -= OnBoxSelectSceneGui;
            Debug.Log("[Ceiling] Box select OFF.");
        }
    }

    static void OnBoxSelectSceneGui(SceneView view)
    {
        if (!_boxSelectActive)
            return;

        Handles.BeginGUI();
        GUILayout.Window(
            915024,
            new Rect(12f, 12f, 250f, 54f),
            _ => GUILayout.Label("Ceiling box select: drag to select tiles\nMenu again to turn off"),
            "Ceiling Select");
        Handles.EndGUI();

        Event e = Event.current;
        int controlId = GUIUtility.GetControlID("CeilingBoxSelect".GetHashCode(), FocusType.Passive);

        switch (e.GetTypeForControl(controlId))
        {
            case EventType.Layout:
                HandleUtility.AddDefaultControl(controlId);
                break;

            case EventType.MouseDown:
                if (e.button == 0 && !e.alt)
                {
                    _dragging = true;
                    _dragStart = e.mousePosition;
                    _dragEnd = _dragStart;
                    GUIUtility.hotControl = controlId;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (_dragging && GUIUtility.hotControl == controlId)
                {
                    _dragEnd = e.mousePosition;
                    view.Repaint();
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (_dragging && GUIUtility.hotControl == controlId)
                {
                    _dragEnd = e.mousePosition;
                    _dragging = false;
                    GUIUtility.hotControl = 0;
                    SelectInScreenRect(view, BuildRect(_dragStart, _dragEnd));
                    e.Use();
                }
                break;

            case EventType.Repaint:
                if (_dragging)
                    DrawRect(BuildRect(_dragStart, _dragEnd), new Color(0.2f, 0.7f, 1f, 0.15f), new Color(0.2f, 0.7f, 1f, 0.9f));
                break;
        }
    }

    static Rect BuildRect(Vector2 a, Vector2 b)
    {
        return Rect.MinMaxRect(
            Mathf.Min(a.x, b.x),
            Mathf.Min(a.y, b.y),
            Mathf.Max(a.x, b.x),
            Mathf.Max(a.y, b.y));
    }

    static void DrawRect(Rect rect, Color fill, Color border)
    {
        EditorGUI.DrawRect(rect, fill);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, 1f), border);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - 1f, rect.width, 1f), border);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, 1f, rect.height), border);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.yMin, 1f, rect.height), border);
    }

    static void SelectInScreenRect(SceneView view, Rect rect)
    {
        var selected = new List<Object>();
        foreach (Transform tile in EnumerateCeilingTiles())
        {
            if (tile == null)
                continue;

            Vector3 screen = view.camera.WorldToScreenPoint(tile.position);
            if (screen.z < 0f)
                continue;

            screen.y = view.camera.pixelHeight - screen.y;
            if (rect.Contains(new Vector2(screen.x, screen.y)))
                selected.Add(tile.gameObject);
        }

        Selection.objects = selected.ToArray();
        Debug.Log("[Ceiling] Box-selected " + selected.Count + " tiles.");
    }

    public static bool TryGetPlayAreaBounds(out Bounds bounds)
    {
        bounds = default;
        Transform floor = FindFloorTransform();
        if (floor == null)
        {
            EditorUtility.DisplayDialog("Ceiling Tools", "Could not find Room/Floor in the active scene.", "OK");
            return false;
        }

        Renderer renderer = floor.GetComponent<Renderer>();
        if (renderer != null)
        {
            bounds = renderer.bounds;
            return true;
        }

        Collider collider = floor.GetComponent<Collider>();
        if (collider != null)
        {
            bounds = collider.bounds;
            return true;
        }

        EditorUtility.DisplayDialog("Ceiling Tools", "Floor has no Renderer or Collider for bounds.", "OK");
        return false;
    }

    public static List<GameObject> CollectTilesOutsidePlayArea(float inset = 0.05f)
    {
        var results = new List<GameObject>(256);
        if (!TryGetPlayAreaBounds(out Bounds playArea))
            return results;

        playArea.extents = new Vector3(
            Mathf.Max(0f, playArea.extents.x - inset),
            playArea.extents.y + 50f,
            Mathf.Max(0f, playArea.extents.z - inset));

        foreach (Transform tile in EnumerateCeilingTiles())
        {
            if (tile == null)
                continue;

            Vector3 p = tile.position;
            if (!playArea.Contains(new Vector3(p.x, playArea.center.y, p.z)))
                results.Add(tile.gameObject);
        }

        return results;
    }

    public static IEnumerable<Transform> EnumerateCeilingTiles()
    {
        GameObject ceilingRoot = GameObject.Find(CeilingName);
        if (ceilingRoot != null)
        {
            Transform[] all = ceilingRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != ceilingRoot.transform && IsCeilingTile(t))
                    yield return t;
            }
            yield break;
        }

        Transform[] sceneTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            Transform t = sceneTransforms[i];
            if (IsCeilingTile(t))
                yield return t;
        }
    }

    public static bool IsCeilingTile(Transform t)
    {
        if (t == null)
            return false;

        if (TileNames.Contains(t.name))
            return true;

        return t.name.StartsWith("panel") || t.name.StartsWith("light");
    }

    static Transform FindFloorTransform()
    {
        GameObject floor = GameObject.Find(FloorPath);
        if (floor != null)
            return floor.transform;

        floor = GameObject.Find("Floor");
        return floor != null ? floor.transform : null;
    }
}
#endif
