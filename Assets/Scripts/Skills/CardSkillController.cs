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
    SkillMarker _guideMarker;
    Component _guideCabinet;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool skillPanelOpen = SkillPanelView.Instance != null && SkillPanelView.Instance.IsOpen;
        if (Input.GetKeyDown(KeyCode.P) && (skillPanelOpen || (!GamePause.IsPaused && Cursor.lockState == CursorLockMode.Locked)))
        {
            SkillProgress.ToggleAllSkillsForTesting();
            ClearMarkers();
            _selected = null;
            _guideVisible = _insightVisible = false;
            _effectRefresh = _autoPlace = 0f;
            _boundAutoContext = null;
            _autoShelf = null;
            _autoPsaCabinet = null;
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
            RestoreAutoshelfContextIfNeeded();
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
            case CardSkill.ShelfGuide: success = FindCabinet(hand.SelectedHeldCard) != null; break;
            case CardSkill.Insight: success = FindMatching(hand.SelectedHeldCard); break;
            case CardSkill.Autoshelving: success = TryAutoshelf(false); break;
        }
        if (success)
        {
            SkillProgress.Used(skill, skill == (int)CardSkill.Assemble ? hand.SkillPickupFlightDuration : -1f);
            if (skill == (int)CardSkill.Autoshelving)
            {
                var context = new SkillAutoshelfContext {
                    cabinetId = PersistentId.Resolve(_autoGroup.psa ? (Component)_autoPsaCabinet : _autoShelf),
                    row = _autoRow, series = _autoGroup.series, psa = _autoGroup.psa,
                    psaSet = _autoGroup.set, psaNumber = _autoGroup.number
                };
                SkillProgress.SetAutoshelfContext(context);
                _boundAutoContext = SkillProgress.IsTestingAllSkills ? context : SkillProgress.AutoshelfContext;
                _autoPlace = 0f;
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
            if (card.IsPhysicsSimulating && card.PhysicsBody != null && !card.PhysicsBody.IsSleeping()) continue;
            if (hand.TryPickup(card, collected == 0)) collected++;
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

    Component FindCabinet(WorldCard card)
    {
        if (card == null) return null;
        Component best = null;
        float distance = float.MaxValue;
        if (card.UsesPsaSlab)
        {
            if (_psaCabinets == null) return null;
            foreach (PsaCabinet cabinet in _psaCabinets)
            {
                if (!cabinet || !cabinet.gameObject.activeInHierarchy || !cabinet.AcceptsSkillCard(card)) continue;
                float d = (cabinet.transform.position - card.transform.position).sqrMagnitude;
                if (d < distance) { best = cabinet; distance = d; }
            }
            return best;
        }
        if (_shelves == null) return null;
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
        if (_interaction == null || hand == null || hand.IsHandInputLocked || hand.IsAwaitingRevealCollect) return false;
        if (!place)
        {
            WorldCard selected = hand.SelectedHeldCard;
            if (selected == null || !selected.IsHeld || !CardGroup.TryFrom(selected, out _autoGroup)) return false;
            _autoShelf = !_autoGroup.psa ? _interaction.AimedSkillShelf : null;
            _autoRow = !_autoGroup.psa ? _interaction.AimedSkillRow : -1;
            _autoPsaCabinet = _autoGroup.psa ? _interaction.AimedSkillPsaCabinet : null;
        }

        // The active effect belongs to the row and card group chosen on activation.
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
        if (SkillProgress.IsTestingAllSkills) return;
        SkillAutoshelfContext context = SkillProgress.AutoshelfContext;
        if (ReferenceEquals(context, _boundAutoContext)) return;
        _boundAutoContext = context;
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
            Component cabinet = guide ? FindCabinet(selected) : null;
            if (cabinet != _guideCabinet || (cabinet != null && _guideMarker == null))
            {
                RemoveMarker(_guideMarker);
                _guideCabinet = cabinet;
                _guideMarker = cabinet is CardShelf shelf ? SkillMarker.ForShelf(shelf)
                    : cabinet is PsaCabinet psa ? SkillMarker.ForPsaCabinet(psa) : null;
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
        _guideMarker = null; _guideCabinet = null;
        foreach (SkillMarker marker in _cardMarkers.Values) RemoveMarker(marker);
        _cardMarkers.Clear(); _removeMarkers.Clear();
    }
    void OnDisable() { ClearMarkers(); _selected = null; _guideVisible = _insightVisible = false; }
}
