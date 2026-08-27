using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PsaCabinetSlot))]
public class PsaCabinetSlotEditor : Editor
{
    SerializedProperty _labelColor;

    void OnEnable()
    {
        _labelColor = serializedObject.FindProperty("labelColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "labelColor");

        var slot = (PsaCabinetSlot)target;
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Slot Number Label", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            _labelColor,
            new GUIContent("Rakam Rengi", "Changes the slot number text color on this holder."));
        bool labelColorChanged = EditorGUI.EndChangeCheck();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy Color From Text Child"))
        {
            slot.AdoptLabelColorFromText();
            serializedObject.Update();
            EditorUtility.SetDirty(slot);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Change Rakam Rengi here while editing the prefab. The color is written to SlotNumberLabel/Text automatically.",
            MessageType.Info);

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
            + "close Prefab Mode and run:\nTCG Card Chaos → PSA → Setup 4 Holders In KartTutucu_1 Prefab\n"
            + "If labels 8–10 look wrong, run:\nTCG Card Chaos → PSA → Sync KartTutucu_1 Labels From Holder 7",
            MessageType.Info);

        if (labelColorChanged || GUI.changed)
        {
            serializedObject.ApplyModifiedProperties();
            slot.EnsureLabelExists();
            slot.RefreshLabel();
            EditorUtility.SetDirty(slot);
        }
        else
        {
            serializedObject.ApplyModifiedProperties();
        }
    }

    [MenuItem("TCG Card Chaos/PSA/Add Slot Labels To Selected Holders")]
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
