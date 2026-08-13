using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves a spawned car along an <see cref="ExteriorTrafficPath"/> and destroys itself at the end.
/// </summary>
[DisallowMultipleComponent]
public class ExteriorTrafficCar : MonoBehaviour
{
    static int _activeCount;

    public static int ActiveCount => _activeCount;
    struct SpinningWheel
    {
        public Transform Transform;
        public Quaternion BaseLocalRotation;
        public Vector3 LocalSpinAxis;
        public int SpinSign;
        public float Angle;
    }

    // AE_New_York car meshes face +X, not Unity's default +Z forward.
    static readonly Vector3 ModelForwardAxis = Vector3.right;

    [SerializeField] float wheelRadius = 0.36f;

    ExteriorTrafficPath _path;
    float _distance;
    float _speed;
    bool _reverse;
    float _rotationSpeed = 8f;
    SpinningWheel[] _wheels;

    public void Initialize(ExteriorTrafficPath path, float speed, bool reverse)
    {
        _path = path;
        _speed = speed;
        _reverse = reverse;
        _distance = reverse ? path.TotalLength : 0f;

        DisablePhysics();
        CacheWheels();
        ApplyTransform(instantRotation: true);
    }

    void OnEnable()
    {
        _activeCount++;
    }

    void OnDestroy()
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
    }

    void Update()
    {
        if (_path == null || _path.PointCount < 2)
        {
            Destroy(gameObject);
            return;
        }

        float delta = _speed * Time.deltaTime;
        float movementDelta = _reverse ? -delta : delta;
        _distance += movementDelta;

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
        UpdateWheelSpin(movementDelta);
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

    void CacheWheels()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        var wheels = new List<SpinningWheel>();

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform wheelTransform = transforms[i];
            if (wheelTransform == transform || !IsWheelTransform(wheelTransform))
                continue;

            Vector3 spinAxis = ResolveWheelSpinAxis(wheelTransform);
            wheels.Add(new SpinningWheel
            {
                Transform = wheelTransform,
                BaseLocalRotation = wheelTransform.localRotation,
                LocalSpinAxis = spinAxis,
                SpinSign = ResolveWheelSpinSign(spinAxis),
                Angle = 0f
            });
        }

        _wheels = wheels.ToArray();
    }

    static bool IsWheelTransform(Transform wheelTransform)
    {
        string name = wheelTransform.name;
        return name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("whell", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static Vector3 ResolveWheelSpinAxis(Transform wheelTransform)
    {
        MeshFilter meshFilter = wheelTransform.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Vector3 size = meshFilter.sharedMesh.bounds.size;
            if (size.x <= size.y && size.x <= size.z)
                return Vector3.right;

            if (size.y <= size.x && size.y <= size.z)
                return Vector3.up;

            return Vector3.forward;
        }

        return Vector3.up;
    }

    static int ResolveWheelSpinSign(Vector3 localSpinAxis)
    {
        Vector3 rollDirection = Vector3.Cross(localSpinAxis, Vector3.up);
        if (rollDirection.sqrMagnitude <= 0.0001f)
            rollDirection = Vector3.Cross(localSpinAxis, Vector3.forward);

        rollDirection.Normalize();
        return Vector3.Dot(rollDirection, ModelForwardAxis) >= 0f ? 1 : -1;
    }

    void UpdateWheelSpin(float movementDelta)
    {
        if (_wheels == null || _wheels.Length == 0 || wheelRadius <= 0.0001f)
            return;

        float angleDelta = movementDelta / wheelRadius * Mathf.Rad2Deg;
        for (int i = 0; i < _wheels.Length; i++)
        {
            SpinningWheel wheel = _wheels[i];
            if (wheel.Transform == null)
                continue;

            wheel.Angle += angleDelta * wheel.SpinSign;
            wheel.Transform.localRotation = wheel.BaseLocalRotation
                * Quaternion.AngleAxis(wheel.Angle, wheel.LocalSpinAxis);
            _wheels[i] = wheel;
        }
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
