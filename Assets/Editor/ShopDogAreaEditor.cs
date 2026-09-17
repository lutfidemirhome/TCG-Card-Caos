using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Scene-only authoring controls; never creates or bakes navigation automatically.</summary>
[CustomEditor(typeof(ShopDogArea))]
public sealed class ShopDogAreaEditor : Editor
{
    private static readonly Color GroundColor = new Color(0.2f, 0.9f, 1f);
    private static readonly Color UpstairsColor = new Color(1f, 0.7f, 0.1f);
    private bool _editPoints;

    [MenuItem("TCG Card Chaos/Shop Dog/Select Walking Area")]
    private static void SelectWalkingArea()
    {
        ShopDogArea[] areas = Object.FindObjectsByType<ShopDogArea>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        ShopDogArea found = null;
        foreach (ShopDogArea area in areas)
        {
            if (!area || !area.gameObject.scene.IsValid() || !area.gameObject.scene.isLoaded)
                continue;
            if (!found || area.gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
                found = area;
        }

        if (!found)
        {
            EditorUtility.DisplayDialog("Shop Dog", "Önce köpeğin bulunduğu MainScene sahnesini aç.", "Tamam");
            return;
        }

        Selection.activeGameObject = found.gameObject;
        EditorGUIUtility.PingObject(found.gameObject);
        if (SceneView.lastActiveSceneView)
            SceneView.lastActiveSceneView.Frame(new Bounds(found.NavigationBounds.center,
                found.NavigationBounds.size), false);
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Mavi noktalar alt kat, turuncu noktalar üst kat, yeşil nokta başlangıç konumu. "
            + "Dış çerçeve köpeğin gezebileceği sınırı gösterir. Köpek yalnızca zemin ve "
            + "merdivenlerle bağlı noktalara yürür.", MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            _editPoints = EditorGUILayout.ToggleLeft("Scene görünümünde noktaları taşı", _editPoints);
            DrawDefaultInspector();
        }

        if (Application.isPlaying)
        {
            ShopDogArea area = (ShopDogArea)target;
            EditorGUILayout.HelpBox(area.IsReady
                ? "Köpeğin dolaşma alanı hazır."
                : "Dolaşma alanı henüz hazır değil; ayrıntılar için Console'a bak.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Noktaları ilgili katın zemini üzerinde tut. Alan ve noktalar değişince sahneyi kaydet. "
                + "Gezilebilir yüzey, Play sırasında bir kez hazırlanır; düzenlemeler sonraki açılışta uygulanır.",
                MessageType.None);
        }
    }

    private void OnSceneGUI()
    {
        ShopDogArea area = (ShopDogArea)target;
        if (!area) return;

        serializedObject.Update();
        SerializedProperty spawn = serializedObject.FindProperty("spawnPoint");
        SerializedProperty points = serializedObject.FindProperty("roamingPoints");
        SerializedProperty groundHeight = serializedObject.FindProperty("groundHeight");
        SerializedProperty upstairsHeight = serializedObject.FindProperty("upstairsHeight");
        float floorSplit = (groundHeight.floatValue + upstairsHeight.floatValue) * 0.5f;

        Color previousColor = Handles.color;
        DrawPoint(spawn, "Başlangıç", Color.green);
        if (points != null)
        {
            for (int i = 0; i < points.arraySize; i++)
            {
                SerializedProperty point = points.GetArrayElementAtIndex(i);
                bool upstairs = point.vector3Value.y > floorSplit;
                DrawPoint(point, (upstairs ? "Üst kat " : "Alt kat ") + (i + 1),
                    upstairs ? UpstairsColor : GroundColor);
            }
        }
        Handles.color = previousColor;
    }

    private void DrawPoint(SerializedProperty property, string label, Color color)
    {
        if (property == null) return;
        Vector3 position = property.vector3Value;
        Handles.color = color;
        Handles.Label(position + Vector3.up * 0.25f, label);
        if (!_editPoints || Application.isPlaying) return;

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.PositionHandle(position, Quaternion.identity);
        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(target, "Köpek dolaşma noktasını taşı");
        property.vector3Value = moved;
        serializedObject.ApplyModifiedProperties();
        ShopDogArea area = (ShopDogArea)target;
        if (area.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(area.gameObject.scene);
    }
}
