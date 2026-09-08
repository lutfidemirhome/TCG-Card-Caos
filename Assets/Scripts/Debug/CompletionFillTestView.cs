using UnityEngine;

/// <summary>
/// Play-mode test button. Hidden when <see cref="CompletionFillTest.Enabled"/> is false.
/// </summary>
public class CompletionFillTestView : MonoBehaviour
{
    void Update()
    {
        if (!CompletionFillTest.Enabled)
            return;

        if (Input.GetKeyDown(KeyCode.F8))
            CompletionFillTest.Run();
    }

    void OnGUI()
    {
        if (!CompletionFillTest.Enabled)
            return;

        const float width = 280f;
        const float height = 44f;
        var rect = new Rect(Screen.width - width - 16f, 16f, width, height);
        if (GUI.Button(rect, "TEST: Dolapları doldur  (F8)"))
            CompletionFillTest.Run();

        if (!string.IsNullOrEmpty(CompletionFillTest.LastSummary))
        {
            var summary = new Rect(16f, 16f, Screen.width - width - 48f, 54f);
            GUI.Label(summary, CompletionFillTest.LastSummary);
        }
    }
}
