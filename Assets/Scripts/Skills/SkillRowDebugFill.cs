#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

/// <summary>On-demand test shortcut. Moves existing ground cards through normal shelf placement.</summary>
internal static class SkillRowDebugFill
{
    sealed class RowPlan
    {
        public string Series;
        public List<CardShelfSlot> Slots;
        public readonly List<WorldCard> Cards = new List<WorldCard>();
    }

    public static void FillTwoRows(CardShelf[] shelves)
    {
        if (!SkillProgress.Ready || shelves == null) return;
        var progress = SkillProgress.Capture();
        var creditedRows = new HashSet<string>(progress.completedRows);
        var creditedSeries = new HashSet<string>(progress.completedSeries);
        var ground = new List<WorldCard>();
        CardGroundStack.CopyCardsForSkill(ground);
        var series = new Dictionary<string, List<WorldCard>>();
        foreach (WorldCard card in ground)
        {
            if (!card || !card.gameObject.activeInHierarchy || card.UsesPsaSlab || card.IsInHand
                || card.IsFlyingToShelf || card.IsPackReveal || card.IsShelfRowCompleteLocked
                || card.GetComponentInParent<CardShelfSlot>() != null
                || card.GetComponentInParent<PsaCabinetSlot>() != null
                || (card.IsPhysicsSimulating && card.PhysicsBody != null && !card.PhysicsBody.IsSleeping())) continue;
            if (!CardShelfSeries.TryGetSeriesId(card.Definition, out string id)
                || creditedSeries.Contains("normal:" + id)) continue;
            if (!series.TryGetValue(id, out var cards)) series.Add(id, cards = new List<WorldCard>());
            cards.Add(card);
        }

        foreach (CardShelf shelf in shelves)
        {
            if (!shelf || !shelf.gameObject.activeInHierarchy) continue;
            shelf.RefreshSlotCache();
            var rows = new SortedDictionary<int, List<CardShelfSlot>>();
            foreach (CardShelfSlot slot in shelf.GetComponentsInChildren<CardShelfSlot>())
            {
                if (slot.GetComponentInParent<CardShelf>() != shelf) continue;
                if (!rows.TryGetValue(slot.RowIndex, out var slots)) rows.Add(slot.RowIndex, slots = new List<CardShelfSlot>());
                slots.Add(slot);
            }
            string path = PersistentId.BuildPathFallback(shelf.transform);
            RowPlan first = null;
            foreach (var row in rows)
            {
                if (creditedRows.Contains(path + ":row:" + row.Key) || row.Value.Count != shelf.SlotsPerRow) continue;
                bool empty = true;
                foreach (CardShelfSlot slot in row.Value) if (!slot.IsEmpty) { empty = false; break; }
                if (!empty) continue;
                RowPlan plan = FindPlan(shelf, row.Value, series, first != null ? first.Series : null);
                if (plan == null) continue;
                if (first == null) { first = plan; continue; }

                // Both distinct series are complete before changing any card/slot. No async gap.
                int oldPoints = SkillProgress.Points;
                Place(shelf, first);
                Place(shelf, plan);
                GameSaveSignals.MarkDirty();
                SkillProgress.NotifyShelfChanged(shelf);
                CabinetSignCompleteOverlay.Refresh(shelf);
                if (shelf.IsComplete()) GameSaveSignals.NotifyMilestone();
                Debug.Log($"[Skills Test] O: {shelf.name}, 2 raf dolduruldu. Kazanılan yükseltme hakkı: {SkillProgress.Points - oldPoints}.", shelf);
                return;
            }
        }
        Debug.LogWarning("[Skills Test] O: Aynı dolapta iki boş rafı tamamlayacak, daha önce puan kazandırmamış iki tam kart serisi yerde bulunamadı. Hiçbir kart değiştirilmedi.");
    }

    static RowPlan FindPlan(CardShelf shelf, List<CardShelfSlot> slots,
        Dictionary<string, List<WorldCard>> series, string excludedSeries)
    {
        foreach (var group in series)
        {
            if (group.Key == excludedSeries || group.Value.Count < slots.Count) continue;
            var plan = new RowPlan { Series = group.Key, Slots = slots };
            foreach (CardShelfSlot slot in slots)
            {
                int foundBefore = plan.Cards.Count;
                foreach (WorldCard card in group.Value)
                {
                    if (plan.Cards.Contains(card) || !shelf.IsCorrectPlacement(card, slot)) continue;
                    plan.Cards.Add(card);
                    break;
                }
                if (plan.Cards.Count == foundBefore) break;
            }
            if (plan.Cards.Count == slots.Count) return plan;
        }
        return null;
    }

    static void Place(CardShelf shelf, RowPlan plan)
    {
        for (int i = 0; i < plan.Slots.Count; i++)
            plan.Slots[i].RestoreOccupiedCard(plan.Cards[i], shelf.SurfacePadding, true, false);
    }
}
#endif
