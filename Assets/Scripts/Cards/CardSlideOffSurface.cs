using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Vegetation cannot support a thrown card or pack. Keep it sliding toward the
/// edge while contact lasts, then let gravity and the normal landing code finish.
/// Attached explicitly to plant instances, never inferred from runtime names.
/// </summary>
[DisallowMultipleComponent]
public sealed class CardSlideOffSurface : MonoBehaviour
{
    const float SlideSpeed = 0.8f;
    const float EdgeClearance = 0.08f;
    [SerializeField] bool useMeshColliders;
    readonly Dictionary<Rigidbody, Vector3> _slideDirections = new Dictionary<Rigidbody, Vector3>();
    static readonly RaycastHit[] PathHits = new RaycastHit[64];

    void Awake()
    {
        if (useMeshColliders)
            ConfigureMeshColliders();

        // Imported plants can have their colliders on child objects. Static
        // collision messages go to those objects, so give each one a handler.
        foreach (Collider surface in GetComponentsInChildren<Collider>(true))
        {
            if (surface.enabled && !surface.isTrigger && surface.GetComponent<CardSlideOffSurface>() == null)
                surface.gameObject.AddComponent<CardSlideOffSurface>();
        }
    }

    void ConfigureMeshColliders()
    {
        // Use only the highest-detail LOD, not several overlapping versions of
        // the same plant. Collision geometry stays stable as camera LOD changes.
        var lodRenderers = new HashSet<Renderer>();
        var collisionRenderers = new HashSet<Renderer>();
        foreach (LODGroup group in GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] lods = group.GetLODs();
            for (int i = 0; i < lods.Length; i++)
            {
                foreach (Renderer renderer in lods[i].renderers)
                {
                    if (renderer == null)
                        continue;
                    lodRenderers.Add(renderer);
                    if (i == 0)
                        collisionRenderers.Add(renderer);
                }
            }
        }

        Collider[] originalColliders = GetComponentsInChildren<Collider>(true);
        // These scene decorations are static. Do not create concave collision
        // shapes if this component is ever reused on a moving physics prop.
        foreach (Collider original in originalColliders)
        {
            if (original.attachedRigidbody != null)
                return;
        }

        var meshColliders = new HashSet<Collider>();
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            Renderer renderer = filter.GetComponent<Renderer>();
            if (filter.sharedMesh == null || renderer == null || !filter.gameObject.activeInHierarchy)
                continue;
            if (lodRenderers.Contains(renderer) && !collisionRenderers.Contains(renderer))
                continue;
            if (filter.GetComponentInParent<Rigidbody>() != null)
                continue;

            MeshCollider meshCollider = null;
            foreach (MeshCollider existing in filter.GetComponents<MeshCollider>())
            {
                if (!existing.isTrigger)
                {
                    meshCollider = existing;
                    break;
                }
            }
            if (meshCollider == null)
                meshCollider = filter.gameObject.AddComponent<MeshCollider>();

