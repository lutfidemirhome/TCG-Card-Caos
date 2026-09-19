using UnityEngine;

/// <summary>Registers sleeping throws without changing the pose or contacts resolved by physics.</summary>
public static class CardSettlePlacement
{
    /// <summary>Used only to interpret older, non-physical authored/save poses.</summary>
    const float FlatUpDot = 0.94f;

    public static bool IsFlatOnFloor(Transform itemTransform) =>
        itemTransform != null && Mathf.Abs(itemTransform.up.y) >= FlatUpDot;

    public static void RegisterSleepingPose(WorldCard card)
    {
        if (card != null)
            CardGroundStack.TrackPhysicsPose(card);
    }

    public static void RegisterSleepingPose(WorldBoosterPack pack)
    {
        if (pack != null)
            CardGroundStack.TrackPack(pack);
    }
}
