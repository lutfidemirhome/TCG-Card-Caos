using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene / Hierarchy picks often hit CardVisual, PackVisual, or PSA mesh children.
/// Keep the WorldCard / WorldBoosterPack root selected so transforms move the item.
/// </summary>
[InitializeOnLoad]
static class WorldItemSelectionRedirect
{
    static bool _redirecting;

    static WorldItemSelectionRedirect()
    {
        Selection.selectionChanged += OnSelectionChanged;
    }

    static void OnSelectionChanged()
    {
        if (_redirecting)
            return;

        Object[] selected = Selection.objects;
        if (selected == null || selected.Length == 0)
            return;

        var redirected = new List<Object>(selected.Length);
        bool changed = false;

        for (int i = 0; i < selected.Length; i++)
        {
            Object current = selected[i];
            GameObject root = ResolveRoot(current);
            if (root == null)
            {
                redirected.Add(current);
                continue;
            }

            if (!ReferenceEquals(current, root) && !IsSameGameObject(current, root))
                changed = true;

            if (!redirected.Contains(root))
                redirected.Add(root);
        }

        if (!changed)
            return;

        _redirecting = true;
        try
        {
            Selection.objects = redirected.ToArray();
        }
        finally
        {
            _redirecting = false;
        }
    }

    static bool IsSameGameObject(Object current, GameObject root)
    {
        if (current is GameObject go)
            return go == root;

        if (current is Component component)
            return component.gameObject == root;

        return false;
    }

    static GameObject ResolveRoot(Object selected)
    {
        Transform transform = selected as Transform;
        if (transform == null && selected is Component component)
            transform = component.transform;
        if (transform == null && selected is GameObject gameObject)
            transform = gameObject.transform;
        if (transform == null)
            return null;

        WorldCard card = transform.GetComponentInParent<WorldCard>();
        if (card != null)
            return card.gameObject;

        WorldBoosterPack pack = transform.GetComponentInParent<WorldBoosterPack>();
        if (pack != null)
            return pack.gameObject;

        return null;
    }
}
