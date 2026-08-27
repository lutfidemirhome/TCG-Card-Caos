using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class KartTutucuPrefabRowEditor
{
    const string PrefabPath = "Assets/Prefabs/PsaCabinet/KartTutucu_1.prefab";
    const string HolderPrefix = "PsaHolder_";
    const string CounterName = "Counter_attlsv";

    static readonly string[] HolderChildNames =
    {
        "Tutucu1_Visual",
        "Tutucu2_Visual",
        "PsaSlotMarker",
        "SlotLabelAnchor",
    };

    /// <summary>Batch entry point for CI/local automation.</summary>
    public static void ExecuteSetupFourHoldersInPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"Prefab not found: {PrefabPath}");
            EditorApplication.Exit(1);
            return;
        }

        try
        {
            if (!SetupFourHolders(root))
            {
                EditorApplication.Exit(1);
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        EditorApplication.Exit(0);
    }

    [MenuItem("TCG Card Chaos/PSA/Setup 4 Holders In KartTutucu_1 Prefab")]
    public static void SetupFourHoldersInPrefabMenu()
    {
        if (SetupFourHoldersInPrefabContents())
        {
            EditorUtility.DisplayDialog(
                "PSA Holders",
                "KartTutucu_1 prefab now has 4 holder seats (7–10) on one shared Counter_attlsv.",
                "OK");
        }
    }

    [MenuItem("TCG Card Chaos/PSA/Sync KartTutucu_1 Labels From Holder 7")]
    public static void SyncLabelsFromHolder7Menu()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            EditorUtility.DisplayDialog("PSA Holders", $"Prefab not found: {PrefabPath}", "OK");
            return;
        }

        try
        {
            Transform holder7 = root.transform.Find($"{HolderPrefix}7");
            if (holder7 == null)
            {
                EditorUtility.DisplayDialog("PSA Holders", "PsaHolder_7 not found in prefab.", "OK");
                return;
            }

            SyncAllLabelHierarchiesFromHolder7(holder7);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "PSA Holders",
                "Labels on holders 8–10 now match holder 7 position, rotation, and text color.",
                "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool SetupFourHoldersInPrefabContents()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            EditorUtility.DisplayDialog("PSA Holders", $"Prefab not found: {PrefabPath}", "OK");
            return false;
        }

        try
        {
            if (!SetupFourHolders(root))
                return false;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool SetupFourHolders(GameObject root)
    {
        Transform rootTransform = root.transform;
        Transform counter = FindDirectChild(rootTransform, CounterName);
        if (counter == null)
        {
            EditorUtility.DisplayDialog(
                "PSA Holders",
                $"{CounterName} not found under {root.name}.",
                "OK");
            return false;
        }

        Transform holder7 = rootTransform.Find($"{HolderPrefix}7");
        if (holder7 == null)
            holder7 = WrapExistingHolder(root, counter);

        if (holder7 == null)
            return false;

        float spacing = CalculateLocalSpacing(holder7, rootTransform);
        EnsureHolderSlots(rootTransform, holder7, counter, spacing);
        counter.SetAsLastSibling();
        EnsurePsaCabinetComponent(root);
        return true;
    }

    static void EnsurePsaCabinetComponent(GameObject root)
    {
        if (root.GetComponent<PsaCabinet>() == null)
            root.AddComponent<PsaCabinet>();
    }

    static Transform WrapExistingHolder(GameObject root, Transform counter)
    {
        Transform rootTransform = root.transform;
        PsaCabinetSlot rootSlot = root.GetComponent<PsaCabinetSlot>();

        for (int i = 0; i < HolderChildNames.Length; i++)
        {
            if (FindDirectChild(rootTransform, HolderChildNames[i]) == null)
            {
                EditorUtility.DisplayDialog(
                    "PSA Holders",
                    $"Missing child '{HolderChildNames[i]}' on {root.name}.",
                    "OK");
                return null;
            }
        }

        var holderGo = new GameObject($"{HolderPrefix}7");
        Transform holder7 = holderGo.transform;
        holder7.SetParent(rootTransform, false);
        holder7.localPosition = Vector3.zero;
        holder7.localRotation = Quaternion.identity;
        holder7.localScale = Vector3.one;
        holder7.SetSiblingIndex(0);

        for (int i = 0; i < HolderChildNames.Length; i++)
        {
            Transform child = FindDirectChild(rootTransform, HolderChildNames[i]);
            child.SetParent(holder7, true);
        }

        PsaCabinetSlot slot7 = holderGo.AddComponent<PsaCabinetSlot>();
        if (rootSlot != null)
            CopySlotSettings(rootSlot, slot7, counter);
        else
            ConfigureSlot(slot7, 7, holder7, counter);

        if (rootSlot != null)
            Object.DestroyImmediate(rootSlot);

        slot7.SetSlotNumber(7);
        EditorUtility.SetDirty(holderGo);
        return holder7;
    }

    static void EnsureHolderSlots(
        Transform rootTransform,
        Transform holder7,
        Transform counter,
        float spacing)
    {
        for (int slotNumber = 8; slotNumber <= 10; slotNumber++)
        {
            Transform existing = rootTransform.Find($"{HolderPrefix}{slotNumber}");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
        }

        for (int slotNumber = 8; slotNumber <= 10; slotNumber++)
        {
            string holderName = $"{HolderPrefix}{slotNumber}";
            int step = slotNumber - 7;
            GameObject copy = Object.Instantiate(holder7.gameObject, rootTransform);
            copy.name = holderName;
            Transform copyTransform = copy.transform;
            copyTransform.localPosition = holder7.localPosition + Vector3.right * (spacing * step);
            copyTransform.localRotation = holder7.localRotation;
            copyTransform.localScale = holder7.localScale;

            PsaCabinetSlot slot = copy.GetComponent<PsaCabinetSlot>();
            ConfigureSlot(slot, slotNumber, copyTransform, counter);
            EditorUtility.SetDirty(copy);
        }

        ConfigureSlot(holder7.GetComponent<PsaCabinetSlot>(), 7, holder7, counter);
    }

    static void SyncAllLabelHierarchiesFromHolder7(Transform holder7)
    {
        Transform root = holder7.parent;
        if (root == null)
            return;

        for (int slotNumber = 8; slotNumber <= 10; slotNumber++)
        {
            Transform holder = root.Find($"{HolderPrefix}{slotNumber}");
            if (holder != null)
                SyncLabelHierarchyFromHolder7(holder7, holder);
        }
    }

    static void SyncLabelHierarchyFromHolder7(Transform sourceHolder, Transform targetHolder)
    {
        Transform sourceAnchor = sourceHolder.Find("SlotLabelAnchor");
        Transform targetAnchor = targetHolder.Find("SlotLabelAnchor");
        if (sourceAnchor == null || targetAnchor == null)
            return;

        targetAnchor.localPosition = sourceAnchor.localPosition;
        targetAnchor.localRotation = sourceAnchor.localRotation;
        targetAnchor.localScale = sourceAnchor.localScale;

        Transform sourceLabel = sourceAnchor.Find("SlotNumberLabel");
        Transform targetLabel = targetAnchor.Find("SlotNumberLabel");
        if (sourceLabel == null || targetLabel == null)
            return;

        CopyRectTransform(sourceLabel as RectTransform, targetLabel as RectTransform);
        CopyLabelVisualStyle(sourceLabel, targetLabel, targetHolder);
    }

    static void CopyLabelVisualStyle(Transform sourceLabel, Transform targetLabel, Transform targetHolder)
    {
        if (sourceLabel == null || targetLabel == null)
            return;

        CanvasRenderer sourceLabelRenderer = sourceLabel.GetComponent<CanvasRenderer>();
        CanvasRenderer targetLabelRenderer = targetLabel.GetComponent<CanvasRenderer>();
        if (targetLabelRenderer != null)
            targetLabelRenderer.cullTransparentMesh = sourceLabelRenderer == null || !sourceLabelRenderer.cullTransparentMesh;

        Canvas sourceCanvas = sourceLabel.GetComponent<Canvas>();
        Canvas targetCanvas = targetLabel.GetComponent<Canvas>();
        if (sourceCanvas != null && targetCanvas != null)
        {
            targetCanvas.renderMode = sourceCanvas.renderMode;
            targetCanvas.overrideSorting = sourceCanvas.overrideSorting;
            targetCanvas.sortingLayerID = sourceCanvas.sortingLayerID;
        }

        Transform sourceText = sourceLabel.Find("Text");
        Transform targetText = targetLabel.Find("Text");
        if (sourceText == null || targetText == null)
            return;

        CopyRectTransform(sourceText as RectTransform, targetText as RectTransform);

        Text sourceTextComponent = sourceText.GetComponent<Text>();
        Text targetTextComponent = targetText.GetComponent<Text>();
        if (sourceTextComponent == null || targetTextComponent == null)
            return;

        targetTextComponent.color = sourceTextComponent.color;
        targetTextComponent.font = sourceTextComponent.font;
        targetTextComponent.fontSize = sourceTextComponent.fontSize;
        targetTextComponent.fontStyle = sourceTextComponent.fontStyle;
        targetTextComponent.alignment = sourceTextComponent.alignment;
        targetTextComponent.horizontalOverflow = sourceTextComponent.horizontalOverflow;
        targetTextComponent.verticalOverflow = sourceTextComponent.verticalOverflow;
        targetTextComponent.raycastTarget = sourceTextComponent.raycastTarget;

        CanvasRenderer targetTextRenderer = targetText.GetComponent<CanvasRenderer>();
        if (targetTextRenderer != null)
            targetTextRenderer.cullTransparentMesh = false;

        PsaCabinetSlot slot = targetHolder.GetComponent<PsaCabinetSlot>();
        if (slot != null && targetCanvas != null)
            targetCanvas.sortingOrder = slot.SlotNumber;
    }

    static void CopyRectTransform(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
            return;

        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.pivot = source.pivot;
    }

    static void CopySlotSettings(PsaCabinetSlot source, PsaCabinetSlot target, Transform counter)
    {
        if (source == null || target == null)
            return;

        EditorUtility.CopySerialized(source, target);
        ConfigureSlot(target, source.SlotNumber, target.transform, counter);
    }

    static void ConfigureSlot(
        PsaCabinetSlot slot,
        int slotNumber,
        Transform holderRoot,
        Transform counter)
    {
        if (slot == null)
            return;

        Transform tutucu2 = holderRoot.Find("Tutucu2_Visual");
        Transform marker = holderRoot.Find("PsaSlotMarker");
        Transform labelAnchor = holderRoot.Find("SlotLabelAnchor");

        SerializedObject serializedSlot = new SerializedObject(slot);
        serializedSlot.FindProperty("slotNumber").intValue = slotNumber;
        serializedSlot.FindProperty("slotMarker").objectReferenceValue = marker;
        serializedSlot.FindProperty("holderOutlineTarget").objectReferenceValue = tutucu2;
        serializedSlot.FindProperty("labelAnchor").objectReferenceValue = labelAnchor;
        serializedSlot.FindProperty("tableVisual").objectReferenceValue = counter;
        serializedSlot.FindProperty("placementParent").objectReferenceValue = holderRoot.root;
        serializedSlot.ApplyModifiedPropertiesWithoutUndo();

        slot.SetSlotNumber(slotNumber);
        slot.EnsureLabelExists();
        slot.RefreshLabel();
        DisableOrphanSlotLabels(holderRoot);

        Transform holder7 = holderRoot.parent != null ? holderRoot.parent.Find($"{HolderPrefix}7") : null;
        if (holder7 != null && holderRoot != holder7)
            SyncLabelHierarchyFromHolder7(holder7, holderRoot);

        Transform label = labelAnchor != null ? labelAnchor.Find("SlotNumberLabel") : null;
        Canvas labelCanvas = label != null ? label.GetComponent<Canvas>() : null;
        if (labelCanvas != null)
            labelCanvas.sortingOrder = slotNumber;
    }

    static void DisableOrphanSlotLabels(Transform holderRoot)
    {
        for (int i = 0; i < holderRoot.childCount; i++)
        {
            Transform child = holderRoot.GetChild(i);
            if (child.name == "SlotNumberLabel")
                child.gameObject.SetActive(false);
        }
    }

    static float CalculateLocalSpacing(Transform holder7, Transform rootTransform)
    {
        Renderer[] renderers = holder7.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return CardDimensions.Width * 1.15f / Mathf.Max(rootTransform.lossyScale.x, 0.001f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float worldSpacing = Mathf.Max(bounds.size.x * 1.02f, CardDimensions.Width * 1.15f);
        return worldSpacing / Mathf.Max(rootTransform.lossyScale.x, 0.001f);
    }

    static Transform FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;
        }

        return null;
    }
}
