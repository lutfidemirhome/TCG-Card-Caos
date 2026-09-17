using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Owns a small, isolated navigation surface for the shop's ambient dog.</summary>
[DisallowMultipleComponent]
public sealed class ShopDogArea : MonoBehaviour
{
    public const int AgentTypeId = 613740349;

    [Header("Dog")]
    [SerializeField] GameObject dogPrefab;
    [SerializeField] AnimationClip[] animations;
    [SerializeField, Min(0.1f)] float modelScale = 4f;

    [Header("Interior (world coordinates)")]
    [SerializeField] Bounds navigationBounds = new Bounds(new Vector3(2.1f, 2.9f, -2.075f), new Vector3(23.4f, 7f, 23.15f));
    [SerializeField] Vector3 spawnPoint = new Vector3(0f, 0.06f, 3f);
    [SerializeField] Vector3[] roamingPoints;
    [SerializeField] string[] walkableRootNames = { "Floor_Var01", "Ladder" };
    [Tooltip("The base floor and the structural boxes supporting the upstairs floor.")]
    [SerializeField] Collider[] additionalWalkableSurfaces;
    [SerializeField] float groundHeight = 0.06f;
    [SerializeField] float upstairsHeight = 4.304f;

    readonly List<NavMeshBuildSource> _sources = new List<NavMeshBuildSource>(256);
    readonly List<Vector3> _destinations = new List<Vector3>(32);
    readonly System.Random _random = new System.Random();
    NavMeshPath _path;
    NavMeshQueryFilter _filter;
    NavMeshData _data;
    NavMeshDataInstance _instance;
    AsyncOperation _build;
    Coroutine _routine;
    GameObject _dog;
    Transform _visualPivot;
    Animation _dogAnimation;
    bool _started;
    bool _visitUpper, _localVisitComplete, _hasGround, _hasUpper;
    bool _selectedTransfer, _selectedUpper, _hasSelectedDestination;
    Vector3 _selectedDestination;

    public bool IsReady { get; private set; }
    public float ModelScale => modelScale;
    public Bounds NavigationBounds => navigationBounds;
    public Vector3 SpawnPoint => spawnPoint;
    public IReadOnlyList<Vector3> RoamingPoints => roamingPoints;

    void OnEnable()
    {
        if (Application.isPlaying && !_started)
        {
            _started = true;
            _routine = StartCoroutine(Prepare());
        }
    }

