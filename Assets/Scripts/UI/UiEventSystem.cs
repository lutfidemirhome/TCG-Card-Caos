using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Gameplay scenes do not always have an EventSystem; UI clicks need one.
/// </summary>
public static class UiEventSystem
{
    public static void Ensure()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;

        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();
    }
}
