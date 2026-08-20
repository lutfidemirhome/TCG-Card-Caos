using UnityEngine;

/// <summary>
/// Measures the 3D PSA holder mesh in cabinet-slot orientation (upright, face +Z).
/// Used so slot previews and placed cards share the same footprint.
/// </summary>
public static class PsaSlabLayoutUtility
{
    struct FootprintCache
    {
        public bool Ready;
        public Vector3 Min;
        public Vector3 Max;
        public Vector3 Size;
        public float FaceWidth;
        public float FaceHeight;
        public float Thickness;
    }

    static FootprintCache _cache;

    public static float DefaultCabinetRootWorldScale => CardDimensions.GroundCardScale;

    public static bool TryGetCabinetRootBounds(out Vector3 min, out Vector3 max, out Vector3 size)
    {
        EnsureCache();
        if (!_cache.Ready)
        {
            min = max = size = Vector3.zero;
            return false;
        }

        min = _cache.Min;
        max = _cache.Max;
        size = _cache.Size;
        return true;
    }

    public static float GetCabinetFaceWidth()
    {
        EnsureCache();
        return _cache.Ready ? _cache.FaceWidth : CardDimensions.Width;
    }

    public static float GetCabinetFaceHeight()
    {
        EnsureCache();
        return _cache.Ready ? _cache.FaceHeight : CardDimensions.Height;
    }

    public static float GetCabinetThickness()
    {
        EnsureCache();
        return _cache.Ready ? _cache.Thickness : CardDimensions.Thickness * 2f;
    }

    /// <summary>Bottom-center of the front face in root-local space (cabinet orientation).</summary>
    public static Vector3 GetCabinetFaceBottomCenterLocal()
    {
        EnsureCache();
        if (!_cache.Ready)
            return Vector3.zero;

        return new Vector3(
            (_cache.Min.x + _cache.Max.x) * 0.5f,
            _cache.Min.y,
            _cache.Max.z);
    }

    public static void InvalidateCache() => _cache.Ready = false;

    static void EnsureCache()
    {
        if (_cache.Ready)
            return;

        if (!TryProbeCabinetFootprint(out FootprintCache footprint))
        {
            footprint.Ready = true;
            footprint.FaceWidth = CardDimensions.Width;
            footprint.FaceHeight = CardDimensions.Height;
            footprint.Thickness = CardDimensions.Thickness * 2f;
            footprint.Min = Vector3.zero;
            footprint.Max = new Vector3(footprint.FaceWidth, footprint.FaceHeight, footprint.Thickness);
            footprint.Size = footprint.Max - footprint.Min;
        }

        _cache = footprint;
    }

    static bool TryProbeCabinetFootprint(out FootprintCache footprint)
    {
        footprint = default;

        GameObject prefab = PsaArtLibrary.LoadModelPrefab();
        if (prefab == null)
            return false;

        var probeRoot = new GameObject("PsaCabinetFootprintProbe");
        probeRoot.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            var cardRefGo = new GameObject("PsaCardRef");
            cardRefGo.hideFlags = HideFlags.HideAndDontSave;
            cardRefGo.transform.SetParent(probeRoot.transform, false);
            cardRefGo.transform.localRotation = PsaCardVisualController.GetCabinetSlotCardRefLocalRotation();

            GameObject modelInstance = Object.Instantiate(prefab, cardRefGo.transform, false);
            modelInstance.hideFlags = HideFlags.HideAndDontSave;
            modelInstance.transform.localRotation = Quaternion.Euler(PsaVisualSettings.GetModelRootRotationEulerOrDefault());
            modelInstance.transform.localScale = PsaVisualSettings.GetModelRootScaleOrDefault();
            modelInstance.transform.localPosition = PsaVisualSettings.GetModelRootPositionOrDefault();

            if (!TryMeasureMeshBoundsInLocalSpace(
                    probeRoot.transform,
                    modelInstance.transform,
                    out Vector3 min,
                    out Vector3 max))
            {
                return false;
            }

            Vector3 size = max - min;
            GetFootprintAxes(size, out int thicknessAxis, out int widthAxis, out int heightAxis);

            footprint.Ready = true;
            footprint.Min = min;
            footprint.Max = max;
            footprint.Size = size;
            footprint.Thickness = size[thicknessAxis];
            footprint.FaceWidth = size[widthAxis];
            footprint.FaceHeight = size[heightAxis];
            return true;
        }
        finally
        {
            if (Application.isPlaying)
                Object.Destroy(probeRoot);
            else
                Object.DestroyImmediate(probeRoot);
        }
    }

    static void GetFootprintAxes(Vector3 size, out int thicknessAxis, out int widthAxis, out int heightAxis)
    {
        thicknessAxis = SmallestAxis(size);
        int faceA = (thicknessAxis + 1) % 3;
        int faceB = (thicknessAxis + 2) % 3;
        if (size[faceA] <= size[faceB])
        {
            widthAxis = faceA;
            heightAxis = faceB;
        }
        else
        {
            widthAxis = faceB;
            heightAxis = faceA;
        }
    }

    static int SmallestAxis(Vector3 size)
    {
        int axis = 0;
        if (size.y < size[axis])
            axis = 1;
        if (size.z < size[axis])
            axis = 2;
        return axis;
    }

    static bool TryMeasureMeshBoundsInLocalSpace(
        Transform localSpace,
        Transform meshRoot,
        out Vector3 min,
        out Vector3 max)
    {
        min = Vector3.positiveInfinity;
        max = Vector3.negativeInfinity;
        if (localSpace == null || meshRoot == null)
            return false;

        MeshFilter[] meshFilters = meshRoot.GetComponentsInChildren<MeshFilter>(true);
        if (meshFilters.Length == 0)
            return false;

        bool hasBounds = false;
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            EncapsulateMeshBounds(localSpace, meshFilter.transform, meshFilter.sharedMesh.bounds, ref min, ref max);
            hasBounds = true;
        }

        return hasBounds;
    }

    static void EncapsulateMeshBounds(
        Transform localSpace,
        Transform meshTransform,
        Bounds meshBounds,
        ref Vector3 min,
        ref Vector3 max)
    {
        Vector3 center = meshBounds.center;
        Vector3 extents = meshBounds.extents;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 localCorner = localSpace.InverseTransformPoint(meshTransform.TransformPoint(corner));
                    min = Vector3.Min(min, localCorner);
                    max = Vector3.Max(max, localCorner);
                }
            }
        }
    }
}