    IEnumerator Prepare()
    {
        if (!dogPrefab || roamingPoints == null || roamingPoints.Length == 0)
        {
            Debug.LogWarning("[Shop Dog] Köpek modeli veya dolaşma noktaları eksik.", this);
            yield break;
        }
        // Prepare the imported bone hierarchy while the scene is still loading,
        // so revealing the dog later does not instantiate hundreds of transforms.
        if (!PrepareVisual()) yield break;

        // Scene bootstrap and restored card transforms must finish first. Navigation
        // is optional scenery and must never hold up the game's loading screen.
        yield return null;
        while (!CardInstancedRenderManager.IsGameplayReady || GameSceneLoader.IsLoading)
            yield return null;
        yield return null;

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(AgentTypeId);
        if (settings.agentTypeID != AgentTypeId)
        {
            Debug.LogWarning("[Shop Dog] Navigation ayarlarında Shop Dog agent türü eksik.", this);
            yield break;
        }
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.08f;
        settings.overrideTileSize = true;
        settings.tileSize = 128;
        settings.maxJobWorkers = 2;
        settings.buildHeightMesh = false;

        // One bounded physics query, then small batches. Cards are excluded in the
        // query itself, so their thousands of colliders never enter this work list.
        int worldCardLayer = LayerMask.NameToLayer("WorldCard");
        int mask = Physics.DefaultRaycastLayers;
        if (worldCardLayer >= 0) mask &= ~(1 << worldCardLayer);
        Collider[] colliders = Physics.OverlapBox(navigationBounds.center, navigationBounds.extents,
            Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
        var explicitFloors = new HashSet<Collider>();
        if (additionalWalkableSurfaces != null)
            foreach (Collider floor in additionalWalkableSurfaces)
                if (floor) explicitFloors.Add(floor);

        var budget = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (ShouldInclude(collider))
            {
                bool walkable = explicitFloors.Contains(collider) || IsWalkableRoot(collider.transform);
                if (TryCreateSource(collider, walkable ? 0 : 1, out NavMeshBuildSource source))
                    _sources.Add(source);
            }
            if ((i & 31) == 31 && budget.Elapsed.TotalMilliseconds >= 1.5)
            {
                yield return null;
                budget.Restart();
            }
        }
        colliders = null;
        if (_sources.Count == 0)
        {
            Debug.LogWarning("[Shop Dog] Dükkân için gezilebilir yüzey bulunamadı.", this);
            yield break;
        }

        _data = new NavMeshData(AgentTypeId) { name = "Shop Dog Navigation" };
        _build = NavMeshBuilder.UpdateNavMeshDataAsync(_data, settings, _sources, navigationBounds);
        while (!_build.isDone) yield return null;
        _build = null;
        _instance = NavMesh.AddNavMeshData(_data);
        _filter = new NavMeshQueryFilter { agentTypeID = AgentTypeId, areaMask = 1 << 0 };
        _path = new NavMeshPath();

        if (!TrySampleFloor(spawnPoint, 1.25f, out Vector3 start))
        {
            Debug.LogWarning("[Shop Dog] Başlangıç noktasında güvenli zemin bulunamadı; köpek oluşturulmadı.", this);
            ReleaseNavigation();
            yield break;
        }

        int upperPoints = 0;
        for (int i = 0; i < roamingPoints.Length; i++)
        {
            if (TrySampleFloor(roamingPoints[i], 0.8f, out Vector3 point)
                && NavMesh.CalculatePath(start, point, _filter, _path)
                && _path.status == NavMeshPathStatus.PathComplete)
            {
                _destinations.Add(point);
                if (Mathf.Abs(point.y - upstairsHeight) < 0.2f) upperPoints++;
            }
            // Validate the two-floor routes once, without bunching the searches
            // into a single gameplay frame. Never jump across disconnected floors.
            if ((i & 3) == 3) yield return null;
        }
        int sourceCount = _sources.Count;
        _sources.Clear();
        if (_destinations.Count < 2)
        {
            Debug.LogWarning("[Shop Dog] Birbirine bağlı yeterli dolaşma noktası bulunamadı; köpek oluşturulmadı.", this);
            ReleaseNavigation();
            yield break;
        }
        if (upperPoints == 0)
            Debug.LogWarning("[Shop Dog] Üst kata kesintisiz merdiven rotası bulunamadı. Köpek erişilebilir alt katta kalacak.", this);

        _hasUpper = upperPoints > 0;
        _hasGround = upperPoints < _destinations.Count;
        _visitUpper = IsUpperFloor(start);
        _localVisitComplete = false;

        _dog.transform.SetPositionAndRotation(start, Quaternion.identity);
        NavMeshAgent agent = _dog.AddComponent<NavMeshAgent>();
        agent.agentTypeID = AgentTypeId;
        agent.areaMask = _filter.areaMask;
        agent.radius = settings.agentRadius;
        agent.height = settings.agentHeight;
        agent.baseOffset = 0f;
        agent.stoppingDistance = 0.12f;
        agent.angularSpeed = 240f;
        agent.acceleration = 5f;
        agent.autoBraking = true;
        agent.autoRepath = false;
        agent.autoTraverseOffMeshLink = false;
        agent.updateUpAxis = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        ShopDogController controller = _dog.GetComponent<ShopDogController>();
        _dog.SetActive(true);
        if (!agent.isOnNavMesh || !agent.Warp(start))
        {
            Debug.LogWarning("[Shop Dog] Köpek güvenli dolaşma yüzeyine yerleştirilemedi.", this);
            Destroy(_dog);
            ReleaseNavigation();
            yield break;
        }
        IsReady = true;
        ShopDogGroundAlignment alignment = _dog.AddComponent<ShopDogGroundAlignment>();
        alignment.Initialize(this, _visualPivot, agent);
        ShopDogPlayerCollision playerCollision = _dog.AddComponent<ShopDogPlayerCollision>();
        playerCollision.Initialize(modelScale);
        controller.Initialize(this, agent, alignment, playerCollision);
        _routine = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Shop Dog] Hazır: {_destinations.Count} dolaşma noktası, üst katta {upperPoints}, {sourceCount} sabit yüzey.", this);
#endif
    }

    bool PrepareVisual()
    {
        // This neutral parent stays out of card physics, raycasts and save records.
        _dog = new GameObject("Shop Dog");
        _dog.SetActive(false);
        _dog.transform.SetParent(transform, false);
        _visualPivot = new GameObject("Ground Alignment").transform;
        _visualPivot.SetParent(_dog.transform, false);
        GameObject visual = Instantiate(dogPrefab, _visualPivot);
        visual.name = "Dog Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * modelScale;
        _dogAnimation = visual.GetComponentInChildren<Animation>(true);
        if (!_dogAnimation)
        {
            Debug.LogWarning("[Shop Dog] Modelin Animation bileşeni bulunamadı.", this);
            Destroy(_dog);
            return false;
        }
        _dogAnimation.playAutomatically = false;
        foreach (SkinnedMeshRenderer renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            renderer.updateWhenOffscreen = false;
        if (!_dog.AddComponent<ShopDogController>().PrepareAnimations(_dogAnimation, animations))
        {
            Debug.LogWarning("[Shop Dog] Yürüme/bekleme klipleri hazırlanamadı; köpek oluşturulmadı.", this);
            Destroy(_dog);
            return false;
        }
        return true;
    }

    bool ShouldInclude(Collider collider)
    {
        if (!collider || !collider.enabled || collider.isTrigger
            || collider.gameObject.scene != gameObject.scene || collider is CharacterController
            || (collider.excludeLayers.value & 1) != 0) return false;
        if (collider.attachedRigidbody && !collider.attachedRigidbody.isKinematic) return false;
        // Defensively exclude held items even if their layer has been changed.
        return !collider.GetComponentInParent<WorldCard>()
            && !collider.GetComponentInParent<WorldBoosterPack>()
            && !collider.GetComponentInParent<FirstPersonController>()
            && !collider.GetComponentInParent<CardShelfSlot>()
            && !collider.GetComponentInParent<PsaCabinetSlot>()
            && !collider.transform.IsChildOf(transform);
    }

    bool IsWalkableRoot(Transform item)
    {
        if (walkableRootNames == null) return false;
        for (Transform current = item; current; current = current.parent)
            foreach (string rootName in walkableRootNames)
                if (!string.IsNullOrEmpty(rootName) && (current.name == rootName
                    || current.name.StartsWith(rootName + " (", StringComparison.Ordinal))) return true;
        return false;
    }

    static bool TryCreateSource(Collider collider, int area, out NavMeshBuildSource source)
    {
        source = new NavMeshBuildSource { area = area, component = collider };
        Matrix4x4 matrix = collider.transform.localToWorldMatrix;
        if (collider is BoxCollider box)
        {
            source.shape = NavMeshBuildSourceShape.Box;
            source.transform = matrix * Matrix4x4.Translate(box.center);
            source.size = box.size;
        }
        else if (collider is MeshCollider meshCollider && meshCollider.sharedMesh)
        {
            Mesh mesh = meshCollider.sharedMesh;
            if (mesh.isReadable)
            {
                // In particular, use the real, already-readable staircase mesh.
                source.shape = NavMeshBuildSourceShape.Mesh;
                source.sourceObject = mesh;
                source.transform = matrix;
            }
            else
            {
                // Bounds remain readable for imported GPU-only meshes. Keep their
                // orientation; a world AABB would block too much near rotated props.
                source.shape = NavMeshBuildSourceShape.Box;
                source.transform = matrix * Matrix4x4.Translate(mesh.bounds.center);
                Vector3 size = mesh.bounds.size;
                source.size = new Vector3(Mathf.Max(0.002f, size.x), Mathf.Max(0.002f, size.y), Mathf.Max(0.002f, size.z));
            }
        }
        else if (collider is SphereCollider sphere)
        {
            source.shape = NavMeshBuildSourceShape.Sphere;
            source.transform = matrix * Matrix4x4.Translate(sphere.center);
            source.size = Vector3.one * (sphere.radius * 2f);
        }
        else if (collider is CapsuleCollider capsule)
        {
            Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 2 ? Vector3.forward : Vector3.up;
            source.shape = NavMeshBuildSourceShape.Capsule;
            source.transform = matrix * Matrix4x4.TRS(capsule.center, Quaternion.FromToRotation(Vector3.up, axis), Vector3.one);
            source.size = new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f);
        }
        else return false;
        return true;
    }

    bool TrySampleFloor(Vector3 point, float radius, out Vector3 result)
    {
        result = default;
        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, radius, _filter)
            || !navigationBounds.Contains(hit.position) || Mathf.Abs(hit.position.y - point.y) > 0.2f) return false;
        if (Mathf.Abs(hit.position.y - groundHeight) > 0.2f
            && Mathf.Abs(hit.position.y - upstairsHeight) > 0.2f) return false;
        result = hit.position;
        return true;
    }

    bool IsUpperFloor(Vector3 position)
    {
        return Mathf.Abs(position.y - upstairsHeight) < Mathf.Abs(position.y - groundHeight);
    }

    public bool TryFindRoute(Vector3 from, out NavMeshPath route, out bool changesFloor)
    {
        route = null;
        changesFloor = false;
        _hasSelectedDestination = false;
        if (!IsReady || _destinations.Count == 0) return false;
        // One local excursion, then transfer to the opposite floor. A floor with
        // more authored points must not get more visits. Failed trips don't count.
        bool canChangeFloor = _hasUpper && _hasGround;
        if (canChangeFloor && !_localVisitComplete && IsUpperFloor(from) == _visitUpper
            && Mathf.Abs(from.y - (_visitUpper ? upstairsHeight : groundHeight)) < 0.2f)
        {
            bool hasLocalExcursion = false;
            foreach (Vector3 point in _destinations)
                if (IsUpperFloor(point) == _visitUpper && (point - from).sqrMagnitude >= 4f)
                {
                    hasLocalExcursion = true;
                    break;
                }
            // A floor with only its arrival point still counts as a visit. Don't
            // wait forever for a second local point after an authoring change.
            if (!hasLocalExcursion) _localVisitComplete = true;
        }
        bool targetUpper = _localVisitComplete && canChangeFloor ? !_visitUpper : _visitUpper;
        int pathQueries = 0;
        int offset = _random.Next(_destinations.Count);
        // Prefer enough space for an actual run; short routes remain a fallback.
        for (int pass = 0; pass < 2 && pathQueries < 3; pass++)
        for (int i = 0; i < _destinations.Count && pathQueries < 3; i++)
        {
            Vector3 candidate = _destinations[(offset + i) % _destinations.Count];
            if (IsUpperFloor(candidate) != targetUpper) continue;
            float distanceSquared = (candidate - from).sqrMagnitude;
            if (distanceSquared < 4f || (pass == 0 ? distanceSquared < 16f : distanceSquared >= 16f)) continue;
            bool otherFloor = Mathf.Abs(candidate.y - from.y) > 1f;
            pathQueries++;
            if (!NavMesh.CalculatePath(from, candidate, _filter, _path)
                || _path.status != NavMeshPathStatus.PathComplete) continue;
            route = _path;
            changesFloor = otherFloor || (Mathf.Abs(from.y - groundHeight) > 0.2f
                && Mathf.Abs(from.y - upstairsHeight) > 0.2f);
            _selectedDestination = candidate;
            _selectedUpper = targetUpper;
            _selectedTransfer = targetUpper != _visitUpper;
            _hasSelectedDestination = true;
            return true;
        }
        return false;
    }

    public void ConfirmArrival(Vector3 position)
    {
        if (!_hasSelectedDestination || (position - _selectedDestination).sqrMagnitude > 0.16f) return;
        _hasSelectedDestination = false;
        if (_selectedTransfer)
        {
            _visitUpper = _selectedUpper;
            _localVisitComplete = false;
        }
        else
        {
            _localVisitComplete = true;
        }
    }

    public bool IsAtFloorHeight(Vector3 position, float tolerance)
    {
        return IsReady && (Mathf.Abs(position.y - groundHeight) <= tolerance
            || Mathf.Abs(position.y - upstairsHeight) <= tolerance);
    }

    void OnDisable()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        IsReady = false;
        if (_dog)
        {
            _dog.SetActive(false);
            Destroy(_dog);
        }
        _dog = null;
        _visualPivot = null;
        _dogAnimation = null;
        _hasSelectedDestination = false;
        ReleaseNavigation();
        _destinations.Clear();
        _sources.Clear();
        _started = false;
    }

    void ReleaseNavigation()
    {
        if (_build != null && !_build.isDone && _data) NavMeshBuilder.Cancel(_data);
        _build = null;
        if (_instance.valid) _instance.Remove();
        if (_data) Destroy(_data);
        _data = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireCube(navigationBounds.center, navigationBounds.size);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnPoint, 0.3f);
        if (roamingPoints == null) return;
        foreach (Vector3 point in roamingPoints)
        {
            Gizmos.color = point.y > 2f ? new Color(1f, 0.7f, 0.1f) : new Color(0.2f, 0.9f, 1f);
            Gizmos.DrawWireSphere(point, 0.22f);
        }
    }
}
