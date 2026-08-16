using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Spawns decorative ceiling fixtures and applies even room-wide fill lighting.
/// </summary>
[ExecuteAlways]
public class StoreLighting : MonoBehaviour
{
    const string LightsRootName = "CeilingLights";
    const string RoofLightPrefabPath = "Assets/ModernSupermarket/Prefabs/Props/Utility/Rooflight_9ecem1.prefab";

    [Header("Ceiling fixtures (visual only)")]
    [SerializeField] float fixtureMountDrop = 0.18f;
    [SerializeField] bool spawnRoofLightMeshes = true;
    [SerializeField] GameObject roofLightPrefab;

    [Header("Even room fill")]
    [SerializeField] Color roomAmbientColor = new Color(1f, 0.957f, 0.847f);
    [SerializeField] float roomAmbientIntensity = 1.08f;
    [SerializeField] bool useSoftOverheadFill = true;
    [SerializeField] float overheadIntensity = 0.14f;
    [SerializeField] Color overheadColor = new Color(1f, 0.97f, 0.92f);
    [SerializeField] Vector3 overheadEuler = new Vector3(50f, -30f, 0f);

    Transform _lightsRoot;
    bool _rebuildQueued;

    void OnEnable()
    {
        EnsureRoofLightPrefab();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            QueueRebuild();
        else
            Rebuild();
#else
        Rebuild();
#endif
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled)
            return;

        QueueRebuild();
    }

    void QueueRebuild()
    {
        if (_rebuildQueued)
            return;

        _rebuildQueued = true;
#if UNITY_EDITOR
        EditorApplication.delayCall += RunQueuedRebuild;
#else
        RunQueuedRebuild();
#endif
    }

    void RunQueuedRebuild()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= RunQueuedRebuild;
#endif
        _rebuildQueued = false;

        if (this == null || !isActiveAndEnabled)
            return;

        Rebuild();
    }

    public void Rebuild()
    {
        EnsureRoofLightPrefab();
        ApplyEvenRoomLighting();
        BuildCeilingFixtures();
    }

    void EnsureRoofLightPrefab()
    {
        if (roofLightPrefab != null || !spawnRoofLightMeshes)
            return;

#if UNITY_EDITOR
        roofLightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoofLightPrefabPath);
#endif
    }

    void ApplyEvenRoomLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = roomAmbientColor;
        RenderSettings.ambientIntensity = roomAmbientIntensity;

        Light directional = FindDirectionalLight();
        if (directional == null)
            return;

        if (!useSoftOverheadFill)
        {
            directional.enabled = false;
            return;
        }

        directional.enabled = true;
        directional.intensity = overheadIntensity;
        directional.color = overheadColor;
        directional.shadows = LightShadows.None;
        directional.transform.rotation = Quaternion.Euler(overheadEuler);
    }

    static Light FindDirectionalLight()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light != null && light.type == LightType.Directional)
                return light;
        }

        return null;
    }

    void BuildCeilingFixtures()
    {
        if (!GameScenes.IsActiveGameScene())
            return;

        EnsureLightsRoot();
        ClearLightsRoot();

        if (!spawnRoofLightMeshes || roofLightPrefab == null)
            return;

        List<Transform> ceilingTiles = CollectCeilingTiles();
        if (ceilingTiles.Count == 0)
            return;

        for (int i = 0; i < ceilingTiles.Count; i++)
        {
            Transform ceiling = ceilingTiles[i];
            if (ceiling == null)
                continue;

            Vector3 fixturePos = ceiling.position + Vector3.down * fixtureMountDrop;
            GameObject fixture = InstantiateFixture(roofLightPrefab, fixturePos, ceiling.rotation, _lightsRoot);
            fixture.name = "CeilingLight_" + i.ToString("00");
        }
    }

    static List<Transform> CollectCeilingTiles()
    {
        var results = new List<Transform>(32);
        var seen = new HashSet<int>();

        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            Transform[] transforms = roots[r].GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null || !IsCeilingTileName(t.name))
                    continue;

                int id = t.GetInstanceID();
                if (!seen.Add(id))
                    continue;

                results.Add(t);
            }
        }

        return results;
    }

    static bool IsCeilingTileName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (name == "Ceiling")
            return true;

        return name.StartsWith("Ceiling (", System.StringComparison.Ordinal);
    }

    void EnsureLightsRoot()
    {
        Transform existing = transform.Find(LightsRootName);
        if (existing != null)
        {
            _lightsRoot = existing;
            return;
        }

        var root = new GameObject(LightsRootName);
        root.transform.SetParent(transform, false);
        _lightsRoot = root.transform;
    }

    void ClearLightsRoot()
    {
        if (_lightsRoot == null)
            return;

        for (int i = _lightsRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = _lightsRoot.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    static GameObject InstantiateFixture(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }
#endif
        return Instantiate(prefab, position, rotation, parent);
    }

    public static StoreLighting EnsureExists()
    {
        StoreLighting existing = FindFirstObjectByType<StoreLighting>();
        if (existing != null)
            return existing;

        var root = new GameObject("StoreLighting");
        return root.AddComponent<StoreLighting>();
    }
}
