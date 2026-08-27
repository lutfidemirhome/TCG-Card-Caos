using UnityEditor;
using UnityEngine;

public static class CardScatterZoneEditor
{
    [MenuItem("GameObject/TCG Card Chaos/Card Scatter Zone", false, 11)]
    public static void CreateScatterZone()
    {
        var zoneObject = new GameObject("CardScatterZone");
        Undo.RegisterCreatedObjectUndo(zoneObject, "Create Card Scatter Zone");

        CardScatterZone zone = zoneObject.AddComponent<CardScatterZone>();
        zone.EnsureSetup(forceDefaultSize: true);

        if (Selection.activeTransform != null)
        {
            Undo.SetTransformParent(zoneObject.transform, Selection.activeTransform, "Parent Card Scatter Zone");
            zoneObject.transform.localPosition = Vector3.zero;
            zoneObject.transform.localRotation = Quaternion.identity;
        }
        else
        {
            zoneObject.transform.position = new Vector3(8f, 0.05f, -5.5f);
        }

        Selection.activeGameObject = zoneObject;
    }
}
