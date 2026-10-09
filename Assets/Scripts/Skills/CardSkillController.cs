using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class SkillAutoshelfContext
{
    public string cabinetId;
    public int row = -1;
    public string series;
    public bool psa;
    public PsaCardSet psaSet;
    public int psaNumber;
}

/// <summary>Skills reuse normal pickup/placement transactions; no physics or save shortcuts.</summary>
public sealed class CardSkillController : MonoBehaviour
{
    readonly List<WorldCard> _ground = new List<WorldCard>(6000);
    readonly List<WorldCard> _matching = new List<WorldCard>(32);
    readonly List<WorldCard> _held = new List<WorldCard>(10);
    readonly Dictionary<WorldCard, SkillMarker> _cardMarkers = new Dictionary<WorldCard, SkillMarker>(16);
    readonly List<WorldCard> _removeMarkers = new List<WorldCard>(16);
    readonly Dictionary<Component, SkillMarker> _guideMarkers = new Dictionary<Component, SkillMarker>(16);
    readonly List<Component> _guideCabinets = new List<Component>(16);
    readonly List<Component> _removeGuideMarkers = new List<Component>(16);
    bool _autoStarted;
    CardShelf[] _shelves;
    PsaCabinet[] _psaCabinets;
    InteractionController _interaction;
    float _effectRefresh, _autoPlace;
    ulong _progressRevision, _groundRevision;
    bool _guideVisible, _insightVisible;
    WorldCard _selected;
    CardShelf _autoShelf;
    PsaCabinet _autoPsaCabinet;
    int _autoRow = -1;
    CardGroup _autoGroup;
    SkillAutoshelfContext _boundAutoContext;

    // Capture a value, not the held card: taking the last X card can select Y automatically.
    struct CardGroup
    {
        public string series;
        public bool psa;
        public PsaCardSet set;
        public int number;

        public static bool TryFrom(WorldCard card, out CardGroup group)
        {
            group = default;
            if (card == null) return false;
            group.psa = card.UsesPsaSlab;
            if (group.psa)
            {
                group.set = card.PsaSet;
                group.number = card.PsaSlotNumber;
                return true;
            }
            return CardShelfSeries.TryGetSeriesId(card.Definition, out group.series);
        }

        public bool Matches(WorldCard card)
        {
            if (card == null || card.UsesPsaSlab != psa) return false;
            return psa ? card.PsaSet == set && card.PsaSlotNumber == number
                : CardShelfSeries.TryGetSeriesId(card.Definition, out string other) && other == series;
        }
    }

    void Start()
    {
        _shelves = FindObjectsByType<CardShelf>(FindObjectsSortMode.None);
        _psaCabinets = FindObjectsByType<PsaCabinet>(FindObjectsSortMode.None);
        _interaction = FindFirstObjectByType<InteractionController>();
    }

    void LateUpdate()
    {
        if (!SkillProgress.Ready || GameSceneLoader.IsLoading || !CardInstancedRenderManager.IsGameplayReady) return;
        if (WelcomePopupView.IsWaitingForStart) return;
        if (GamePause.IsPaused) return;
        SkillProgress.Tick(Time.deltaTime);
        if (Cursor.lockState == CursorLockMode.Locked && !SkillPanelView.ConsumesPauseInput)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.P))
            {
                SkillProgress.EnableSkillPointsForTesting();
                _autoStarted = false;
                _boundAutoContext = null;
                _autoShelf = null;
                _autoPsaCabinet = null;
                _autoRow = -1;
                _autoGroup = default;
                _autoPlace = 0f;
                _effectRefresh = 0f;
            }
