using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Stable identity that survives play mode and sessions.
/// Scene-authored objects keep a serialized GUID. Runtime-spawned objects
/// receive a new GUID once and persist it through the save file.
/// </summary>
[DisallowMultipleComponent]
public sealed class PersistentId : MonoBehaviour
{
    [SerializeField] string id;

    public string Value => id;
    public bool HasValue => !string.IsNullOrEmpty(id);

    void Awake()
    {
        EnsureAssigned();
        PersistentIdRegistry.Register(this);
    }

    void OnDestroy()
    {
        PersistentIdRegistry.Unregister(this);
    }

    public void AssignNew()
    {
        PersistentIdRegistry.Unregister(this);
        id = Guid.NewGuid().ToString("N");
        PersistentIdRegistry.Register(this);
    }

    public void AssignExisting(string existing)
    {
        if (string.IsNullOrEmpty(existing) || id == existing)
            return;

        PersistentIdRegistry.Unregister(this);
        id = existing;
        PersistentIdRegistry.Register(this);
    }

    public void EnsureAssigned()
    {
        if (HasValue)
            return;

        if (IsSceneAuthored())
        {
            id = BuildPathFallback(transform);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(this);
#endif
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            AssignNew();
            EditorUtility.SetDirty(this);
            return;
        }
#endif
        AssignNew();
    }

    bool IsSceneAuthored()
    {
        return GetComponent<CardShelf>() != null || GetComponent<PsaCabinet>() != null;
    }

    public static PersistentId GetOrCreate(GameObject target)
    {
        if (target == null)
            return null;

        PersistentId persistent = target.GetComponent<PersistentId>();
        if (persistent == null)
            persistent = target.AddComponent<PersistentId>();

        persistent.EnsureAssigned();
        return persistent;
    }

    public static string Resolve(Component component)
    {
        return component == null ? string.Empty : Resolve(component.gameObject);
    }

    public static string Resolve(GameObject target)
    {
        if (target == null)
            return string.Empty;

        PersistentId persistent = target.GetComponent<PersistentId>();
        if (persistent != null)
        {
            persistent.EnsureAssigned();
            return persistent.Value;
        }

        return BuildPathFallback(target.transform);
    }

    public static string BuildPathFallback(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        string sceneName = transform.gameObject.scene.IsValid()
            ? transform.gameObject.scene.name
            : "runtime";

        System.Text.StringBuilder builder = new System.Text.StringBuilder(96);
        builder.Append("path:").Append(sceneName);
        AppendHierarchyPath(transform, builder);
        return builder.ToString();
    }

    static void AppendHierarchyPath(Transform transform, System.Text.StringBuilder builder)
    {
        if (transform.parent != null)
            AppendHierarchyPath(transform.parent, builder);

        builder.Append('/').Append(transform.name);
    }

#if UNITY_EDITOR
    void Reset()
    {
        EnsureAssigned();
    }

    void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
            EnsureAssigned();
    }
#endif
}
