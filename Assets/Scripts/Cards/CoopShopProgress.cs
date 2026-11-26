using UnityEngine;

public sealed partial class CoopShopCards
{
    GameProgressCounter.Snapshot? sharedProgress;
    float nextProgress;

    public bool TryGetProgress(out GameProgressCounter.Snapshot value)
    {
        value = sharedProgress.GetValueOrDefault();
        return local != host && sharedProgress.HasValue;
    }

    void TickProgress()
    {
        if (local != host || Time.unscaledTime < nextProgress) return;
        nextProgress = Time.unscaledTime + 1f;
        var s = GameProgressCounter.Capture();
        var m = new CoopCardMessage { Action = CoopCardAction.Progress, Revision = revision,
            Position = new Vector3(s.cardsPlaced, s.shelvesCompleted, s.cabinetsCompleted),
            Velocity = new Vector3(s.totalCards, s.totalShelves, s.totalCabinets) };
        foreach (ulong id in members) if (id != local) send(id, m);
    }

    void ReceiveProgress(CoopCardMessage m)
    {
        if (revision != m.Revision) return;
        var snapshot = new GameProgressCounter.Snapshot((int)m.Position.x, (int)m.Velocity.x,
            (int)m.Position.y, (int)m.Velocity.y, (int)m.Position.z, (int)m.Velocity.z);
        if (sharedProgress.HasValue && sharedProgress.Value.Equals(snapshot)) return;
        sharedProgress = snapshot;
        GameProgressCounter.InvalidateCache();
    }
}
