using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Waypoint path for ambient exterior cars. Direct child transforms define the route top-to-bottom in Hierarchy.
/// </summary>
[DisallowMultipleComponent]
public class ExteriorTrafficPath : MonoBehaviour
{
    struct PathSample
    {
        public Vector3 Position;
        public Vector3 Tangent;
        public float Distance;
    }

    [SerializeField] Transform[] waypoints;
    [SerializeField] bool useChildWaypoints = true;
    [SerializeField] bool smoothCorners = true;
    [SerializeField] int samplesPerSegment = 16;
    [SerializeField] Color gizmoColor = new Color(0.2f, 0.85f, 1f, 0.9f);

    PathSample[] _samples;
    int _sampleCount;
    float _cachedLength = -1f;
    bool _samplesDirty = true;

    public int PointCount => waypoints != null ? waypoints.Length : 0;
    public bool UsesChildWaypoints => useChildWaypoints;

    public float TotalLength
    {
        get
        {
            EnsureSamples();
            return _cachedLength;
        }
    }

    void Awake()
    {
        RefreshWaypointsIfNeeded();
        EnsureSamples();
    }

    void OnValidate()
    {
        RefreshWaypointsIfNeeded();
        InvalidateSamples();
        samplesPerSegment = Mathf.Clamp(samplesPerSegment, 4, 48);
    }

    public void RefreshWaypointsIfNeeded()
    {
        if (!useChildWaypoints || transform.childCount == 0)
            return;

        waypoints = CollectDirectChildren();
        InvalidateSamples();
    }

