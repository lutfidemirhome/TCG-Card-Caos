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

        if (GUILayout.Button("Create / Refresh Slot Number Label"))
        {
            slot.TryCreateLabelObject();
        }

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
