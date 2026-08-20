using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 3D PSA slab visual for a <see cref="WorldCard"/>. Invisible card-proxy child mirrors normal card
/// orientation; the imported holder mesh is cosmetic. Ground collider matches the mesh body; hand
/// profile thins the slab like a held pack so it does not clip into neighbors.
/// </summary>
public sealed class PsaCardVisualController
{
    const string CardRefChildName = "PsaCardRef";
    const string PsaModelChildName = "PsaVisual";
    const float FootprintWidthFitMultiplier = 1.06f;

    readonly WorldCard _owner;
    int _slotNumber = PsaArtLibrary.MinCabinetSlotNumber;
    int _variantIndex = 1;
    Transform _cardRef;
    Transform _psaModel;
    Outline _modelOutline;
    float _bodyThickness;
    float _groundOffsetYFaceUp;
    float _groundOffsetYFaceDown;
    Vector3 _visualBaseScale = Vector3.one;
    int _nativeThicknessAxis;

    public PsaCardVisualController(WorldCard owner)
    {
        _owner = owner;
    }

    public Transform CardRef => _cardRef;

    public Transform PsaModel => _psaModel;

    public float GroundRestLift
    {
        get
        {
            if (_cardRef == null)
                return 0f;

            float halfBody = Mathf.Max(CardDimensions.Thickness, _bodyThickness) * 0.5f;
            float halfCard = CardDimensions.Thickness * 0.5f;
            return (halfBody - halfCard - _cardRef.localPosition.y) * CardDimensions.GroundCardScale;
        }
    }

    public void Build(int slotNumber, int variantIndex)
    {
        _slotNumber = PsaArtLibrary.ClampCabinetSlotNumber(slotNumber);
        _variantIndex = Mathf.Max(1, variantIndex);
        EnsureVisual();
        RefreshLayout();
        ApplyWorldOrientation(alignModelToGround: true);
        ApplyBodyCollider();
    }

    public void EnsureVisual()
    {
        EnsureCardRef();
        EnsurePsaModel();
    }

    public void ReleaseVisual()
    {
        if (_psaModel != null)
        {
            if (Application.isPlaying)
                Object.Destroy(_psaModel.gameObject);
            else
                Object.DestroyImmediate(_psaModel.gameObject);
        }

        if (_cardRef != null)
        {
            if (Application.isPlaying)
                Object.Destroy(_cardRef.gameObject);
            else
                Object.DestroyImmediate(_cardRef.gameObject);
        }

        _psaModel = null;
        _cardRef = null;
    }

    public void ApplyHandOrientation()
    {
        EnsureVisual();
        if (_cardRef == null)
            return;

        _cardRef.localPosition = Vector3.zero;
        _cardRef.localRotation = CardArtLibrary.HandVisualRotation;
        _cardRef.localScale = Vector3.one;
        ApplyModelLocalTransform();
    }

    public void AlignRootRotationForHandPickup()
    {
        EnsureVisual();
        if (_cardRef == null)
            return;

        _owner.RootTransform.rotation = _cardRef.rotation * Quaternion.Inverse(CardArtLibrary.HandVisualRotation);
        ApplyHandOrientation();
    }

    public void RefreshOutlineState(bool interactionHighlighted, bool handSelected)
    {
        EnsureOutline();
        if (_modelOutline == null)
            return;

        CardOutlineSettings.Palette palette = CardOutlineSettings.GetPaletteOrDefaults();
        bool inHand = _owner.IsInHand;

        if (!inHand && interactionHighlighted)
        {
            _modelOutline.OutlineColor = palette.cardHover;
            _modelOutline.enabled = true;
            return;
        }

        if (inHand && handSelected)
        {
            _modelOutline.OutlineColor = palette.handSelection;
            _modelOutline.enabled = true;
            return;
        }

        _modelOutline.enabled = false;
    }

    public void DisableOutline()
    {
        if (_modelOutline != null)
            _modelOutline.enabled = false;
    }

