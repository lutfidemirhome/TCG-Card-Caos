using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PsaCabinetSlot))]
public class PsaCabinetSlotEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var slot = (PsaCabinetSlot)target;
        EditorGUILayout.Space(6f);

        if (GUILayout.Button("Create / Refresh Slot Marker"))
        {
            slot.TryCreateSlotMarker();
        }

        if (GUILayout.Button("Create / Refresh Slot Number Label"))
        {
            slot.TryCreateLabelObject();
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "To add seats 8–10 inside this prefab (one shared table, no extra Counter_attlsv), "
            + "close Prefab Mode and run:\nTCG Card Caos → PSA → Setup 4 Holders In KartTutucu_1 Prefab\n"
            + "If labels 8–10 look wrong, run:\nTCG Card Caos → PSA → Sync KartTutucu_1 Labels From Holder 7",
            MessageType.Info);

        if (GUI.changed)
        {
            slot.EnsureLabelExists();
            slot.RefreshLabel();
            EditorUtility.SetDirty(slot);
        }
    }

    [MenuItem("TCG Card Caos/PSA/Add Slot Labels To Selected Holders")]
    static void AddLabelsToSelection()
    {
        PsaCabinetSlot[] slots = Selection.GetFiltered<PsaCabinetSlot>(SelectionMode.Editable);
        if (slots.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "PSA Slot Label",
                "Select one or more PsaCabinetSlot objects in the Hierarchy.",
                "OK");
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null)
                continue;

            slot.TryCreateLabelObject();
            EditorUtility.SetDirty(slot);
        }
    }
}
