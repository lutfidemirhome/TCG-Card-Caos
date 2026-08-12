using UnityEditor;
using UnityEngine;

public static class CardShelfSignSetup
{
    const string SignName = "CategorySign";
    const string BackSignName = "CategorySignBack";

    const string UncommonMatPath = "Assets/Art/ShelfSigns/Materials/normal_uncommon_sign.mat";
    const string UncommonTexPath = "Assets/Art/ShelfSigns/Textures/normal_uncommon_sign.png";
    const string CommonMatPath = "Assets/Art/ShelfSigns/Materials/normal_common_sign.mat";
    const string CommonTexPath = "Assets/Art/ShelfSigns/Textures/normal_common_sign.png";
    const string UncommonCategoryPath = "Assets/Data/ShelfCategories/normal_uncommon.asset";
    const string UncommonCabinetPath = "Assets/Prefabs/Cabinets/Cabinet_NormalUncommon.prefab";

    [MenuItem("TCG Card Caos/Fix Normal Uncommon Sign Material")]
    public static void FixNormalUncommonSignMaterial()
    {
        EnsureUncommonSignAssets();

        Material material = AssetDatabase.LoadAssetAtPath<Material>(UncommonMatPath);
        CardShelfCategoryDefinition category =
            AssetDatabase.LoadAssetAtPath<CardShelfCategoryDefinition>(UncommonCategoryPath);
        GameObject cabinetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UncommonCabinetPath);

        if (material == null)
        {
            Debug.LogError("TCG Card Caos: Could not load " + UncommonMatPath);
            return;
        }

        if (category != null)
        {
            SerializedObject categoryObject = new SerializedObject(category);
            categoryObject.FindProperty("signMaterial").objectReferenceValue = material;
            categoryObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(category);
        }

        if (cabinetPrefab != null)
        {
            CardShelf shelf = cabinetPrefab.GetComponent<CardShelf>();
            if (shelf != null)
            {
                SerializedObject shelfObject = new SerializedObject(shelf);
                shelfObject.FindProperty("categoryDefinition").objectReferenceValue = category;
                shelfObject.FindProperty("categoryId").stringValue = CardShelfCategories.NormalUncommon;
                shelfObject.ApplyModifiedPropertiesWithoutUndo();
            }

            ApplySignMaterialToPrefab(cabinetPrefab, material);
            EditorUtility.SetDirty(cabinetPrefab);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TCG Card Caos: Normal Uncommon sign material refreshed. Re-enter Play mode if needed.");
    }

    [MenuItem("TCG Card Caos/Sync Sign Texture Scale From Selected Cabinet")]
    public static void SyncSignTextureScaleFromSelectedCabinet()
    {
        CardShelf shelf = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<CardShelf>()
            : null;
        if (shelf == null)
        {
            Debug.LogWarning("TCG Card Caos: Select a cabinet prefab or instance first.");
            return;
        }

        Transform sign = shelf.transform.Find(SignName);
        if (sign == null)
        {
            Debug.LogWarning("TCG Card Caos: No CategorySign found on '" + shelf.name + "'.");
            return;
        }

        CardShelfCategoryDefinition category = shelf.CategoryDefinition;
        Material material = category != null ? category.SignMaterial : null;
        if (material == null)
        {
            Debug.LogWarning("TCG Card Caos: Cabinet has no category sign material assigned.");
            return;
        }

        Vector3 scale = sign.localScale;
        Vector2 baseMapScale = new Vector2(Mathf.Max(Mathf.Abs(scale.x), 0.0001f), Mathf.Max(Mathf.Abs(scale.y), 0.0001f));
        ApplyBaseMapScale(material, baseMapScale, Vector2.zero);

        if (category != null)
        {
            SerializedObject categoryObject = new SerializedObject(category);
            categoryObject.FindProperty("signBaseMapScale").vector2Value = baseMapScale;
            categoryObject.FindProperty("signBaseMapOffset").vector2Value = Vector2.zero;
            categoryObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(category);
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            "TCG Card Caos: Applied sign Base Map scale "
            + baseMapScale
            + " on '"
            + material.name
            + "' to match CategorySign scale.");
    }

    static void ApplyBaseMapScale(Material material, Vector2 scale, Vector2 offset)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_MainTex", scale);
            material.SetTextureOffset("_MainTex", offset);
        }

        EditorUtility.SetDirty(material);
    }

    static void EnsureUncommonSignAssets()
    {
        if (!System.IO.File.Exists(UncommonTexPath))
            AssetDatabase.CopyAsset(CommonTexPath, UncommonTexPath);

        if (!System.IO.File.Exists(UncommonMatPath))
            AssetDatabase.CopyAsset(CommonMatPath, UncommonMatPath);

        AssetDatabase.ImportAsset(UncommonTexPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(UncommonMatPath, ImportAssetOptions.ForceUpdate);

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(UncommonTexPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(UncommonMatPath);
        if (material == null || texture == null)
            return;

        if (material.name != "normal_uncommon_sign")
            material.name = "normal_uncommon_sign";

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);

        EditorUtility.SetDirty(material);
    }

    static void ApplySignMaterialToPrefab(GameObject cabinetPrefab, Material material)
    {
        foreach (Renderer renderer in cabinetPrefab.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.gameObject.name != SignName && renderer.gameObject.name != BackSignName)
                continue;

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            renderer.sharedMaterials = materials;
        }
    }

    [MenuItem("TCG Card Caos/Duplicate Category Sign For Back")]
    public static void DuplicateCategorySignForBack()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("TCG Card Caos: Select a cabinet prefab or instance first.");
            return;
        }

        CardShelf shelf = selected.GetComponentInParent<CardShelf>();
        if (shelf == null)
        {
            Debug.LogWarning("TCG Card Caos: Selected object has no CardShelf parent.");
            return;
        }

        Transform frontSign = shelf.transform.Find(SignName);
        if (frontSign == null)
        {
            Debug.LogWarning("TCG Card Caos: '" + shelf.name + "' has no CategorySign child.");
            return;
        }

        Transform existingBack = shelf.transform.Find(BackSignName);
        if (existingBack != null)
        {
            Selection.activeGameObject = existingBack.gameObject;
            Debug.Log("TCG Card Caos: CategorySignBack already exists on '" + shelf.name + "'.");
            return;
        }

        GameObject backSignGo = Object.Instantiate(frontSign.gameObject, shelf.transform);
        Undo.RegisterCreatedObjectUndo(backSignGo, "Duplicate Category Sign For Back");
        backSignGo.name = BackSignName;

        Transform backSign = backSignGo.transform;
        backSign.localPosition = frontSign.localPosition;
        backSign.localRotation = frontSign.localRotation * Quaternion.Euler(0f, 180f, 0f);
        backSign.localScale = frontSign.localScale;

        MeshCollider collider = backSignGo.GetComponent<MeshCollider>();
        if (collider != null)
            collider.enabled = false;

        Selection.activeGameObject = backSignGo;
        EditorUtility.SetDirty(shelf.gameObject);

        Debug.Log(
            "TCG Card Caos: Duplicated CategorySign as CategorySignBack on '"
            + shelf.name
            + "'. Nudge position if needed, then save prefab.");
    }
}
