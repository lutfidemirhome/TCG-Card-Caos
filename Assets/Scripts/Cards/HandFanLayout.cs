using UnityEngine;

public struct HandCardPose
{
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;
    public float Scale;
}

/// <summary>
/// Camera-local fan using VoodooDeck arc math. Cards lie flat on XZ in the world;
/// we pitch them toward the camera, then fan around the view axis.
/// </summary>
public static class HandFanLayout
{
    static readonly Vector3 FlatCardBottomToCenter = new Vector3(0f, 0f, 1f);

    public static HandCardPose GetPose(int index, int count, in HandFanLayoutSettings settings, bool isSelected)
    {
        if (count <= 0)
            return default;

        float t = count <= 1 ? 0.5f : index / (float)(count - 1);
        float normalized = t - 0.5f;

        float fanAngle = GetAdaptiveFanAngle(count, settings);
        float angle = normalized * fanAngle;
        float radians = angle * Mathf.Deg2Rad;

        float x = Mathf.Sin(radians) * settings.Radius;
        float y = settings.FanPivotY
            + (Mathf.Cos(radians) - 1f) * settings.Radius
            - Mathf.Abs(normalized) * settings.VerticalCurve;

        if (count > 1)
        {
            float widthBudget = GetWidthBudget(count, settings);
            if (widthBudget > 0f)
                x = Mathf.Clamp(x, -widthBudget * 0.5f, widthBudget * 0.5f);
        }

        float z = ComputeFanDepthZ(index, count, settings, isSelected);

        var faceCamera = Quaternion.FromToRotation(Vector3.up, -Vector3.forward);
        if (settings.CardPitchDegrees != 0f)
            faceCamera = Quaternion.AngleAxis(settings.CardPitchDegrees, Vector3.right) * faceCamera;

        var fanSpin = Quaternion.AngleAxis(-angle, Vector3.forward);
        var localRotation = fanSpin * faceCamera;

        float halfHeight = CardDimensions.Height * settings.HeldScale * 0.5f;
        var bottomPivot = new Vector3(x, y, z);
        var localPosition = bottomPivot + localRotation * (FlatCardBottomToCenter * halfHeight);

        if (settings.CardVisualOffsetY != 0f)
            localPosition += localRotation * (Vector3.up * settings.CardVisualOffsetY);

        if (isSelected)
            localPosition += Vector3.up * settings.SelectedLift;

        return new HandCardPose
        {
            LocalPosition = localPosition,
            LocalRotation = localRotation,
            Scale = settings.HeldScale,
        };
    }

    public static float ComputeFanDepthZ(int index, int count, in HandFanLayoutSettings settings, bool isSelected)
    {
        if (count <= 0)
            return 0f;

        float centerIndex = (count - 1) * 0.5f;
        float z = (centerIndex - index) * settings.CardDepthStep;

        if (isSelected && count > 1)
        {
            // Depth buffer decides draw order for 3D meshes — sibling index is not enough.
            // Snap the selected card to the front of the fan regardless of scroll position.
            z = (centerIndex - (count - 1)) * settings.CardDepthStep - settings.SelectedForwardMargin;
        }

        return z;
    }

    public static HandCardPose WithFanDepthZ(
        HandCardPose pose,
        int fanIndex,
        int fanCount,
        in HandFanLayoutSettings settings,
        bool isSelected,
        float[] fanDepthZ)
    {
        if (fanDepthZ == null || fanDepthZ.Length <= fanIndex)
            return pose;

        float defaultZ = ComputeFanDepthZ(fanIndex, fanCount, settings, isSelected);
        float targetZ = fanDepthZ[fanIndex];
        if (isSelected && fanCount > 1)
            targetZ = fanDepthZ[fanCount - 1] - settings.SelectedForwardMargin;

        pose.LocalPosition += new Vector3(0f, 0f, targetZ - defaultZ);
        return pose;
    }

    static float GetAdaptiveFanAngle(int count, in HandFanLayoutSettings settings)
    {
        if (count <= 1)
            return 0f;

        float span = Mathf.Max(1f, settings.FanAngleRampCardSpan);
        float ramp = Mathf.Clamp01((count - 2f) / span);
        float angle = Mathf.Lerp(settings.MinFanAngle, settings.MaxFanAngle, ramp);
        return Mathf.Min(angle, settings.FanAngleHardCap);
    }

    static float GetWidthBudget(int count, in HandFanLayoutSettings settings)
    {
        if (count <= 1)
            return 0f;

        int overTwo = Mathf.Max(0, count - 2);
        float widthBudget = settings.MaxWidth + overTwo * settings.ExtraMaxWidthPerCard;
        widthBudget = Mathf.Min(widthBudget, settings.MaxWidthClamp);

        float spacingByWidth = widthBudget / Mathf.Max(1, count - 1);
        float spacing = Mathf.Clamp(spacingByWidth, settings.MinCardSpacing, settings.MaxCardSpacing);
        return spacing * (count - 1);
    }
}

public struct HandFanLayoutSettings
{
    public float HeldScale;
    public float CardPitchDegrees;
    public float CardDepthStep;
    public float CardVisualOffsetY;

    public float MinFanAngle;
    public float MaxFanAngle;
    public float FanAngleRampCardSpan;
    public float FanAngleHardCap;

    public float Radius;
    public float FanPivotY;
    public float VerticalCurve;

    public float MaxWidth;
    public float ExtraMaxWidthPerCard;
    public float MaxWidthClamp;
    public float MinCardSpacing;
    public float MaxCardSpacing;

    public float SelectedLift;
    public float SelectedForwardMargin;
}
