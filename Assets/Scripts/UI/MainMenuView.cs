using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Wires the authored main menu hierarchy. All visuals live in the scene so art can be swapped in
/// the editor; this component only handles input, links and the version label.
/// </summary>
public class MainMenuView : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] Button newGameButton;
    [SerializeField] Button loadGameButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button quitButton;
    [SerializeField] Button feedbackButton;

    [Header("Version label")]
    [SerializeField] TMP_Text versionText;
    [Tooltip("Leave empty to show Application.version from Project Settings.")]
    [SerializeField] string versionOverride = "0.30";
    [SerializeField] string versionPrefix = "v";

    [Header("Social buttons")]
    [SerializeField] Button discordButton;
    [SerializeField] Button tiktokButton;
    [SerializeField] Button instagramButton;
    [SerializeField] Button youtubeButton;

    [Header("Links")]
    [SerializeField] string feedbackUrl = "";
    [SerializeField] string discordUrl = "";
    [SerializeField] string tiktokUrl = "";
    [SerializeField] string instagramUrl = "";
    [SerializeField] string youtubeUrl = "";

    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();
        WireButtons();
        ApplyVersionLabel();

        GameAssetPrewarm.Start(this);
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();
    }

    void WireButtons()
    {
        Bind(newGameButton, OnNewGameClicked);
        Bind(loadGameButton, OnLoadGameClicked);
        Bind(settingsButton, OnSettingsClicked);
        Bind(quitButton, OnQuitClicked);

        Bind(feedbackButton, () => OpenUrl(feedbackUrl, "Feedback"));
        Bind(discordButton, () => OpenUrl(discordUrl, "Discord"));
        Bind(tiktokButton, () => OpenUrl(tiktokUrl, "TikTok"));
        Bind(instagramButton, () => OpenUrl(instagramUrl, "Instagram"));
        Bind(youtubeButton, () => OpenUrl(youtubeUrl, "YouTube"));
    }

    static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    void ApplyVersionLabel()
    {
        if (versionText == null)
            return;

        string version = string.IsNullOrWhiteSpace(versionOverride)
            ? Application.version
            : versionOverride;

        versionText.text = versionPrefix + version;
    }

    static void OpenUrl(string url, string label)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Debug.Log("[MainMenu] " + label + " URL is not set yet.");
            return;
        }

        Application.OpenURL(url);
    }

    static void OnNewGameClicked() => GameSceneLoader.LoadGame();

    static void OnLoadGameClicked() =>
        Debug.Log("[MainMenu] Load Game is not implemented yet.");

    static void OnSettingsClicked() =>
        Debug.Log("[MainMenu] Settings is not implemented yet.");

    static void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
