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
    [SerializeField] Transform[] waypoints;
    [SerializeField] bool useChildWaypoints = true;
    [SerializeField] Color gizmoColor = new Color(0.2f, 0.85f, 1f, 0.9f);

    float _cachedLength = -1f;

    public int PointCount => waypoints != null ? waypoints.Length : 0;
    public bool UsesChildWaypoints => useChildWaypoints;

    public float TotalLength
    {
        get
        {
            if (_cachedLength < 0f)
                _cachedLength = ComputeLength();
            return _cachedLength;
        }
    }

    void Awake()
    {
        RefreshWaypointsIfNeeded();
    }

    void OnValidate()
    {
        RefreshWaypointsIfNeeded();
        _cachedLength = -1f;
    }

    public void RefreshWaypointsIfNeeded()
    {
        if (!useChildWaypoints || transform.childCount == 0)
            return;

        waypoints = CollectDirectChildren();
        _cachedLength = -1f;
    }

    public void SyncFromChildren()
    {
        useChildWaypoints = true;
        waypoints = CollectDirectChildren();
        _cachedLength = -1f;

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
        if (waypoints == null || waypoints.Length == 0)
            return transform.position;

        if (waypoints.Length == 1)
            return GetPoint(0);

        distance = Mathf.Clamp(distance, 0f, TotalLength);
        float remaining = distance;

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            Vector3 start = GetPoint(i);
            Vector3 end = GetPoint(i + 1);
            float segmentLength = Vector3.Distance(start, end);
            if (segmentLength <= 0.0001f)
                continue;

            if (remaining <= segmentLength)
                return Vector3.Lerp(start, end, remaining / segmentLength);

            remaining -= segmentLength;
        }

        return GetPoint(waypoints.Length - 1);
    }

    public Vector3 GetDirectionAtDistance(float distance)
    {
        if (waypoints == null || waypoints.Length < 2)
            return transform.forward;

        distance = Mathf.Clamp(distance, 0f, TotalLength);
        float remaining = distance;

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            Vector3 start = GetPoint(i);
            Vector3 end = GetPoint(i + 1);
            Vector3 segment = end - start;
            float segmentLength = segment.magnitude;
            if (segmentLength <= 0.0001f)
                continue;

            if (remaining <= segmentLength)
                return segment / segmentLength;

            remaining -= segmentLength;
        }

        Vector3 lastSegment = GetPoint(waypoints.Length - 1) - GetPoint(waypoints.Length - 2);
        return lastSegment.sqrMagnitude > 0.0001f ? lastSegment.normalized : transform.forward;
    }

    float ComputeLength()
    {
        if (waypoints == null || waypoints.Length < 2)
            return 0f;

        float length = 0f;
        for (int i = 0; i < waypoints.Length - 1; i++)
            length += Vector3.Distance(GetPoint(i), GetPoint(i + 1));

        return length;
    }

    void OnDrawGizmos()
    {
        RefreshWaypointsIfNeeded();
        DrawPathGizmos(0.35f);
    }

    void OnDrawGizmosSelected()
    {
        RefreshWaypointsIfNeeded();
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

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            Vector3 start = GetPoint(i);
            Vector3 end = GetPoint(i + 1);
            Gizmos.DrawLine(start, end);

            Vector3 direction = end - start;
            if (direction.sqrMagnitude <= 0.0001f)
                continue;

            direction.Normalize();
            Vector3 arrow = end - direction * 1.5f;
            Gizmos.DrawLine(end, arrow + Quaternion.Euler(0f, 25f, 0f) * -direction * 0.8f);
            Gizmos.DrawLine(end, arrow + Quaternion.Euler(0f, -25f, 0f) * -direction * 0.8f);
        }
    }
}
