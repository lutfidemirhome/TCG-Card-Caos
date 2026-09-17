using UnityEngine;
using UnityEngine.AI;

/// <summary>Stable torso collision and a cached player proximity check for the shop dog.</summary>
[DisallowMultipleComponent]
public sealed class ShopDogPlayerCollision : MonoBehaviour
{
    const float BodyRadius = 0.25f;
    const float BodyLength = 0.95f;
    const float BodyCenterHeight = 0.45f;
    const float ContactMargin = 0.08f;
    const float YieldHysteresis = 0.15f;

    Transform _root;
    Transform _bodyTransform;
    Transform _playerTransform;
    CharacterController _player;
    NavMeshAgent _agent;
    CapsuleCollider _bodyCollider;
    bool _initialized;

    public void Initialize(float modelScale)
    {
        if (_initialized) return;
        _root = transform;
        _agent = GetComponent<NavMeshAgent>();
        // Resolve the player once. Subsequent movement checks only compare the
        // two cached shapes, without scanning cards or issuing physics queries.
        FirstPersonController[] players = Object.FindObjectsByType<FirstPersonController>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (FirstPersonController candidate in players)
        {
            if (candidate.gameObject.scene != gameObject.scene) continue;
            _player = candidate.GetComponent<CharacterController>();
            if (!_player) continue;
            _playerTransform = candidate.transform;
            break;
        }
        if (!_player || !_agent)
        {
            Debug.LogWarning("[Shop Dog] Oyuncu veya gezinme bileşeni bulunamadı; köpek çarpışması eklenmedi.", this);
            return;
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (!body) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.detectCollisions = true;
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;

        GameObject collisionObject = new GameObject("Player Collision");
        collisionObject.layer = 2; // Ignore Raycast; contact filtering is independent of query layers.
        _bodyTransform = collisionObject.transform;
        // The navigation root is upright. Neither animated bones nor the visual
        // stair-alignment pivot moves this torso collider relative to the agent.
        _bodyTransform.SetParent(_root, false);
        _bodyCollider = collisionObject.AddComponent<CapsuleCollider>();
        float scale = Mathf.Max(0.025f, modelScale / 4f);
        _bodyCollider.direction = 2;
        _bodyCollider.radius = BodyRadius * scale;
        _bodyCollider.height = BodyLength * scale;
        _bodyCollider.center = new Vector3(0f, BodyCenterHeight, 0.02f) * scale;
        int playerLayer = 1 << _player.gameObject.layer;
        _bodyCollider.includeLayers = playerLayer;
        _bodyCollider.excludeLayers = ~playerLayer;
        _bodyCollider.layerOverridePriority = 50;
        _bodyCollider.isTrigger = false;
        _initialized = true;
    }

    public bool ShouldYieldToPlayer(Vector3 steeringTarget, float plannedSpeed, bool alreadyYielding)
    {
        if (!_initialized || !isActiveAndEnabled || !_root || !_bodyCollider || !_bodyCollider.enabled
            || !_player || !_playerTransform
            || !_player.enabled || !_player.gameObject.activeInHierarchy) return false;

        Vector3 bodyScale = _bodyTransform.lossyScale;
        Vector3 playerScale = _playerTransform.lossyScale;
        float bodyRadius = _bodyCollider.radius * Mathf.Max(Mathf.Abs(bodyScale.x), Mathf.Abs(bodyScale.y));
        float bodyHalfLength = Mathf.Max(bodyRadius, _bodyCollider.height * Mathf.Abs(bodyScale.z) * 0.5f);
        float playerRadius = _player.radius * Mathf.Max(Mathf.Abs(playerScale.x), Mathf.Abs(playerScale.z));
        float playerHalfHeight = Mathf.Max(playerRadius, _player.height * Mathf.Abs(playerScale.y) * 0.5f);
        Vector3 bodyCenter = _bodyTransform.TransformPoint(_bodyCollider.center);
        Vector3 playerCenter = _playerTransform.TransformPoint(_player.center);
        if (playerCenter.y - playerHalfHeight > bodyCenter.y + bodyRadius + ContactMargin
            || playerCenter.y + playerHalfHeight < bodyCenter.y - bodyRadius - ContactMargin) return false;

        Vector3 bodyForward = _root.forward;
        bodyForward.y = 0f;
        bodyForward.Normalize();
        Vector3 toPlayer = playerCenter - bodyCenter;
        toPlayer.y = 0f;
        float halfCylinder = bodyHalfLength - bodyRadius;
        float alongBody = Mathf.Clamp(Vector3.Dot(toPlayer, bodyForward), -halfCylinder, halfCylinder);
        Vector3 fromCapsuleAxis = toPlayer - bodyForward * alongBody;
        float margin = ContactMargin + (alreadyYielding ? YieldHysteresis : 0f);
        float contactDistance = bodyRadius + playerRadius + margin;
        // An existing overlap counts even behind the dog; it should not keep
        // sliding its kinematic body into a player already touching the torso.
        if (fromCapsuleAxis.sqrMagnitude <= contactDistance * contactDistance) return true;

        Vector3 direction = steeringTarget - _root.position;
        direction.y = 0f;
        float cornerDistance = direction.magnitude;
        if (cornerDistance <= 0.01f) return false;
        direction /= cornerDistance;
        float forwardDistance = Vector3.Dot(toPlayer, direction);
        if (forwardDistance <= 0f) return false;

        // Account for the body's current yaw while it turns toward its next
        // corner. The capsule extends farther sideways during such a turn.
        float forwardDot = Mathf.Clamp(Vector3.Dot(bodyForward, direction), -1f, 1f);
        float projectedRadius = bodyRadius + halfCylinder * Mathf.Sqrt(Mathf.Max(0f, 1f - forwardDot * forwardDot));
        float corridorWidth = projectedRadius + playerRadius + margin;
        float sidewaysSquared = Mathf.Max(0f, toPlayer.sqrMagnitude - forwardDistance * forwardDistance);
        if (sidewaysSquared > corridorWidth * corridorWidth) return false;

        float acceleration = _agent ? Mathf.Max(0.1f, _agent.acceleration) : 3f;
        float speed = Mathf.Max(0f, plannedSpeed);
        float clearance = bodyHalfLength + playerRadius + 0.12f;
        // Include travel before the controller's next 10 Hz proximity check.
        float stopDistance = Mathf.Clamp(clearance + speed * speed / (2f * acceleration) + speed * 0.1f,
            clearance, Mathf.Max(clearance, 2.8f));
        if (alreadyYielding) stopDistance += YieldHysteresis;
        // A player beyond the next bend should not hold up a dog whose path is
        // about to turn away. This check follows only the current straight leg.
        stopDistance = Mathf.Min(stopDistance, cornerDistance + playerRadius + 0.12f);
        return forwardDistance <= stopDistance;
    }

    void OnEnable()
    {
        if (_initialized && _bodyCollider) _bodyCollider.enabled = true;
    }

    void OnDisable()
    {
        if (_bodyCollider) _bodyCollider.enabled = false;
    }
}
