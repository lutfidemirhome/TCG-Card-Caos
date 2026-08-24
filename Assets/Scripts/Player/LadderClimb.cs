using UnityEngine;

/// <summary>
/// Walk-into climb volume. Forward climbs up, back climbs down — no jumping.
/// </summary>
public class LadderClimb : MonoBehaviour
{
    [SerializeField] float climbSpeed = 2.6f;
    [SerializeField] float dismountSpeed = 2.2f;
    [SerializeField] Vector3 triggerSize = new Vector3(1.15f, 2.7f, 0.95f);
    [SerializeField] Vector3 triggerCenter = new Vector3(0f, 1.28f, 0f);

    public float ClimbSpeed => climbSpeed;
    public float DismountSpeed => dismountSpeed;
    public Vector3 ClimbAxis => transform.up;

    void Awake()
    {
        EnsureTrigger();
        DisableSolidMeshColliders();
    }

    public static LadderClimb FindAround(Vector3 worldPosition, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(
            worldPosition,
            radius,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            LadderClimb climb = hit.GetComponentInParent<LadderClimb>();
            if (climb != null && climb.isActiveAndEnabled)
                return climb;

            if (LooksLikeLadder(hit.transform))
                return EnsureOn(hit.transform);
        }

        return null;
    }

    public static LadderClimb EnsureOn(Transform transform)
    {
        Transform root = transform;
        while (root.parent != null && !LooksLikeLadderRoot(root))
            root = root.parent;

        LadderClimb climb = root.GetComponent<LadderClimb>();
        if (climb == null)
            climb = root.gameObject.AddComponent<LadderClimb>();
        return climb;
    }

    public Vector3 HoldPoint(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        local.x = 0f;
        local.z = 0f;
        return transform.TransformPoint(local);
    }

    public bool IsNearTop(Vector3 worldPosition, float playerHeight)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        float top = triggerCenter.y + triggerSize.y * 0.5f;
        return local.y + playerHeight * 0.35f >= top - 0.45f;
    }

    public bool IsStandingOnDeck(Vector3 feetPosition)
    {
        Vector3 local = transform.InverseTransformPoint(feetPosition);
        return local.y >= triggerCenter.y;
    }

    public bool IsOverShaft(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return Mathf.Abs(local.x) <= triggerSize.x * 0.5f
            && Mathf.Abs(local.z) <= triggerSize.z * 0.5f;
    }

    public bool IsNearBottom(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        float bottom = triggerCenter.y - triggerSize.y * 0.5f;
        return local.y <= bottom + 0.35f;
    }

    void EnsureTrigger()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        box.isTrigger = true;
        box.center = triggerCenter;
        box.size = triggerSize;
    }

    void DisableSolidMeshColliders()
    {
        MeshCollider[] colliders = GetComponentsInChildren<MeshCollider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    static bool LooksLikeLadder(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            if (LooksLikeLadderRoot(current))
                return true;
            current = current.parent;
        }

        return false;
    }

    static bool LooksLikeLadderRoot(Transform transform)
    {
        return transform.name.StartsWith("Ladder", System.StringComparison.OrdinalIgnoreCase);
    }
}
