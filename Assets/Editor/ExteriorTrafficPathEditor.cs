using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExteriorTrafficPath))]
public class ExteriorTrafficPathEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ExteriorTrafficPath path = (ExteriorTrafficPath)target;

        EditorGUILayout.HelpBox(
            "Yol rotasi icin Path_Main altina bos noktalar koy.\n"
            + "Hierarchy sirasi = araba rotasi (Start -> donus noktalari -> End).\n"
            + "Smooth Corners acikken donusler yumusak egride olur.",
            MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Alt noktalardan guncelle"))
                path.SyncFromChildren();

            if (GUILayout.Button("Yeni nokta ekle"))
                path.AddWaypointAtEnd();
        }

        if (path.PointCount >= 2)
            EditorGUILayout.LabelField("Rota uzunlugu", $"{path.TotalLength:0.0} m");
    }

    void OnSceneGUI()
    {
        ExteriorTrafficPath path = (ExteriorTrafficPath)target;
        path.RefreshWaypointsIfNeeded();
    }
}
