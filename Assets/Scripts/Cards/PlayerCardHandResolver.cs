using UnityEngine;

/// <summary>
/// Resolves <see cref="PlayerCardHand"/> from common interaction hierarchies.
/// </summary>
public static class PlayerCardHandResolver
{
    public static PlayerCardHand FromInteractor(GameObject interactor)
    {
        if (interactor == null)
            return null;

        PlayerCardHand hand = interactor.GetComponent<PlayerCardHand>();
        if (hand != null)
            return hand;

        return interactor.GetComponentInChildren<PlayerCardHand>();
    }

    public static PlayerCardHand FromInteractorOrInstance(GameObject interactor)
    {
        PlayerCardHand hand = FromInteractor(interactor);
        return hand != null ? hand : PlayerCardHand.Instance;
    }

    public static PlayerCardHand FromTransformHierarchy(Transform transform)
    {
        if (transform == null)
            return null;

        PlayerCardHand hand = transform.GetComponentInParent<PlayerCardHand>();
        if (hand != null)
            return hand;

        return transform.root.GetComponentInChildren<PlayerCardHand>();
    }
}