    public void ApplyWorldOrientation(bool alignModelToGround)
    {
        EnsureVisual();
        if (_cardRef == null)
            return;

        bool applyGroundPose = alignModelToGround && !_owner.HasActivePhysics;
        _cardRef.localRotation = applyGroundPose
            ? GetWorldGroundLocalRotation(_owner.IsGroundFaceDown)
            : CardArtLibrary.WorldVisualRotation;
        _cardRef.localScale = Vector3.one;
        _cardRef.localPosition = applyGroundPose
            ? Vector3.up * GetModelGroundOffsetY()
            : Vector3.zero;
        ApplyModelLocalTransform();
    }

    /// <summary>
    /// Upright PSA slab in a cabinet slot marker (+Z = face toward player).
    /// </summary>
    public static Quaternion GetCabinetSlotCardRefLocalRotation() => Quaternion.identity;

    public void ApplyCabinetSlotOrientation()
    {
        EnsureVisual();
        if (_cardRef == null)
            return;

        _cardRef.localPosition = Vector3.zero;
        _cardRef.localRotation = GetCabinetSlotCardRefLocalRotation();
        _cardRef.localScale = Vector3.one;
        ApplyModelLocalTransform();
    }

    public void ApplyBodyCollider()
    {
        if (_cardRef == null || !(_owner.PhysCollider is BoxCollider boxCollider))
            return;

        boxCollider.center = new Vector3(0f, _cardRef.localPosition.y, 0f);
        boxCollider.size = new Vector3(
            CardDimensions.Width * FootprintWidthFitMultiplier,
            Mathf.Max(CardDimensions.Thickness, _bodyThickness),
            CardDimensions.Height);
        CardCollisionUtility.ApplyToCollider(boxCollider);
    }

    public void ConvertHandVisualToWorldRoot()
    {
        EnsureVisual();
        if (_cardRef == null)
            return;

        _owner.RootTransform.rotation = _cardRef.rotation * Quaternion.Inverse(CardArtLibrary.WorldVisualRotation);
        ApplyWorldOrientation(alignModelToGround: false);
    }

    void EnsureCardRef()
    {
        if (_cardRef != null)
            return;

        Transform existing = _owner.RootTransform.Find(CardRefChildName);
        if (existing != null)
        {
            _cardRef = existing;
            return;
        }

        CardArtLibrary.EnsureLoaded();
        var cardGo = new GameObject(CardRefChildName);
        cardGo.transform.SetParent(_owner.RootTransform, false);
        cardGo.transform.localRotation = CardArtLibrary.WorldVisualRotation;
        cardGo.transform.localScale = Vector3.one;
        _cardRef = cardGo.transform;
    }

    void EnsurePsaModel()
    {
        if (_psaModel != null)
            return;

        EnsureCardRef();
        Transform existing = _cardRef.Find(PsaModelChildName);
        if (existing != null)
        {
            _psaModel = existing;
            return;
        }

        GameObject prefab = PsaArtLibrary.LoadModelPrefab();
        if (prefab == null)
        {
            CreatePlaceholderModel();
            return;
        }

        var instance = Object.Instantiate(prefab, _cardRef, false);
        instance.name = PsaModelChildName;
        _psaModel = instance.transform;
        _visualBaseScale = Vector3.one;
        PsaArtLibrary.ApplySlabMaterials(_psaModel, _slotNumber, _variantIndex);
        StripVisualColliders(_psaModel);
    }

    void CreatePlaceholderModel()
    {
        var visualGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visualGo.name = PsaModelChildName;
        visualGo.transform.SetParent(_cardRef, false);
        _visualBaseScale = new Vector3(
            CardDimensions.Width * FootprintWidthFitMultiplier,
            CardDimensions.Height,
            CardDimensions.Thickness * 2f);
        _nativeThicknessAxis = 2;
        visualGo.transform.localScale = _visualBaseScale;

        var renderer = visualGo.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = PsaArtLibrary.CreateSlabMaterial(_slotNumber, _variantIndex);
        }

