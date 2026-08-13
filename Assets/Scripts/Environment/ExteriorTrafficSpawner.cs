using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns AE_New_York cars on exterior road paths after the previous car has cleared.
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
    [FormerlySerializedAs("minSpawnInterval")]
    [SerializeField] float spawnDelay = 5f;
    [SerializeField] float initialDelay = 5f;
    [SerializeField] int maxActiveCars = 1;

    [Header("Movement")]
    [SerializeField] float minSpeed = 7f;
    [SerializeField] float maxSpeed = 11f;

    bool _spawnReverse;
    int _nextCarIndex;
    bool _slotWasOccupied;
    float _spawnAllowedTime;

    void Start()
    {
        _spawnAllowedTime = Time.time + initialDelay;
    }

    void Update()
    {
        if (!CanSpawn())
            return;

        if (CountActiveCars() >= maxActiveCars)
        {
            _slotWasOccupied = true;
            return;
        }

        if (_slotWasOccupied)
        {
            _slotWasOccupied = false;
            _spawnAllowedTime = Time.time + spawnDelay;
        }

        if (Time.time < _spawnAllowedTime)
            return;

        SpawnCar();
    }

    bool CanSpawn()
    {
        if (carPrefabs == null || carPrefabs.Length == 0)
            return false;

        if (paths == null || paths.Length == 0)
            return false;

        return true;
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
}
