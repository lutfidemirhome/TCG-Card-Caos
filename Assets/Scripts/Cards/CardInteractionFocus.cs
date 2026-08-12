using UnityEngine;

/// <summary>
/// Keeps exactly one ground/shelf card collider active — the card the player is looking at.
/// </summary>
public static class CardInteractionFocus
{
    static WorldCard _focusedCard;

    public static WorldCard FocusedCard => _focusedCard;

    public static void SetFocusedCard(WorldCard card)
    {
        if (_focusedCard == card)
            return;

        if (_focusedCard != null)
            _focusedCard.SetPlayerAimFocus(false);

        _focusedCard = card;

        if (_focusedCard != null)
            _focusedCard.SetPlayerAimFocus(true);
    }

    public static void ClearFocus()
    {
        SetFocusedCard(null);
    }
}