    public void SyncFromChildren()
    {
        useChildWaypoints = true;
        waypoints = CollectDirectChildren();
        InvalidateSamples();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public Transform AddWaypointAtEnd()
    {
        Vector3 position = transform.position;
        if (waypoints != null && waypoints.Length > 0)
        {
            Transform last = waypoints[waypoints.Length - 1];
            if (last != null)
                position = last.position;
        }

        GameObject pointObject = new GameObject(BuildWaypointName(waypoints != null ? waypoints.Length : 0));
        pointObject.transform.SetParent(transform, false);
        pointObject.transform.position = position + transform.forward * 5f;

        SyncFromChildren();

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(pointObject, "Add Traffic Waypoint");
        Selection.activeTransform = pointObject.transform;
#endif

        return pointObject.transform;
    }

    Transform[] CollectDirectChildren()
    {
        int count = transform.childCount;
        Transform[] children = new Transform[count];
        for (int i = 0; i < count; i++)
            children[i] = transform.GetChild(i);

        return children;
    }

    static string BuildWaypointName(int index)
    {
        if (index <= 0)
            return "Start";

        if (index == 1)
            return "End";

        return $"Point_{index:00}";
    }

    public Vector3 GetPoint(int index)
    {
        if (waypoints == null || waypoints.Length == 0)
            return transform.position;

        index = Mathf.Clamp(index, 0, waypoints.Length - 1);
        Transform point = waypoints[index];
        return point != null ? point.position : transform.position;
    }

    public Vector3 GetPositionAtDistance(float distance)
    {
        SampleAtDistance(distance, out Vector3 position, out _);
        return position;
    }

    public Vector3 GetDirectionAtDistance(float distance)
    {
        SampleAtDistance(distance, out _, out Vector3 tangent);
        return tangent.sqrMagnitude > 0.0001f ? tangent : transform.forward;
    }

    void InvalidateSamples()
    {
        _samplesDirty = true;
        _cachedLength = -1f;
    }

    void EnsureSamples()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            BuildSamples();
            return;
        }
#endif
        if (_samplesDirty)
            BuildSamples();
    }

    void BuildSamples()
    {
        _samplesDirty = false;
        _sampleCount = 0;
        _cachedLength = 0f;

        if (waypoints == null || waypoints.Length < 2)
            return;

        if (!smoothCorners)
        {
            BuildPolylineSamples();
            return;
        }

        int segmentCount = waypoints.Length - 1;
        int perSegment = samplesPerSegment;
        int capacity = segmentCount * perSegment + 1;
        if (_samples == null || _samples.Length < capacity)
            _samples = new PathSample[capacity];

        float distance = 0f;
        for (int segment = 0; segment < segmentCount; segment++)
        {
            Vector3 p0 = GetControlPoint(segment - 1);
            Vector3 p1 = GetControlPoint(segment);
            Vector3 p2 = GetControlPoint(segment + 1);
            Vector3 p3 = GetControlPoint(segment + 2);

            int stepStart = segment == 0 ? 0 : 1;
            int stepEnd = segment == segmentCount - 1 ? perSegment : perSegment - 1;

            for (int step = stepStart; step <= stepEnd; step++)
            {
                float t = step / (float)perSegment;
                Vector3 position = EvaluateCatmullRom(p0, p1, p2, p3, t);
                Vector3 tangent = EvaluateCatmullRomDerivative(p0, p1, p2, p3, t);
                if (tangent.sqrMagnitude > 0.0001f)
                    tangent.Normalize();
                else if (_sampleCount > 0)
                    tangent = _samples[_sampleCount - 1].Tangent;
                else
                    tangent = (p2 - p1).normalized;

                if (_sampleCount > 0)
                    distance += Vector3.Distance(_samples[_sampleCount - 1].Position, position);

                _samples[_sampleCount++] = new PathSample
                {
                    Position = position,
                    Tangent = tangent,
                    Distance = distance
                };
            }
        }

        _cachedLength = distance;
    }

    void BuildPolylineSamples()
    {
        int segmentCount = waypoints.Length - 1;
        int capacity = segmentCount + 1;
        if (_samples == null || _samples.Length < capacity)
            _samples = new PathSample[capacity];

        float distance = 0f;
        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector3 position = GetPoint(i);
            Vector3 tangent = i < waypoints.Length - 1
                ? (GetPoint(i + 1) - position).normalized
                : _sampleCount > 0 ? _samples[_sampleCount - 1].Tangent : transform.forward;

            if (_sampleCount > 0)
                distance += Vector3.Distance(_samples[_sampleCount - 1].Position, position);

            _samples[_sampleCount++] = new PathSample
            {
                Position = position,
                Tangent = tangent,
                Distance = distance
            };
        }

        _cachedLength = distance;
    }

    void SampleAtDistance(float distance, out Vector3 position, out Vector3 tangent)
    {
        EnsureSamples();

        if (_sampleCount == 0)
        {
            position = transform.position;
            tangent = transform.forward;
            return;
        }

        if (_sampleCount == 1)
        {
            position = _samples[0].Position;
            tangent = _samples[0].Tangent;
            return;
        }

        distance = Mathf.Clamp(distance, 0f, _cachedLength);

        int low = 0;
        int high = _sampleCount - 1;
        while (low < high - 1)
        {
            int mid = (low + high) >> 1;
            if (_samples[mid].Distance <= distance)
                low = mid;
            else
                high = mid;
        }

        PathSample a = _samples[low];
        PathSample b = _samples[high];
        float span = b.Distance - a.Distance;
        float t = span <= 0.0001f ? 0f : (distance - a.Distance) / span;
        position = Vector3.Lerp(a.Position, b.Position, t);
        tangent = Vector3.Slerp(a.Tangent, b.Tangent, t);
        if (tangent.sqrMagnitude > 0.0001f)
            tangent.Normalize();
    }

    Vector3 GetControlPoint(int index)
    {
        if (waypoints == null || waypoints.Length == 0)
            return transform.position;

        if (index <= 0)
            return GetPoint(0);

        if (index >= waypoints.Length)
            return GetPoint(waypoints.Length - 1);

        return GetPoint(index);
    }

    static Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    static Vector3 EvaluateCatmullRomDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        return 0.5f * (
            (-p0 + p2) +
            2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
            3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
    }

    void OnDrawGizmos()
    {
        RefreshWaypointsIfNeeded();
        EnsureSamples();
        DrawPathGizmos(0.35f);
    }

    void OnDrawGizmosSelected()
    {
        RefreshWaypointsIfNeeded();
        EnsureSamples();
        DrawPathGizmos(1f);
    }

    void DrawPathGizmos(float alpha)
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        Color lineColor = gizmoColor;
        lineColor.a = alpha;
        Gizmos.color = lineColor;

        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform point = waypoints[i];
            if (point == null)
                continue;

            Gizmos.DrawSphere(point.position, i == 0 || i == waypoints.Length - 1 ? 0.45f : 0.3f);

#if UNITY_EDITOR
            Handles.color = lineColor;
            Handles.Label(point.position + Vector3.up * 0.6f, point.name);
#endif
        }

        if (_sampleCount >= 2)
        {
            for (int i = 1; i < _sampleCount; i++)
                Gizmos.DrawLine(_samples[i - 1].Position, _samples[i].Position);

            for (int i = 1; i < _sampleCount; i += Mathf.Max(1, samplesPerSegment / 2))
            {
                Vector3 direction = _samples[i].Tangent;
                if (direction.sqrMagnitude <= 0.0001f)
                    continue;

                Vector3 end = _samples[i].Position;
                Vector3 arrow = end - direction * 1.2f;
                Gizmos.DrawLine(end, arrow + Quaternion.Euler(0f, 25f, 0f) * -direction * 0.7f);
                Gizmos.DrawLine(end, arrow + Quaternion.Euler(0f, -25f, 0f) * -direction * 0.7f);
            }

            return;
        }

        for (int i = 0; i < waypoints.Length - 1; i++)
            Gizmos.DrawLine(GetPoint(i), GetPoint(i + 1));
    }
}
