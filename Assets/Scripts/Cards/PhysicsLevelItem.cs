using UnityEngine;

/// <summary>
/// Marks a scene-authored card or pack created by the physics level builder.
/// Editor: keeps a solid BoxCollider so Grabbit and later batches can land on it.
/// Play mode: does not override <see cref="WorldCard"/> collider sleep/pickup.
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
public class PhysicsLevelItem : MonoBehaviour
{
    public enum AreaKind
    {
        Demo = 0,
        Main = 1,
    }

    [SerializeField] AreaKind area;
    [SerializeField] int batchIndex;
    [SerializeField] bool baked;

    public AreaKind Area => area;
    public int BatchIndex => batchIndex;
    public bool Baked => baked;

    public static bool IsMixStoreItem(Component component)
    {
        if (component == null)
            return false;

        PhysicsLevelItem item = component.GetComponent<PhysicsLevelItem>();
        return item != null && item.Area == AreaKind.Main;
    }

    public void Configure(AreaKind areaKind, int batch, bool isBaked)
    {
        area = areaKind;
        batchIndex = batch;
        baked = isBaked;
    }

    public void MarkBaked()
    {
        baked = true;
    }

#if UNITY_EDITOR
    void OnEnable()
    {
        if (Application.isPlaying || baked)
            return;

        WorldCard card = GetComponent<WorldCard>();
        if (card != null)
            card.ApplySolidEditorCollider();

        WorldBoosterPack pack = GetComponent<WorldBoosterPack>();
        if (pack != null)
            pack.PrepareEditorPhysicsPlacement();
    }
#endif
}
