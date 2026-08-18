using UnityEngine;

/// <summary>
/// Basic interactable for testing. Attach to cards, shelves, etc. later.
/// </summary>
public class SimpleInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] string actionLabel = "Action";

    public string GetPromptText()
    {
        return InteractPrompt.Format(actionLabel);
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log("Interacted with " + name, this);
    }
}
