using UnityEngine;

/// <summary>
/// Scene-authored floor area where scattered cards spawn.
/// Resize with the Scale tool (R) in top view. Visible only as a Scene gizmo (Gizmos on).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class CardScatterZone : MonoBehaviour
{
    const float MinFootprint = 0.25f;
    const float MinHeight = 0.02f;

    BoxCollider _boxCollider;
    bool _volumeNormalized;

    void Reset()
    {
        EnsureSetup(forceDefaultSize: true);
    }

    void Awake()
    {
        EnsureSetup();
    }

    void OnValidate()
    {
        EnsureSetup();
    }

    public void EnsureSetup(bool forceDefaultSize = false)
    {
        _boxCollider = GetComponent<BoxCollider>();
        if (_boxCollider == null)
            _boxCollider = gameObject.AddComponent<BoxCollider>();

        _boxCollider.isTrigger = true;
        _boxCollider.enabled = true;

        if (forceDefaultSize)
        {
            transform.localScale = new Vector3(6f, MinHeight, 4f);
            _boxCollider.size = Vector3.one;
            _boxCollider.center = Vector3.zero;
            _volumeNormalized = true;
        }
        else
        {
            NormalizeVolume();
        }

        RemoveVisualComponents();
    }

    void NormalizeVolume()
    {
        Vector3 combined = Vector3.Scale(_boxCollider.size, transform.localScale);
        bool needsDefault = combined.x < MinFootprint || combined.z < MinFootprint;
        if (needsDefault && !_volumeNormalized)
            combined = new Vector3(6f, MinHeight, 4f);

        Vector3 targetScale = new Vector3(
            Mathf.Max(combined.x, MinFootprint),
            Mathf.Max(combined.y, MinHeight),
            Mathf.Max(combined.z, MinFootprint));

        if (targetScale != transform.localScale
            || _boxCollider.size != Vector3.one
            || _boxCollider.center != Vector3.zero)
        {
            transform.localScale = targetScale;
            _boxCollider.size = Vector3.one;
            _boxCollider.center = Vector3.zero;
        }

        _volumeNormalized = true;
    }

    void RemoveVisualComponents()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(renderer);
            else
#endif
                Destroy(renderer);
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(meshFilter);
            else
#endif
                Destroy(meshFilter);
        }
    }

    public static CardScatterZone FindActive()
    {
        return FindFirstObjectByType<CardScatterZone>();
    }

    public Vector2 GetRandomXZ()
    {
        EnsureSetup();
        Vector3 localPoint = new Vector3(
            Random.Range(-0.5f, 0.5f),
            0f,
            Random.Range(-0.5f, 0.5f));

        Vector3 worldPoint = transform.TransformPoint(localPoint);
        return new Vector2(worldPoint.x, worldPoint.z);
    }

    public Vector2 ClampXZ(Vector2 worldXZ)
    {
        EnsureSetup();
        Vector3 localPoint = transform.InverseTransformPoint(new Vector3(worldXZ.x, transform.position.y, worldXZ.y));
        localPoint.x = Mathf.Clamp(localPoint.x, -0.5f, 0.5f);
        localPoint.z = Mathf.Clamp(localPoint.z, -0.5f, 0.5f);
        Vector3 worldPoint = transform.TransformPoint(new Vector3(localPoint.x, 0f, localPoint.z));
        return new Vector2(worldPoint.x, worldPoint.z);
    }

    public void GetWorldAabb(out float minX, out float maxX, out float minZ, out float maxZ)
    {
        EnsureSetup();
        Bounds bounds = _boxCollider.bounds;
        minX = bounds.min.x;
        maxX = bounds.max.x;
        minZ = bounds.min.z;
        maxZ = bounds.max.z;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (Application.isPlaying)
            return;

        BoxCollider box = _boxCollider != null ? _boxCollider : GetComponent<BoxCollider>();
        if (box == null)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 0.95f, 0.35f, 0.18f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.2f, 0.95f, 0.35f, 0.85f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = previousMatrix;
    }
#endif
}
