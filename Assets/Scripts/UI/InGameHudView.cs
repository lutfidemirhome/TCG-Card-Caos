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
    [SerializeField] int maxShelves = GameHudLimits.MaxShelves;
    const float RefreshInterval = 0.12f;
    float _refreshTimer;

    void Awake()
    {
        BindIfMissing();
        if (GetComponent<SkillBarView>() == null)
            gameObject.AddComponent<SkillBarView>();
        Refresh();
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
        SetCounter(shelvesValueText, progress.shelvesCompleted, Mathf.Max(maxShelves, progress.totalShelves));
        SetCounter(cardsValueText, progress.cardsPlaced, progress.totalCards);

        PlayerCardHand hand = PlayerCardHand.Instance;
        int held = hand != null ? hand.OccupiedHandSlots : 0;
        SetCounter(handValueText, held, CardDimensions.MaxHandSize);
    }

    static void SetCounter(TMP_Text label, int current, int max)
    {
        if (label == null)
            return;

        label.text = current + " / " + max;
    }
}
