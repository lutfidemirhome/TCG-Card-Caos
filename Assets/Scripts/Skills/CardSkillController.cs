using System.Collections.Generic;
using UnityEngine;

/// <summary>Skills reuse normal pickup/placement transactions; no physics or save shortcuts.</summary>
public sealed class CardSkillController : MonoBehaviour
{
    readonly List<WorldCard> _ground = new List<WorldCard>(6000);
    readonly List<WorldCard> _matching = new List<WorldCard>(32);
    readonly List<WorldCard> _held = new List<WorldCard>(10);
    readonly Dictionary<WorldCard, SkillMarker> _cardMarkers = new Dictionary<WorldCard, SkillMarker>(16);
    readonly List<WorldCard> _removeMarkers = new List<WorldCard>(16);
    SkillMarker _guideMarker;
    CardShelf _guideShelf;
    CardShelf[] _shelves;
    InteractionController _interaction;
    float _effectRefresh, _autoPlace;
    ulong _progressRevision, _groundRevision;
    bool _guideVisible, _insightVisible;
    WorldCard _selected;

    void Start()
    {
        _shelves = FindObjectsByType<CardShelf>(FindObjectsSortMode.None);
        _interaction = FindFirstObjectByType<InteractionController>();
    }

    void LateUpdate()
    {
        if (!SkillProgress.Ready || GameSceneLoader.IsLoading || !CardInstancedRenderManager.IsGameplayReady) return;
        if (WelcomePopupView.IsWaitingForStart) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool skillPanelOpen = SkillPanelView.Instance != null && SkillPanelView.Instance.IsOpen;
        if (Input.GetKeyDown(KeyCode.P) && (skillPanelOpen || (!GamePause.IsPaused && Cursor.lockState == CursorLockMode.Locked)))
        {
            SkillProgress.ToggleAllSkillsForTesting();
            ClearMarkers();
            _selected = null;
            _guideVisible = _insightVisible = false;
            _effectRefresh = _autoPlace = 0f;
        }
#endif
        if (GamePause.IsPaused) return;
        SkillProgress.Tick(Time.deltaTime);
        if (Cursor.lockState == CursorLockMode.Locked && !SkillPanelView.ConsumesPauseInput)
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                if (!Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha2 + i)) && !Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad2 + i))) continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Shift+6 is the existing save shortcut; other skills still work while sprinting.
                if (i == (int)CardSkill.Autoshelving && Input.GetKeyDown(KeyCode.Alpha6)
                    && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) continue;