        StripVisualColliders(visualGo.transform);
        _psaModel = visualGo.transform;
    }

    void RefreshLayout()
    {
        EnsureVisual();
        if (_cardRef == null || _psaModel == null)
            return;

        _visualBaseScale = PsaVisualSettings.GetModelRootScaleOrDefault();
        ApplyCardRefGroundPose(faceDown: false);
        ApplyModelLocalTransform();
        if (!TryMeasureMeshBoundsInLocalSpace(_cardRef, _psaModel, out Vector3 nativeMin, out Vector3 nativeMax))
            return;

        GetFootprintAxes(nativeMax - nativeMin, out int nativeThickness, out _, out _);
        _nativeThicknessAxis = nativeThickness;

        RefreshGroundOffset(faceDown: false, out _groundOffsetYFaceUp);
        RefreshGroundOffset(faceDown: true, out _groundOffsetYFaceDown);
    }

    void ApplyModelLocalTransform()
    {
        if (_psaModel == null)
            return;

        _psaModel.localRotation = Quaternion.Euler(PsaVisualSettings.GetModelRootRotationEulerOrDefault());
        _psaModel.localScale = GetModelLocalScale();
        _psaModel.localPosition = PsaVisualSettings.GetModelRootPositionOrDefault();
    }

    bool UsesHandThinProfile()
    {
        return _owner.IsInHand || _owner.IsFlyingToHand;
    }

    Vector3 GetModelLocalScale()
    {
        if (!UsesHandThinProfile())
            return _visualBaseScale;

        float worldThicknessRatio = Mathf.Max(_bodyThickness, CardDimensions.Thickness) / CardDimensions.Thickness;
        float handMultiplier = PsaVisualSettings.GetHeldThicknessFitMultiplierOrDefault();
        if (Mathf.Approximately(worldThicknessRatio, 0f))
            return _visualBaseScale;

        Vector3 scale = _visualBaseScale;
        scale[_nativeThicknessAxis] *= handMultiplier / worldThicknessRatio;
        return scale;
    }

    float GetModelGroundOffsetY()
    {
        return _owner.IsGroundFaceDown ? _groundOffsetYFaceDown : _groundOffsetYFaceUp;
    }

    void RefreshGroundOffset(bool faceDown, out float groundOffsetY)
    {
        groundOffsetY = 0f;
        EnsureVisual();
        if (_cardRef == null || _psaModel == null)
            return;

        ApplyCardRefGroundPose(faceDown);
        _cardRef.localPosition = Vector3.zero;
        ApplyModelProbePose();

        if (!TryMeasureMeshBoundsInLocalSpace(_owner.RootTransform, _psaModel, out Vector3 min, out Vector3 max))
            return;

        groundOffsetY = (-CardDimensions.Thickness * 0.5f) - min.y;
        _bodyThickness = Mathf.Max(CardDimensions.Thickness, max.y - min.y);
    }

    void ApplyCardRefGroundPose(bool faceDown)
    {
        _cardRef.localPosition = Vector3.zero;
        _cardRef.localScale = Vector3.one;
        _cardRef.localRotation = GetWorldGroundLocalRotation(faceDown);
    }

    void ApplyModelProbePose()
    {
        if (_psaModel == null)
            return;

        ApplyModelLocalTransform();
    }

    void EnsureOutline()
    {
        EnsureVisual();
        if (_psaModel == null)
            return;

        if (_modelOutline == null)
            _modelOutline = _psaModel.GetComponent<Outline>();
        if (_modelOutline == null)
            _modelOutline = _psaModel.gameObject.AddComponent<Outline>();

        _modelOutline.OutlineMode = Outline.Mode.OutlineAll;
        _modelOutline.OutlineWidth = PackVisualSettings.GetQuickOutlineWidthOrDefault();
    }

    static Quaternion GetWorldGroundLocalRotation(bool showsBack)
    {
        Quaternion rotation = CardArtLibrary.WorldVisualRotation;
        if (showsBack)
            rotation *= Quaternion.Euler(180f, 0f, 0f);
        return rotation;
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

    static void StripVisualColliders(Transform visualRoot)
    {
        Collider[] colliders = visualRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                Object.Destroy(colliders[i]);
        }
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
