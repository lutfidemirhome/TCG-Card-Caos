using UnityEngine;

/// <summary>
/// Drag BoosterPackModelTest into the scene, tweak PackVisual transform, then copy values from the inspector.
/// </summary>
[ExecuteAlways]
public sealed class PackModelTestTuning : MonoBehaviour
{
    const string VisualChildName = "PackVisual";

    [SerializeField] Transform packVisual;

    public Transform PackVisual => packVisual;

    public void EnsureVisualReference()
    {
        if (packVisual != null)
            return;

        packVisual = transform.Find(VisualChildName);
    }

    public bool TryGetVisualTransform(out Vector3 localPosition, out Quaternion localRotation, out Vector3 localScale)
    {
        EnsureVisualReference();
        if (packVisual == null)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            localScale = Vector3.one;
            return false;
        }

        localPosition = packVisual.localPosition;
        localRotation = packVisual.localRotation;
        localScale = packVisual.localScale;
        return true;
    }

    public void ApplyPlaceholderOrientation()
    {
        EnsureVisualReference();
        if (packVisual == null)
            return;

        packVisual.localPosition = Vector3.zero;
        packVisual.localRotation = CardArtLibrary.WorldVisualRotation;
        packVisual.localScale = new Vector3(
            CardDimensions.Width,
            CardDimensions.Height,
            CardDimensions.Thickness * PackVisualSettings.GetThicknessFitMultiplierOrDefault());
    }

    void OnDrawGizmosSelected()
    {
        float rootScale = transform.lossyScale.x;
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
        Vector3 flatSize = new Vector3(
            CardDimensions.Width * rootScale,
            0.002f,
            CardDimensions.Height * rootScale);
        Gizmos.DrawWireCube(transform.position, flatSize);

        Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.85f);
        Gizmos.DrawWireCube(
            transform.position + Vector3.up * (CardDimensions.Thickness * rootScale * 0.5f),
            new Vector3(
                CardDimensions.Width * rootScale,
                CardDimensions.Thickness * rootScale,
                CardDimensions.Height * rootScale));
    }
}
