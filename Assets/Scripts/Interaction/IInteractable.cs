using UnityEngine;

public interface IInteractable
{
    string GetPromptText();
    void Interact(GameObject interactor);
}
