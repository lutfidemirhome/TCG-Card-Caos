using UnityEngine;
using UnityEngine.AI;

/// <summary>Controls only the shop dog's navigation and the vendor's legacy animations.</summary>
[DisallowMultipleComponent]
public sealed class ShopDogController : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float walkSpeed = 0.7f;
    [SerializeField, Min(0.1f)] private float runSpeed = 3.6f;

    // Estimated from the original planted-paw travel at visual scale 4. The
    // run clip moves its feet much faster than the former 1.65 m/s agent speed.
    private const float WalkAuthoredSpeed = 0.95f;
    private const float RunAuthoredSpeed = 4.6f;

    private enum Behaviour { Waiting, Idle, Moving, LyingDown, Sleeping, Waking, Voice }

    private readonly System.Random _random = new System.Random();
    private ShopDogArea _area;
    private NavMeshAgent _agent;
    private ShopDogGroundAlignment _alignment;
    private ShopDogPlayerCollision _playerCollision;
    private Animation _animation;
    private AnimationState _idle, _idle2, _walk, _run, _lie, _sleep, _voice, _playing;
    private Behaviour _behaviour;
    private Vector3 _progressPosition;
    private float _remaining, _travelTime, _withoutProgress;
    private bool _initialized, _prepared, _paused, _canRest = true, _restedHere, _running, _preferRunning;
    private float _gaitElapsed, _safeRunTime, _walkCooldown, _animationScale = 1f;
    private int _journeyCount;
    private bool _yielding;
    private float _playerCheckRemaining;

    public bool PrepareAnimations(Animation animation, AnimationClip[] clips)
    {
        if (_prepared) return _idle != null && _walk != null;
        _animation = animation;
        if (!_animation) return false;

        _animation.playAutomatically = false;
        _animation.Stop();
        // One ambient dog's animation clock must keep running when the camera
        // turns away. Only the mesh skinning/rendering is culled off screen.
        // Restarting/sampling a single state on visibility changes loses blends.
        _animation.cullingType = AnimationCullingType.AlwaysAnimate;
        if (clips != null)
        {
            foreach (AnimationClip clip in clips)
            {
                if (clip && clip.legacy && _animation[clip.name] == null)
                    _animation.AddClip(clip, clip.name);
            }
        }

        _idle = _animation["Idle1"];
        _idle2 = _animation["Idle2"];
        _walk = _animation["Walk"];
        _run = _animation["run"];
        _lie = _animation["Idle_sleep"];
        _sleep = _animation["Sleep_loop"];
        _voice = _animation["Voice"];
        _prepared = true;
        return _idle != null && _walk != null;
    }

    public void Initialize(ShopDogArea area, NavMeshAgent agent, ShopDogGroundAlignment alignment,
        ShopDogPlayerCollision playerCollision)
    {
        _area = area;
        _agent = agent;
        _alignment = alignment;
        _playerCollision = playerCollision;
        _initialized = _area && _agent && _animation && _prepared && _idle != null && _walk != null;
        if (!_initialized)
        {
            StopMoving();
            Debug.LogWarning("[Shop Dog] Idle1 veya Walk animasyonu eksik; köpek hareket ettirilmiyor.", this);
            return;
        }

        _animationScale = Mathf.Max(0.025f, _area.ModelScale / 4f);
        _behaviour = Behaviour.Waiting;
        SetPaused(true);
    }

    private void Update()
    {
        var sample = GameplayPerformance.BeginSample();
        UpdateBehaviour();
        GameplayPerformance.EndSample(GameplayPerformance.Area.Dog, sample);
    }

    private void UpdateBehaviour()
    {
        if (!_initialized || !_area || !_agent || !_animation) return;
        bool paused = !_area.IsReady || !_agent.enabled || !_agent.isOnNavMesh
            || GamePause.IsPaused || GameSceneLoader.IsLoading || !CardInstancedRenderManager.IsGameplayReady;
        SetPaused(paused);
        if (paused) return;

        float delta = Time.deltaTime;
        if (_behaviour == Behaviour.Waiting)
        {
            BeginIdle(2f, 4f);
            return;
        }
        if (_behaviour == Behaviour.Moving)
        {
            UpdateMovement(delta);
            return;
        }

        _remaining -= delta;
        if (_remaining > 0f) return;
        switch (_behaviour)
        {
            case Behaviour.LyingDown:
                _behaviour = Behaviour.Sleeping;
                Play(_sleep, WrapMode.Loop);
                _remaining = Range(10f, 25f);
                break;
            case Behaviour.Sleeping:
                _behaviour = Behaviour.Waking;
                // The package has a lie-down transition but no separate wake-up clip.
                Play(_lie, WrapMode.ClampForever, true);
                _remaining = _lie.length;
                break;
            case Behaviour.Waking:
            case Behaviour.Voice:
                BeginIdle(2f, 5f);
                break;
            case Behaviour.Idle:
                ChooseBehaviour();
                break;
        }
    }

    private void ChooseBehaviour()
    {
        double choice = _random.NextDouble();
        if (_canRest && !_restedHere && choice < 0.2 && _lie != null && _sleep != null)
        {
            _restedHere = true;
            StopMoving();
            _behaviour = Behaviour.LyingDown;
            Play(_lie, WrapMode.ClampForever);
            _remaining = _lie.length;
        }
        else if (_canRest && !_restedHere && choice < 0.25 && _voice != null)
        {
            _restedHere = true;
            StopMoving();
            _behaviour = Behaviour.Voice;
            Play(_voice, WrapMode.ClampForever);
            _remaining = _voice.length;
        }
        else if (!TryStartMoving())
        {
            BeginIdle(3f, 6f);
        }
    }

    private bool TryStartMoving()
    {
        if (!_area.TryFindRoute(transform.position, out NavMeshPath route, out _))
            return false;

        _running = false;
        _agent.speed = walkSpeed;
        _agent.isStopped = false;
        // The area already checked this complete path; don't calculate it twice.
        if (!_agent.SetPath(route))
        {
            StopMoving();
            return false;
        }
        // Four trips prefer running; one is a calm walk even on open ground.
        // Start the first walk on trip three. The five-trip cycle also rotates
        // walking across the four-leg floor itinerary instead of one fixed floor.
        _preferRunning = _run != null && _journeyCount++ % 5 != 2;
        _gaitElapsed = _safeRunTime = _walkCooldown = 0f;
        _yielding = false;
        _playerCheckRemaining = 0f;

        _canRest = false;
        _restedHere = false;
        _behaviour = Behaviour.Moving;
        _progressPosition = transform.position;
        _travelTime = _withoutProgress = 0f;
        Play(_walk, WrapMode.Loop);
        return true;
    }

    private void UpdateMovement(float delta)
    {
        // Wait without throwing away the route or counting this as being stuck.
        // The player can bump the stationary body; the dog must not push into them.
        if (UpdatePlayerYield(delta)) return;
        _travelTime += delta;
        _withoutProgress += delta;
        if ((transform.position - _progressPosition).sqrMagnitude > 0.0025f)
        {
            _progressPosition = transform.position;
            _withoutProgress = 0f;
        }

        if (_withoutProgress > 4f || _travelTime > 180f)
        {
            // A temporary obstacle must not leave the dog walking in place indefinitely.
            BeginIdle(2f, 4f);
            return;
        }
        if (_agent.pathPending) return;
        if (_agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            BeginIdle(2f, 4f);
            return;
        }
        if (_agent.remainingDistance <= _agent.stoppingDistance + 0.06f)
        {
            // Only successfully reached floor destinations may be used for sleeping.
            _area.ConfirmArrival(transform.position);
            _canRest = true;
            BeginIdle(3f, 8f);
            return;
        }
        if (!_agent.hasPath && _travelTime > 0.5f)
        {
            BeginIdle(2f, 4f);
            return;
        }

        UpdateGait(delta);
        if (_playing != null)
        {
            float target = MovementPlaybackSpeed(_playing);
            _playing.speed = Mathf.MoveTowards(_playing.speed, target, delta * 6f);
        }
    }

    private bool UpdatePlayerYield(float delta)
    {
        _playerCheckRemaining -= delta;
        if (_playerCheckRemaining > 0f) return _yielding;
        _playerCheckRemaining = 0.1f;
        // Use the planned speed even while stopped, so the release distance does
        // not shrink and repeatedly restart the dog in front of a standing player.
        bool wait = _playerCollision && _playerCollision.ShouldYieldToPlayer(
            _agent.steeringTarget, _preferRunning ? runSpeed : walkSpeed, _yielding);
        if (wait == _yielding) return _yielding;

        _yielding = wait;
        _running = false;
        _agent.isStopped = wait;
        if (wait)
        {
            Play(_idle, WrapMode.Loop);
        }
        else
        {
            _agent.speed = walkSpeed;
            _progressPosition = transform.position;
            _withoutProgress = _gaitElapsed = _safeRunTime = 0f;
            _walkCooldown = 0.5f;
            Play(_walk, WrapMode.Loop);
        }
        return _yielding;
    }

    private void UpdateGait(float delta)
    {
        _walkCooldown = Mathf.Max(0f, _walkCooldown - delta);
        _gaitElapsed += delta;
        if (_gaitElapsed < 0.1f) return;
        float elapsed = _gaitElapsed;
        _gaitElapsed = 0f;

        Vector3 toCorner = _agent.steeringTarget - transform.position;
        toCorner.y = 0f;
        bool level = _alignment && _alignment.TryGetGroundPitch(out float pitch)
            && Mathf.Abs(pitch) < (_running ? 9f : 5f)
            // Match the floor tolerance used when validating destinations: voxel
            // height offsets must not reject an otherwise valid, level corridor.
            && _area.IsAtFloorHeight(transform.position, 0.2f);
        // At 3.6 m/s and 5 m/s² acceleration, slowing to walking needs ~1.25 m,
        // plus the distance travelled before the next 0.1-second gait check.
        float cornerDistance = _running ? 1.8f : 2.5f;
        bool straight = toCorner.sqrMagnitude > cornerDistance * cornerDistance
            && Vector3.Angle(transform.forward, toCorner) < (_running ? 40f : 20f);
        bool canRun = _preferRunning && level && straight
            && _agent.remainingDistance > (_running ? 2f : 3f);

        if (_running)
        {
            if (!canRun)
            {
                SetRunning(false);
                _walkCooldown = 0.35f;
                _safeRunTime = 0f;
            }
        }
        else
        {
            _safeRunTime = canRun ? _safeRunTime + elapsed : 0f;
            if (_safeRunTime >= 0.2f && _walkCooldown <= 0f) SetRunning(true);
        }
    }

    private void SetRunning(bool running)
    {
        if (_running == running) return;
        _running = running;
        _agent.speed = running ? runSpeed : walkSpeed;
        Play(running ? _run : _walk, WrapMode.Loop);
    }

    private float MovementPlaybackSpeed(AnimationState state)
    {
        float authored = state == _run ? RunAuthoredSpeed : WalkAuthoredSpeed;
        // Use actual movement, not desired speed: slowing for a corner must slow
        // the paws too. Zero velocity no longer forces a minimum walking cycle.
        return Mathf.Clamp(_agent.velocity.magnitude / (authored * _animationScale), 0f, 3f);
    }

    private void BeginIdle(float minimum, float maximum)
    {
        _running = false;
        _yielding = false;
        StopMoving();
        _behaviour = Behaviour.Idle;
        AnimationState idle = _idle2 != null && _random.NextDouble() < 0.35 ? _idle2 : _idle;
        Play(idle, WrapMode.Loop);
        _remaining = Mathf.Max(Range(minimum, maximum), idle.length);
    }

    private void Play(AnimationState state, WrapMode wrapMode, bool reverse = false)
    {
        if (state == null) return;
        state.wrapMode = wrapMode;
        _animation.CrossFade(state.name, 0.2f, PlayMode.StopAll);
        state.speed = state == _walk || state == _run ? MovementPlaybackSpeed(state) : reverse ? -1f : 1f;
        state.time = reverse ? Mathf.Max(0f, state.length - 0.001f) : 0f;
        _playing = state;
    }

    private void SetPaused(bool paused)
    {
        if (_paused == paused) return;
        _paused = paused;
        if (_animation) _animation.enabled = !paused;
        if (_agent && _agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = paused || _yielding || _behaviour != Behaviour.Moving;
    }

    private void StopMoving()
    {
        if (!_agent || !_agent.enabled || !_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.ResetPath();
    }

    private float Range(float minimum, float maximum)
    {
        return minimum + (maximum - minimum) * (float)_random.NextDouble();
    }

    private void OnDisable()
    {
        StopMoving();
        if (_animation) _animation.Stop();
        _playing = null;
        _behaviour = Behaviour.Waiting;
        _running = _preferRunning = false;
        _yielding = false;
    }
}