            // Convex would wrap the foliage in another solid hull. Static
            // non-convex geometry follows the actual model triangles instead.
            meshCollider.convex = false;
            meshCollider.sharedMesh = filter.sharedMesh;
            // Detailed foliage is for cards/packs, not the player's capsule.
            meshCollider.excludeLayers = ~CardLayers.WorldCardMask.value;
            meshCollider.enabled = true;
            meshColliders.Add(meshCollider);
        }

        // Keep the original collision as a fallback if no usable mesh exists.
        if (meshColliders.Count == 0)
            return;
        foreach (Collider original in originalColliders)
        {
            if (!original.isTrigger && !meshColliders.Contains(original))
                original.excludeLayers = original.excludeLayers.value | CardLayers.WorldCardMask.value;
        }
    }

    void OnCollisionEnter(Collision collision) => Slide(collision);
    void OnCollisionStay(Collision collision) => Slide(collision);

    void OnCollisionExit(Collision collision)
    {
        if (collision.rigidbody != null)
            _slideDirections.Remove(collision.rigidbody);
    }

    void OnDisable() => _slideDirections.Clear();

    void Slide(Collision collision)
    {
        Rigidbody body = collision.rigidbody;
        if (body == null || body.isKinematic)
            return;

        WorldCard card = body.GetComponent<WorldCard>();
        WorldBoosterPack pack = body.GetComponent<WorldBoosterPack>();
        if (card == null && pack == null)
            return;
        if ((card != null && card.IsInHand) || (pack != null && pack.IsInHand))
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            Vector3 normal = contact.normal;
            // Orient the support normal toward the thrown item, regardless of
            // which side of the collision Unity reports to this component.
            if (Vector3.Dot(normal, body.worldCenterOfMass - contact.point) < 0f)
                normal = -normal;
            if (normal.y < 0.2f)
                continue;

            Collider surface = contact.thisCollider.attachedRigidbody == body
                ? contact.otherCollider : contact.thisCollider;
            Vector3 direction = Vector3.ProjectOnPlane(normal, Vector3.up);
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector3.ProjectOnPlane(body.worldCenterOfMass - surface.bounds.center, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.right;
            direction.Normalize();

            Collider itemCollider = contact.thisCollider.attachedRigidbody == body
                ? contact.thisCollider : contact.otherCollider;
            direction = FindOpenEdge(body, itemCollider, surface, direction);
            if (direction == Vector3.zero)
                return;

            // A frictionless flat box can still hold a motionless card forever.
            // Maintain a small outward speed; never teleport or lift the item.
            // Remove sideways momentum toward the wall when changing course.
            // Keep the vertical velocity so gravity continues normally.
            Vector3 velocity = body.linearVelocity;
            Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float speed = Mathf.Max(SlideSpeed, Vector3.Dot(horizontal, direction));
            body.AddForce(direction * speed - horizontal, ForceMode.VelocityChange);
            body.WakeUp();
            return;
        }
    }

    Vector3 FindOpenEdge(Rigidbody body, Collider item, Collider surface, Vector3 preferred)
    {
        // Keep an open route stable instead of turning back toward the wall as
        // the card moves around the plant's centre.
        if (_slideDirections.TryGetValue(body, out Vector3 previous)
            && HasClearPath(body, item, surface, previous))
            return previous;

        Vector3 best = Vector3.zero;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 candidate = Quaternion.Euler(0f, i * 45f, 0f) * preferred;
            if (!HasClearPath(body, item, surface, candidate))
                continue;
            float score = Vector3.Dot(candidate, preferred);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != Vector3.zero)
            _slideDirections[body] = best;
        else
            _slideDirections.Remove(body);
        return best;
    }

    static bool HasClearPath(Rigidbody body, Collider item, Collider surface, Vector3 direction)
    {
        Bounds bounds = surface.bounds;
        Bounds itemBounds = item.bounds;
        Vector3 centre = itemBounds.center;
        // Travel just far enough for the whole card to clear one edge, rather
        // than rejecting a wall that is beyond the edge where it will fall.
        float x = Mathf.Abs(direction.x) < 0.001f ? float.PositiveInfinity
            : ((direction.x > 0f ? bounds.max.x + itemBounds.extents.x : bounds.min.x - itemBounds.extents.x) - centre.x) / direction.x;
        float z = Mathf.Abs(direction.z) < 0.001f ? float.PositiveInfinity
            : ((direction.z > 0f ? bounds.max.z + itemBounds.extents.z : bounds.min.z - itemBounds.extents.z) - centre.z) / direction.z;
        float distance = Mathf.Max(EdgeClearance, Mathf.Min(x, z) + EdgeClearance);
        int count = Physics.BoxCastNonAlloc(centre, itemBounds.extents * 0.95f,
            direction, PathHits, Quaternion.identity, distance, ~0, QueryTriggerInteraction.Ignore);
        if (count == PathHits.Length)
            return false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = PathHits[i];
            Collider obstacle = hit.collider;
            if (obstacle == null || obstacle == surface || obstacle.attachedRigidbody == body
                || obstacle.GetComponent<CardSlideOffSurface>() != null)
                continue;
            if ((obstacle.excludeLayers.value & (1 << item.gameObject.layer)) != 0)
                continue;
            if (Physics.GetIgnoreLayerCollision(item.gameObject.layer, obstacle.gameObject.layer)
                || Physics.GetIgnoreCollision(item, obstacle))
                continue;

            // If already touching/overlapping glass, allow movement away from
            // it or along it; a cast starting inside must not block every exit.
            if (hit.distance <= 0.01f && Physics.ComputePenetration(
                item, item.transform.position, item.transform.rotation,
                obstacle, obstacle.transform.position, obstacle.transform.rotation,
                out Vector3 separation, out float depth)
                && Vector3.Dot(direction, separation) >= -0.001f)
                continue;
            return false;
        }
        return true;
    }
}
