using UnityEngine;
using UnityEngine.AI;

/// <summary>Tilts only the dog's visual pivot to the grade beneath its front and back feet.</summary>
[DisallowMultipleComponent]
public sealed class ShopDogGroundAlignment : MonoBehaviour
{
    const float SampleInterval = 0.1f;
    const float ProbeSpacing = 0.8f;
    const float SampleRadius = 0.65f;
    const float MaxProbeOffset = 0.3f;
    const float MaxHeightOffset = 0.65f;
    const float MaxPitch = 50f;
    const float SmoothingTime = 0.18f;

    ShopDogArea _area;
    NavMeshAgent _agent;
    Transform _root;
    Transform _pivot;
    NavMeshQueryFilter _filter;
    Quaternion _neutralRotation;
    Vector3 _samplePosition;
    Vector3 _sampleForward;
    float _sampleRemaining;
    float _targetPitch;
    float _groundPitch;
    float _pitch;
    float _pitchVelocity;
    bool _initialized;
    bool _hasSamplePose;
    bool _hasGroundPitch;

    public bool TryGetGroundPitch(out float pitch)
    {
        pitch = _groundPitch;
        return _initialized && _hasGroundPitch;
    }

    public void Initialize(ShopDogArea area, Transform visualPivot, NavMeshAgent agent)
    {
        _area = area;
        _agent = agent;
        _pivot = visualPivot;
        _root = agent ? agent.transform : null;
        _initialized = _area && _agent && _pivot && _root
            && _pivot != _root && _pivot.IsChildOf(_root);
        if (!_initialized) return;

        _neutralRotation = _pivot.localRotation;
        _filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask & (1 << 0)
        };
        _sampleRemaining = _targetPitch = _pitch = _pitchVelocity = 0f;
        _groundPitch = 0f;
        _hasSamplePose = false;
        _hasGroundPitch = false;
    }

    void LateUpdate()
    {
        if (!_initialized || !_area || !_agent || !_pivot || !_root
            || !_area.IsReady || !_agent.enabled || !_agent.isOnNavMesh
            || GamePause.IsPaused || GameSceneLoader.IsLoading
            || !CardInstancedRenderManager.IsGameplayReady) return;

        float delta = Time.deltaTime;
        if (delta <= 0f) return;
        GameplayPerformance.Sample measurement = GameplayPerformance.BeginSample();
        _sampleRemaining -= delta;
        if (_sampleRemaining <= 0f)
        {
            _sampleRemaining = SampleInterval;
            Vector3 position = _root.position;
            Vector3 forward = _root.forward;
            // Sleeping and idle animations move bones, not the navigation root.
            // Their unchanged footprint does not need another pair of queries.
            if (!_hasSamplePose || (position - _samplePosition).sqrMagnitude > 0.000001f
                || (forward - _sampleForward).sqrMagnitude > 0.000001f)
            {
                _samplePosition = position;
                _sampleForward = forward;
                _hasSamplePose = true;
                _hasGroundPitch = TrySamplePitch(position, forward, out _groundPitch);
                _targetPitch = _hasGroundPitch ? Mathf.Clamp(_groundPitch, -MaxPitch, MaxPitch) : 0f;
            }
        }

        _pitch = Mathf.SmoothDamp(_pitch, _targetPitch, ref _pitchVelocity,
            SmoothingTime, 180f, delta);
        // NavMeshAgent keeps its own upright root and yaw. Animation bones
        // remain below this separate, unanimated visual pivot.
        Quaternion rotation = Quaternion.Euler(_pitch, 0f, 0f) * _neutralRotation;
        // Avoid dirtying the entire animated hierarchy while resting on flat ground.
        if (Mathf.Abs(Quaternion.Dot(_pivot.localRotation, rotation)) < 0.9999999f)
            _pivot.localRotation = rotation;
        GameplayPerformance.EndSample(GameplayPerformance.Area.Dog, measurement);
    }

    bool TrySamplePitch(Vector3 center, Vector3 forward, out float pitch)
    {
        pitch = 0f;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.5f) return false;
        forward.Normalize();

        center.y -= _agent.baseOffset;
        Vector3 offset = forward * (ProbeSpacing * 0.5f);
        if (!TrySample(center + offset, out Vector3 front)
            || !TrySample(center - offset, out Vector3 back)) return false;

        Vector3 grade = front - back;
        float forwardDistance = Vector3.Dot(grade, forward);
        if (forwardDistance < 0.15f) return false;
        Vector3 sideways = grade - forward * forwardDistance;
        sideways.y = 0f;
        if (sideways.sqrMagnitude > 0.15f * 0.15f) return false;

        // Individual stair tread normals point straight up. Height over the
        // two-footprint span instead captures the staircase's average slope.
        // Positive local X rotation points the model's nose down in Unity.
        pitch = -Mathf.Atan2(grade.y, forwardDistance) * Mathf.Rad2Deg;
        return true;
    }

    bool TrySample(Vector3 query, out Vector3 position)
    {
        position = default;
        // This dog's filtered NavMesh contains only authored walking surfaces:
        // no cards, furniture tops, other agents' meshes or physics mutations.
        if (!NavMesh.SamplePosition(query, out NavMeshHit hit, SampleRadius, _filter))
            return false;

        Vector3 difference = hit.position - query;
        if (Mathf.Abs(difference.y) > MaxHeightOffset) return false;
        difference.y = 0f;
        if (difference.sqrMagnitude > MaxProbeOffset * MaxProbeOffset) return false;

        position = hit.position;
        return true;
    }
}
