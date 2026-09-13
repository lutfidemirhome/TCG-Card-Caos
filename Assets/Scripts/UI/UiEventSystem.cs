using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Gameplay scenes do not always have an EventSystem; UI clicks need one.
/// </summary>
public static class UiEventSystem
{
    public static void Ensure()
    {
        // The active system already registers itself; avoid searching the large
        // gameplay scene again each time an overlay opens.
        if (EventSystem.current != null)
            return;

        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;

        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();
    }
}
