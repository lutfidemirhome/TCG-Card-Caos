using TMPro;
using UnityEngine;

/// <summary>
/// Always-on gameplay HUD: shelf count, placed cards, and hand size.
/// Layout lives in MainScene under InGameHudCanvas.
/// </summary>
public class InGameHudView : MonoBehaviour
{
    [SerializeField] TMP_Text shelvesValueText;
    [SerializeField] TMP_Text cardsValueText;
    [SerializeField] TMP_Text handValueText;
    const float RefreshInterval = 0.12f;
    float _refreshTimer;
    CounterState _shelvesCounter, _cardsCounter, _handCounter;

    struct CounterState
    {
        public TMP_Text Label;
        public int Current, Maximum;
        public bool Valid;
    }

    void OnEnable()
    {
        _shelvesCounter.Valid = _cardsCounter.Valid = _handCounter.Valid = false;
    }

    void Awake()
    {
        BindIfMissing();
        if (GetComponent<SkillBarView>() == null)
            gameObject.AddComponent<SkillBarView>();
        Refresh();
        SkillPanelView.Ensure(transform);
    }

    void LateUpdate()
    {
        _refreshTimer += Time.unscaledDeltaTime;
        if (_refreshTimer < RefreshInterval)
            return;

        _refreshTimer = 0f;
        Refresh();
    }

    void BindIfMissing()
    {
        if (shelvesValueText == null)
            shelvesValueText = FindText("Panel_TopLeft/ShelvesValue");
        if (cardsValueText == null)
            cardsValueText = FindText("Panel_TopLeft/CardsValue");
        if (handValueText == null)
            handValueText = FindText("Panel_Hand/HandValue");
    }

    TMP_Text FindText(string path)
    {
        Transform found = transform.Find(path);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    void Refresh()
    {
        GameProgressCounter.Snapshot progress = GameProgressCounter.Capture();
        SetCounter(shelvesValueText, progress.shelvesCompleted, progress.totalShelves, ref _shelvesCounter);
        SetCounter(cardsValueText, progress.cardsPlaced, progress.totalCards, ref _cardsCounter);

        PlayerCardHand hand = PlayerCardHand.Instance;
        int held = hand != null ? hand.OccupiedHandSlots : 0;
        SetCounter(handValueText, held, CardDimensions.MaxHandSize, ref _handCounter);
    }

    static void SetCounter(TMP_Text label, int current, int max, ref CounterState state)
    {
        if (label == null)
            return;

        // Keep the same display format, but allocate text only when its values change.
        if (state.Valid && state.Label == label && state.Current == current && state.Maximum == max)
            return;
        label.text = current + " / " + max;
        state.Label = label;
        state.Current = current;
        state.Maximum = max;
        state.Valid = true;
    }
}