#endif
                Activate(i);
            }
        if (SkillProgress.ActiveTime((int)CardSkill.Autoshelving) > 0f)
        {
            _autoPlace -= Time.deltaTime;
            if (_autoPlace <= 0f) { _autoPlace = 0.16f; TryAutoshelf(true); }
        }
        _effectRefresh -= Time.deltaTime;
        if (_effectRefresh > 0f) return;
        _effectRefresh = 0.2f;
        RefreshMarkers();
    }

    public bool Activate(int skill)
    {
        if (GamePause.IsPaused || GameSceneLoader.IsLoading || !SkillProgress.CanUse(skill)) return false;
        PlayerCardHand hand = PlayerCardHand.Instance;
        if (!hand || hand.IsHandInputLocked || hand.IsAwaitingRevealCollect) return false;
        bool success = false;
        switch ((CardSkill)skill)
        {
            case CardSkill.Assemble: success = Assemble(hand, SkillCatalog.Effect(skill, SkillProgress.Level(skill))); break;
            case CardSkill.Sort: success = hand.SortHeldCardsForSkill(); break;
            case CardSkill.ShelfGuide: success = FindShelf(hand.SelectedHeldCard) != null; break;
            case CardSkill.Insight: success = FindMatching(hand.SelectedHeldCard); break;
            case CardSkill.Autoshelving: success = TryAutoshelf(false); break;
        }
        if (success) { SkillProgress.Used(skill); _effectRefresh = 0f; _selected = null; }
        return success;
    }

    bool Assemble(PlayerCardHand hand, int limit)
    {
        if (hand.AvailableSlots <= 0 || !FindMatching(hand.SelectedHeldCard)) return false;
        Vector3 origin = hand.transform.position;
        _matching.Sort((a, b) => (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));
        int collected = 0;
        foreach (WorldCard card in _matching)
        {
            if (collected >= limit || hand.AvailableSlots <= 0) break;
            if (card.IsPhysicsSimulating && !card.PhysicsBody.IsSleeping()) continue;
            if (hand.TryPickup(card, collected == 0)) collected++;
        }
        return collected > 0;
    }

    bool FindMatching(WorldCard selected)
    {
        _matching.Clear();
        if (!selected || selected.UsesPsaSlab || !CardShelfSeries.TryGetSeriesId(selected.Definition, out string series)) return false;
        CardGroundStack.CopyCardsForSkill(_ground);
        foreach (WorldCard card in _ground)
        {
            if (!card || !card.gameObject.activeInHierarchy || card.UsesPsaSlab || card.IsInHand || card.IsFlyingToShelf
                || card.IsPackReveal || card.IsShelfRowCompleteLocked) continue;
            if (!CardShelfSeries.TryGetSeriesId(card.Definition, out string other) || other != series) continue;
            if (card.GetComponentInParent<CardShelfSlot>() != null || card.GetComponentInParent<PsaCabinetSlot>() != null) continue;
            _matching.Add(card);
        }
        return _matching.Count > 0;
    }

    CardShelf FindShelf(WorldCard card)
    {
        if (card == null || card.UsesPsaSlab || _shelves == null) return null;
        CardShelf best = null;
        float distance = float.MaxValue;
        foreach (CardShelf shelf in _shelves)
        {
            // Guide the player to the correct cabinet even if its intended slot is occupied.
            // Autoshelving independently enforces correct, empty slots before moving a card.
            if (!shelf || !shelf.gameObject.activeInHierarchy || !shelf.AcceptsDefinition(card.Definition)) continue;
            float d = (shelf.transform.position - card.transform.position).sqrMagnitude;
            if (d < distance) { best = shelf; distance = d; }
        }
        return best;
    }

    bool TryAutoshelf(bool place)
    {
        PlayerCardHand hand = PlayerCardHand.Instance;
        CardShelf shelf = _interaction != null ? _interaction.AimedSkillShelf : null;
        if (shelf == null || hand == null || hand.IsHandInputLocked || hand.IsAwaitingRevealCollect) return false;
        hand.CopyHeldCards(_held);
        foreach (WorldCard card in _held)
            if (card != null && card.IsHeld && shelf.TryFindSkillSlot(card, out _))
                return !place || shelf.TryPlaceSkillCard(hand, card);
        return false;
    }

    void RefreshMarkers()
    {
        bool guide = SkillProgress.ActiveTime((int)CardSkill.ShelfGuide) > 0f;
        bool insight = SkillProgress.ActiveTime((int)CardSkill.Insight) > 0f;
        WorldCard selected = PlayerCardHand.Instance != null ? PlayerCardHand.Instance.SelectedHeldCard : null;
        ulong revision = GameProgressCounter.Revision;
        ulong groundRevision = CardGroundStack.MembershipRevision;
        bool selectionChanged = _selected != selected;
        bool refreshGuide = guide != _guideVisible || selectionChanged;
        bool refreshInsight = insight != _insightVisible || selectionChanged
            || (insight && (revision != _progressRevision || groundRevision != _groundRevision));
        _guideVisible = guide; _insightVisible = insight; _selected = selected;
        _progressRevision = revision; _groundRevision = groundRevision;
        if (refreshGuide)
        {
            CardShelf shelf = guide ? FindShelf(selected) : null;
            if (shelf != _guideShelf || (shelf != null && _guideMarker == null))
            {
                RemoveMarker(_guideMarker);
                _guideShelf = shelf;
                _guideMarker = shelf != null ? SkillMarker.ForShelf(shelf) : null;
            }
        }
        if (!refreshInsight) return;
        _matching.Clear();
        if (insight) FindMatching(selected);
        // Retain unchanged visuals: pickups/throws should only add or remove affected targets.
        _removeMarkers.Clear();
        foreach (var entry in _cardMarkers)
            if (entry.Key == null || entry.Value == null || !_matching.Contains(entry.Key))
            {
                RemoveMarker(entry.Value);
                _removeMarkers.Add(entry.Key);
            }
        foreach (WorldCard card in _removeMarkers) _cardMarkers.Remove(card);
        foreach (WorldCard card in _matching)
            if (!_cardMarkers.ContainsKey(card)) _cardMarkers.Add(card, SkillMarker.ForCard(card));
    }

    static void RemoveMarker(SkillMarker marker)
    {
        if (marker == null) return;
        marker.gameObject.SetActive(false);
        Object.Destroy(marker.gameObject);
    }

    void ClearMarkers()
    {
        RemoveMarker(_guideMarker);
        _guideMarker = null; _guideShelf = null;
        foreach (SkillMarker marker in _cardMarkers.Values) RemoveMarker(marker);
        _cardMarkers.Clear(); _removeMarkers.Clear();
    }
    void OnDisable() { ClearMarkers(); _selected = null; _guideVisible = _insightVisible = false; }
}
