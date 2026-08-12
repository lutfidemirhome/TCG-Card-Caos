using UnityEditor;
using UnityEngine;

public static class CardShelfSignSetup
{
    const string SignName = "CategorySign";
    const string BackSignName = "CategorySignBack";

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