#endif
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                if (!Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) && !Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i))) continue;
                Activate((int)SkillCatalog.HotbarSkill(i));
            }
        }
        if (SkillProgress.ActiveTime((int)CardSkill.Autoshelving) > 0f)
        {
            RestoreAutoshelfContextIfNeeded();
            if (_autoStarted && Cursor.lockState == CursorLockMode.Locked && !SkillPanelView.ConsumesPauseInput)
            {
                _autoPlace -= Time.deltaTime;
                if (_autoPlace <= 0f) { _autoPlace = 0.16f; TryAutoshelf(true); }
            }
        }
        else _autoStarted = false;
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
            case CardSkill.ShelfGuide: success = CollectGuideCabinets(hand.SelectedHeldCard); break;
            case CardSkill.Insight: success = FindMatching(hand.SelectedHeldCard); break;
            case CardSkill.Autoshelving:
                success = hand.SelectedHeldCard != null && hand.SelectedHeldCard.IsHeld
                    && CollectGuideCabinets(hand.SelectedHeldCard);
                break;
        }
        if (success)
        {
            SkillProgress.Used(skill, skill == (int)CardSkill.Assemble ? hand.SkillPickupFlightDuration : -1f);
            if (skill == (int)CardSkill.Autoshelving)
            {
                // Activation arms the skill; only an explicit interaction starts a batch.
                _autoStarted = false;
                SkillProgress.SetAutoshelfContext(null);
                _boundAutoContext = null;
                _autoShelf = null;
                _autoPsaCabinet = null;
                _autoRow = -1;
            }
            _effectRefresh = 0f;
            _selected = null;
        }
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
            // Picking up a support wakes its neighbours immediately. Do not let that
            // change this skill's eligible cards halfway through the same batch.
            // TryPickup safely stops a moving card's physics, just like manual pickup.
            if (hand.TryPickupForSkill(card, collected)) collected++;
        }
        return collected > 0;
    }

    bool FindMatching(WorldCard selected)
    {
        _matching.Clear();
        if (!CardGroup.TryFrom(selected, out CardGroup group)) return false;
        CardGroundStack.CopyCardsForSkill(_ground);
        foreach (WorldCard card in _ground)
        {
            if (!card || !card.gameObject.activeInHierarchy || card.IsInHand || card.IsFlyingToShelf
                || card.IsPackReveal || card.IsShelfRowCompleteLocked) continue;
            if (!group.Matches(card)) continue;
            if (card.GetComponentInParent<CardShelfSlot>() != null || card.GetComponentInParent<PsaCabinetSlot>() != null) continue;
            _matching.Add(card);
        }
        return _matching.Count > 0;
    }

    bool CollectGuideCabinets(WorldCard card)
    {
        _guideCabinets.Clear();
        if (card == null) return false;
        if (card.UsesPsaSlab)
        {
            if (_psaCabinets != null)
                foreach (PsaCabinet cabinet in _psaCabinets)
                    if (cabinet && cabinet.gameObject.activeInHierarchy && cabinet.AcceptsSkillCard(card))
                        _guideCabinets.Add(cabinet);
        }
        else if (_shelves != null)
            foreach (CardShelf shelf in _shelves)
                // Guide also shows suitable cabinets whose slots are already occupied.
                if (shelf && shelf.gameObject.activeInHierarchy && shelf.AcceptsDefinition(card.Definition))
                    _guideCabinets.Add(shelf);
        return _guideCabinets.Count > 0;
    }

    // Called before ordinary E interaction so the selected card cannot be placed twice,
    // or replaced by the next hand selection before we capture its series.
    public bool TryStartAutoshelvingFromInteract()
    {
        if (!isActiveAndEnabled || !SkillProgress.Ready || GamePause.IsPaused
            || GameSceneLoader.IsLoading || !CardInstancedRenderManager.IsGameplayReady
            || WelcomePopupView.IsWaitingForStart || SkillPanelView.ConsumesPauseInput
            || Cursor.lockState != CursorLockMode.Locked
            || SkillProgress.ActiveTime((int)CardSkill.Autoshelving) <= 0f) return false;
        _autoStarted = false;
        if (!TryAutoshelf(false)) return false;
        var context = new SkillAutoshelfContext {
            cabinetId = PersistentId.Resolve(_autoGroup.psa ? (Component)_autoPsaCabinet : _autoShelf),
            row = _autoRow, series = _autoGroup.series, psa = _autoGroup.psa,
            psaSet = _autoGroup.set, psaNumber = _autoGroup.number
        };
        SkillProgress.SetAutoshelfContext(context);
        _boundAutoContext = SkillProgress.AutoshelfContext;
        GameSaveDirtyTracker.MarkDirty();
        _autoPlace = 0f;
        _autoStarted = true;
        return true;
    }

    bool TryAutoshelf(bool place)
    {
        if (_interaction == null)
            _interaction = FindFirstObjectByType<InteractionController>();
        PlayerCardHand hand = PlayerCardHand.Instance;
        if (_interaction == null || hand == null || hand.IsHandInputLocked || hand.IsAwaitingRevealCollect) return false;
        if (!place)
        {
            WorldCard selected = hand.SelectedHeldCard;
            if (selected == null || !selected.IsHeld || !CardGroup.TryFrom(selected, out _autoGroup)) return false;
            _autoShelf = !_autoGroup.psa ? _interaction.AimedSkillShelf : null;
            _autoRow = !_autoGroup.psa ? _interaction.AimedSkillRow : -1;
            _autoPsaCabinet = _autoGroup.psa ? _interaction.AimedSkillPsaCabinet : null;
        }

        // The active effect belongs to the row and card group chosen by the E press.
        // Looking away pauses placement; looking at another row cannot redirect it.
        if (_autoGroup.psa)
        {
            if (_autoPsaCabinet == null || _interaction.AimedSkillPsaCabinet != _autoPsaCabinet) return false;
        }
        else if (_autoShelf == null || _autoRow < 0 || _interaction.AimedSkillShelf != _autoShelf
            || _interaction.AimedSkillRow != _autoRow) return false;
        hand.CopyHeldCards(_held);
        _held.RemoveAll(card => card == null || !card.IsHeld);
        _held.Sort(PlayerCardHand.CompareCardsForSkill);
        foreach (WorldCard card in _held)
        {
            if (!_autoGroup.Matches(card)) continue;
            if (_autoGroup.psa)
            {
                if (_autoPsaCabinet.TryFindSkillSlot(card, out _))
                    return !place || _autoPsaCabinet.TryPlaceSkillCard(hand, card);
            }
            else if (_autoShelf.TryFindSkillSlot(card, _autoRow, out _))
                return !place || _autoShelf.TryPlaceSkillCard(hand, card, _autoRow);
        }
        return false;
    }

    void RestoreAutoshelfContextIfNeeded()
    {
        SkillAutoshelfContext context = SkillProgress.AutoshelfContext;
        if (ReferenceEquals(context, _boundAutoContext)) return;
        _boundAutoContext = context;
        // Loading an active effect never moves cards without another E press.
        _autoStarted = false;
        _autoShelf = null;
        _autoPsaCabinet = null;
        _autoRow = -1;
        _autoGroup = default;
        if (context == null || string.IsNullOrEmpty(context.cabinetId)) return;
        _autoGroup = new CardGroup { series = context.series, psa = context.psa, set = context.psaSet, number = context.psaNumber };
        if (context.psa)
        {
            if (!PsaArtLibrary.IsCabinetSlotNumber(context.psaNumber) || _psaCabinets == null) return;
            foreach (PsaCabinet cabinet in _psaCabinets)
                if (cabinet != null && PersistentId.Resolve(cabinet) == context.cabinetId)
                { _autoPsaCabinet = cabinet; break; }
        }
        else if (context.row >= 0 && !string.IsNullOrEmpty(context.series) && _shelves != null)
        {
            foreach (CardShelf shelf in _shelves)
                if (shelf != null && PersistentId.Resolve(shelf) == context.cabinetId)
                { _autoShelf = shelf; _autoRow = context.row; break; }
        }
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
            _guideCabinets.Clear();
            if (guide) CollectGuideCabinets(selected);
            _removeGuideMarkers.Clear();
            foreach (var entry in _guideMarkers)
                if (entry.Key == null || entry.Value == null || !_guideCabinets.Contains(entry.Key))
                {
                    RemoveMarker(entry.Value);
                    _removeGuideMarkers.Add(entry.Key);
                }
            foreach (Component cabinet in _removeGuideMarkers) _guideMarkers.Remove(cabinet);
            foreach (Component cabinet in _guideCabinets)
                if (!_guideMarkers.ContainsKey(cabinet))
                    _guideMarkers.Add(cabinet, cabinet is CardShelf shelf ? SkillMarker.ForShelf(shelf)
                        : SkillMarker.ForPsaCabinet((PsaCabinet)cabinet));
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
        foreach (SkillMarker marker in _guideMarkers.Values) RemoveMarker(marker);
        _guideMarkers.Clear(); _guideCabinets.Clear(); _removeGuideMarkers.Clear();
        foreach (SkillMarker marker in _cardMarkers.Values) RemoveMarker(marker);
        _cardMarkers.Clear(); _removeMarkers.Clear();
    }
    void OnDisable() { _autoStarted = false; ClearMarkers(); _selected = null; _guideVisible = _insightVisible = false; }
}
