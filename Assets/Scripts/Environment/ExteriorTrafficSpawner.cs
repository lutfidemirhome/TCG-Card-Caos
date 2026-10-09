using System.Collections;
using System.Collections.Generic;
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

    readonly Dictionary<GameObject, Queue<ExteriorTrafficCar>> _pool = new();
    bool _prepared, _started;

    public static IEnumerator PrewarmAll()
    {
        foreach (var spawner in FindObjectsByType<ExteriorTrafficSpawner>(FindObjectsSortMode.None))
            yield return spawner.Prewarm();
    }

    IEnumerator Prewarm()
    {
        if (_prepared) yield break;
        if (carPrefabs != null)
            foreach (var prefab in carPrefabs)
            {
                if (!prefab || _pool.ContainsKey(prefab)) continue;
                var available = new Queue<ExteriorTrafficCar>();
                _pool.Add(prefab, available);
                for (int i = 0; i < Mathf.Max(1, maxActiveCars); i++)
                {
                    available.Enqueue(CreatePooledCar(prefab, available));
                    yield return null;
                }
            }
        _prepared = true;
    }

    ExteriorTrafficCar CreatePooledCar(GameObject prefab, Queue<ExteriorTrafficCar> available)
    {
        // An inactive parent prevents prefab physics/OnEnable work until preparation finishes.
        var staging = new GameObject("Traffic preparation");
        staging.SetActive(false);
        staging.transform.SetParent(transform, false);
        GameObject obj = Instantiate(prefab, staging.transform);
        obj.SetActive(false);
        var driver = obj.GetComponent<ExteriorTrafficCar>();
        if (!driver) driver = obj.AddComponent<ExteriorTrafficCar>();
        driver.PrepareForPool(car =>
        {
            car.gameObject.SetActive(false);
            available.Enqueue(car);
        });
        obj.transform.SetParent(transform, false);
        Destroy(staging);
        return driver;
    }

    void Update()
    {
        if (!_prepared || !CardInstancedRenderManager.IsGameplayReady || GamePause.IsPaused) return;
        if (!_started)
        {
            _started = true;
            _spawnAllowedTime = Time.time + initialDelay;
        }
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
        return ExteriorTrafficCar.ActiveCount;
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
        if (!_pool.TryGetValue(prefab, out var available) || available.Count == 0)
            return;
        ExteriorTrafficCar driver = available.Dequeue();
        driver.transform.SetPositionAndRotation(spawnPosition, rotation);
        driver.Initialize(path, Random.Range(minSpeed, maxSpeed), reverse);
        driver.gameObject.SetActive(true);

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
