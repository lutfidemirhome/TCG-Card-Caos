using UnityEngine;

/// <summary>
/// 3D box used by the physics level builder. Cards spawn randomly inside this volume, then Grabbit drops them.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class PhysicsCardSpawnVolume : MonoBehaviour
{
    const float MinSize = 0.15f;

    BoxCollider _box;

    public BoxCollider Box
    {
        get
        {
            EnsureSetup();
            return _box;
        }
    }

    void Reset()
    {
        EnsureSetup(forceDefault: true);
    }

    void OnValidate()
    {
        EnsureSetup();
    }

    public void EnsureSetup(bool forceDefault = false)
    {
        _box = GetComponent<BoxCollider>();
        if (_box == null)
            _box = gameObject.AddComponent<BoxCollider>();

        _box.isTrigger = true;
        _box.enabled = true;

        if (forceDefault)
        {
            _box.size = Vector3.one;
            _box.center = Vector3.zero;
            transform.localScale = new Vector3(4f, 2f, 4f);
        }

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Max(Mathf.Abs(scale.x), MinSize);
        scale.y = Mathf.Max(Mathf.Abs(scale.y), MinSize);
        scale.z = Mathf.Max(Mathf.Abs(scale.z), MinSize);
        transform.localScale = scale;
    }

    public Vector3 GetRandomPoint(float padding)
    {
        EnsureSetup();
        float pad = Mathf.Clamp01(padding);
        float half = 0.5f - pad * 0.45f;
        Vector3 local = new Vector3(
            Random.Range(-half, half),
            Random.Range(-half, half),
            Random.Range(-half, half));
        return transform.TransformPoint(local);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        BoxCollider box = _box != null ? _box : GetComponent<BoxCollider>();
        if (box == null)
            return;

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.15f, 0.65f, 1f, 0.12f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = previous;
    }
#endif
}
