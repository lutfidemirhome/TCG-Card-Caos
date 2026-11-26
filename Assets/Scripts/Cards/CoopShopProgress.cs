using UnityEngine;

public sealed partial class CoopShopCards
{
    GameProgressCounter.Snapshot? sharedProgress;
    CoopCardMessage? pendingProgress;
    uint progressRevision;
    float nextProgress;

    public bool TryGetProgress(out GameProgressCounter.Snapshot value)
    {
        value = sharedProgress.GetValueOrDefault();
        return local != host && sharedProgress.HasValue;
    }

    void TickProgress()
    {
        if (local != host || members.Count <= 1 || Time.unscaledTime < nextProgress) return;
        nextProgress = Time.unscaledTime + 1f;
        var m = ProgressMessage();
        foreach (ulong id in members) if (id != local) send(id, m);
    }

    CoopCardMessage ProgressMessage()
    {
        var s = GameProgressCounter.Capture();
        return new CoopCardMessage { Action = CoopCardAction.Progress, Revision = revision,
            Position = new Vector3(s.cardsPlaced, s.shelvesCompleted, s.cabinetsCompleted),
            Velocity = new Vector3(s.totalCards, s.totalShelves, s.totalCabinets) };
    }

    void ReceiveProgress(CoopCardMessage m)
    {
        if (m.Revision < progressRevision) return;
        if (m.Revision > revision)
        {
            // Finish the first advertised snapshot even when new moves keep arriving.
            if (!pendingProgress.HasValue || m.Revision < pendingProgress.Value.Revision) pendingProgress = m;
            return;
        }
        if (pendingProgress.HasValue && pendingProgress.Value.Revision <= m.Revision) pendingProgress = null;
        progressRevision = m.Revision;
        var snapshot = new GameProgressCounter.Snapshot((int)m.Position.x, (int)m.Velocity.x,
            (int)m.Position.y, (int)m.Velocity.y, (int)m.Position.z, (int)m.Velocity.z);
        if (sharedProgress.HasValue && sharedProgress.Value.Equals(snapshot)) return;
        sharedProgress = snapshot;
        GameProgressCounter.InvalidateCache();
    }
}
