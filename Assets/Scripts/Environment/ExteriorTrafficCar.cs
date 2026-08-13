using UnityEngine;

/// <summary>
/// Moves a spawned car along an <see cref="ExteriorTrafficPath"/> and destroys itself at the end.
/// </summary>
[DisallowMultipleComponent]
public class ExteriorTrafficCar : MonoBehaviour
{
    // AE_New_York car meshes face +X, not Unity's default +Z forward.
    static readonly Vector3 ModelForwardAxis = Vector3.right;

    ExteriorTrafficPath _path;
    float _distance;
    float _speed;
    bool _reverse;
    float _rotationSpeed = 8f;

    public void Initialize(ExteriorTrafficPath path, float speed, bool reverse)
    {
        _path = path;
        _speed = speed;
        _reverse = reverse;
        _distance = reverse ? path.TotalLength : 0f;

        DisablePhysics();
        ApplyTransform(instantRotation: true);
    }

    void Update()
    {
        if (_path == null || _path.PointCount < 2)
        {
            Destroy(gameObject);
            return;
        }

        float delta = _speed * Time.deltaTime;
        _distance += _reverse ? -delta : delta;

        if (!_reverse && _distance >= _path.TotalLength)
        {
            Destroy(gameObject);
            return;
        }

        if (_reverse && _distance <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        ApplyTransform(instantRotation: false);
    }

    void ApplyTransform(bool instantRotation)
    {
        transform.position = _path.GetPositionAtDistance(_distance);

        Vector3 direction = _path.GetDirectionAtDistance(_distance);
        if (_reverse)
            direction = -direction;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = GetDrivingRotation(direction);
        transform.rotation = instantRotation
            ? targetRotation
            : Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _rotationSpeed);
    }

    public static Quaternion GetDrivingRotation(Vector3 direction)
    {
        direction.Normalize();
        return Quaternion.FromToRotation(ModelForwardAxis, direction);
    }

    void DisablePhysics()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
            rigidbodies[i].isKinematic = true;
    }
}
