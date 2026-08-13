using UnityEngine;

/// <summary>
/// Spawns AE_New_York cars on exterior road paths at random intervals.
/// </summary>
[DisallowMultipleComponent]
public class ExteriorTrafficSpawner : MonoBehaviour
{
    [Header("Cars")]
    [SerializeField] GameObject[] carPrefabs;

    [Header("Routes")]
    [SerializeField] ExteriorTrafficPath[] paths;
    [SerializeField] bool alternateDirection = true;

    [Header("Timing")]
    [SerializeField] float minSpawnInterval = 5f;
    [SerializeField] float maxSpawnInterval = 11f;
    [SerializeField] float initialDelay = 2f;
    [SerializeField] int maxActiveCars = 1;

    [Header("Movement")]
    [SerializeField] float minSpeed = 7f;
    [SerializeField] float maxSpeed = 11f;

    float _nextSpawnTime;
    bool _spawnReverse;
    int _nextCarIndex;

    void Start()
    {
        ScheduleNextSpawn(initialDelay);
    }

    void Update()
    {
        if (!CanSpawn())
            return;

        if (Time.time < _nextSpawnTime)
            return;

        SpawnCar();
        ScheduleNextSpawn(Random.Range(minSpawnInterval, maxSpawnInterval));
    }

    bool CanSpawn()
    {
        if (carPrefabs == null || carPrefabs.Length == 0)
            return false;

        if (paths == null || paths.Length == 0)
            return false;

        return CountActiveCars() < maxActiveCars;
    }

    int CountActiveCars()
    {
        return FindObjectsByType<ExteriorTrafficCar>(FindObjectsSortMode.None).Length;
    }

    void SpawnCar()
    {
        ExteriorTrafficPath path = ResolvePath();
        if (path == null || path.PointCount < 2)
            return;

        GameObject prefab = GetNextCarPrefab();
        if (prefab == null)
            return;

        bool reverse = alternateDirection && _spawnReverse;
        float startDistance = reverse ? path.TotalLength : 0f;
        Vector3 spawnPosition = path.GetPositionAtDistance(startDistance);
        Vector3 direction = path.GetDirectionAtDistance(startDistance);
        if (reverse)
            direction = -direction;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        Quaternion rotation = ExteriorTrafficCar.GetDrivingRotation(direction);
        GameObject carObject = Instantiate(prefab, spawnPosition, rotation, transform);

        ExteriorTrafficCar driver = carObject.GetComponent<ExteriorTrafficCar>();
        if (driver == null)
            driver = carObject.AddComponent<ExteriorTrafficCar>();

        driver.Initialize(path, Random.Range(minSpeed, maxSpeed), reverse);

        if (alternateDirection)
            _spawnReverse = !_spawnReverse;
    }

    GameObject GetNextCarPrefab()
    {
        if (carPrefabs == null || carPrefabs.Length == 0)
            return null;

        for (int attempt = 0; attempt < carPrefabs.Length; attempt++)
        {
            GameObject prefab = carPrefabs[_nextCarIndex];
            _nextCarIndex = (_nextCarIndex + 1) % carPrefabs.Length;
            if (prefab != null)
                return prefab;
        }

        return null;
    }

    ExteriorTrafficPath ResolvePath()
    {
        if (paths.Length == 1 || alternateDirection)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i] != null)
                    return paths[i];
            }

            return null;
        }

        return paths[Random.Range(0, paths.Length)];
    }

    void ScheduleNextSpawn(float delay)
    {
        _nextSpawnTime = Time.time + Mathf.Max(0.5f, delay);
    }
}
