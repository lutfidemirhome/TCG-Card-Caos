using UnityEngine;

/// <summary>
/// Makes supermarket glass doors work in a single-room shop without an exterior scene.
/// Uses frosted glass plus an opaque exterior backdrop so Unity void is never visible.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class StorefrontDoor : MonoBehaviour
{
    const string BackdropRootName = "ExteriorBackdrop";
    const string PackGlassMaterialName = "Door_c5bu08_Glass";

    [Header("Glass")]
    [SerializeField] Material storefrontGlass;
    [SerializeField] bool applyFrostedGlass = true;

    [Header("Exterior blocker")]
    [SerializeField] bool createExteriorBackdrop = true;
    [SerializeField] Material exteriorBackdrop;
    [SerializeField] Vector3 backdropLocalPosition = new Vector3(0f, 1.05f, -0.12f);
    [SerializeField] Vector3 backdropSize = new Vector3(2.55f, 2.15f, 0.02f);

    bool _rebuildQueued;

    void Reset()
    {
        LoadDefaultMaterials();
        QueueRebuild();
    }

    void OnEnable()
    {
        LoadDefaultMaterials();
        QueueRebuild();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled)
            return;

        QueueRebuild();
    }

    void LoadDefaultMaterials()
    {
#if UNITY_EDITOR
        if (storefrontGlass == null)
            storefrontGlass = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/StorefrontGlass.mat");

        if (exteriorBackdrop == null)
            exteriorBackdrop = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/StorefrontExterior.mat");
#endif
    }

    void QueueRebuild()
    {
        if (_rebuildQueued)
            return;

        _rebuildQueued = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += RunQueuedRebuild;
#else
        RunQueuedRebuild();
#endif
    }

    void RunQueuedRebuild()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= RunQueuedRebuild;
#endif
        _rebuildQueued = false;

        if (this == null || !isActiveAndEnabled)
            return;

        Rebuild();
    }

    public void Rebuild()
    {
        if (applyFrostedGlass && storefrontGlass != null)
            ApplyGlassMaterial();

        if (createExteriorBackdrop && exteriorBackdrop != null)
            EnsureExteriorBackdrop();
        else
            RemoveExteriorBackdrop();
    }

    void ApplyGlassMaterial()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;

            for (int m = 0; m < materials.Length; m++)
            {
                if (!IsPackGlassMaterial(materials[m]))
                    continue;

                materials[m] = storefrontGlass;
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

    static bool IsPackGlassMaterial(Material material)
    {
        if (material == null)
            return false;

        return material.name == PackGlassMaterialName
            || material.name == "StorefrontGlass";
    }

    void EnsureExteriorBackdrop()
    {
        Transform existing = transform.Find(BackdropRootName);
        GameObject backdropObject;

        if (existing != null)
        {
            backdropObject = existing.gameObject;
        }
        else
        {
            backdropObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdropObject.name = BackdropRootName;
            backdropObject.transform.SetParent(transform, false);

            Collider collider = backdropObject.GetComponent<Collider>();
            if (collider != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(collider);
                else
#endif
                    Destroy(collider);
            }
        }

        backdropObject.transform.localPosition = backdropLocalPosition;
        backdropObject.transform.localRotation = Quaternion.identity;
        backdropObject.transform.localScale = backdropSize;

        MeshRenderer renderer = backdropObject.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = exteriorBackdrop;
    }

    void RemoveExteriorBackdrop()
    {
        Transform existing = transform.Find(BackdropRootName);
        if (existing == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(existing.gameObject);
        else
#endif
            Destroy(existing.gameObject);
    }
}
