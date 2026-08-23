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
    [SerializeField] int maxPlacedCards = GameHudLimits.MaxPlacedCards;

    void Awake()
    {
        BindIfMissing();
        Refresh();
    }

    void LateUpdate()
    {
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
        GameSaveWorldCollector.CountProgress(
            out int cardsPlaced,
            out _,
            out int shelvesCompleted,
            out _,
            out _,
            out int totalCards);

        SetCounter(shelvesValueText, shelvesCompleted, maxShelves);
        SetCounter(cardsValueText, cardsPlaced, totalCards);

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
