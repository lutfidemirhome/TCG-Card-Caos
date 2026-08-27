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
    const float MenuButtonStep = 105f;

    /// <summary>Quit sits on the same line as the Feedback button; the stack grows upward from it.</summary>
    const float QuitAnchoredY = -437f;

    [Header("Buttons")]
    [SerializeField] Button continueButton;
    [SerializeField] Button newGameButton;
    [SerializeField] Button loadGameButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button quitButton;
    [SerializeField] Button feedbackButton;

    [Header("Menu layout")]
    [SerializeField] RectTransform logoRect;

    [Header("Version label")]
    [SerializeField] TMP_Text versionText;
    [Tooltip("Leave empty to show Application.version from Project Settings → Player → Version.")]
    [SerializeField] string versionOverride;
    [SerializeField] string versionPrefix = "v";

    [Header("Social buttons")]
    [SerializeField] Button discordButton;
    [SerializeField] Button tiktokButton;
    [SerializeField] Button instagramButton;
    [SerializeField] Button youtubeButton;

    [Header("Load Game")]
    [SerializeField] LoadGamePanelView loadGamePanel;

    [Header("Settings")]
    [SerializeField] SettingsPanelView settingsPanel;

    [Header("Links")]
    [SerializeField] string feedbackUrl = "https://discord.gg/THgcvu3CC";
    [SerializeField] string discordUrl = "https://discord.gg/pFAN48K66";
    [SerializeField] string tiktokUrl = "https://www.tiktok.com/@odd.forge.games";
    [SerializeField] string instagramUrl = "https://www.instagram.com/oddforgegames";
    [SerializeField] string youtubeUrl = "https://www.youtube.com/@oddforgegames";

    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();
        EnsureLogoReference();
        if (loadGamePanel == null)
            loadGamePanel = GetComponentInChildren<LoadGamePanelView>(true);
        if (settingsPanel == null)
            settingsPanel = SettingsPanelView.Ensure(transform);
        if (settingsPanel != null)
            settingsPanel.Hide();
        ApplyMenuLayout();
        WireButtons();
        ApplyVersionLabel();

        GameAssetPrewarm.Start(this);
    }

    void EnsureLogoReference()
    {
        if (logoRect != null)
            return;

        Transform logo = transform.Find("Logo");
        if (logo != null)
            logoRect = logo.GetComponent<RectTransform>();
    }

    void ApplyMenuLayout()
    {
        bool hasSave = GameSaveStore.HasAnySave();

        if (continueButton != null)
            continueButton.gameObject.SetActive(hasSave);

        SetAnchoredY(quitButton, QuitAnchoredY);
        SetAnchoredY(settingsButton, QuitAnchoredY + MenuButtonStep);
        SetAnchoredY(loadGameButton, QuitAnchoredY + MenuButtonStep * 2f);
        SetAnchoredY(newGameButton, QuitAnchoredY + MenuButtonStep * 3f);
        SetAnchoredY(continueButton, QuitAnchoredY + MenuButtonStep * 4f);
    }

    static void SetAnchoredY(Component target, float y)
    {
        if (target == null)
            return;

        RectTransform rect = target.transform as RectTransform;
        if (rect == null)
            return;

        Vector2 position = rect.anchoredPosition;
        position.y = y;
        rect.anchoredPosition = position;
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
        Bind(continueButton, OnContinueClicked);
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
        UiTextFit.Apply(versionText);
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

    static void OnContinueClicked() => GameSceneLoader.ContinueGame();

    static void OnNewGameClicked() => GameSceneLoader.StartNewGame();

    void OnLoadGameClicked()
    {
        if (settingsPanel != null)
            settingsPanel.Hide();

        if (loadGamePanel != null)
            loadGamePanel.Show();
        else
            Debug.Log("[MainMenu] Load Game panel is missing. Run TCG Card Chaos → UI → Add Load Game Panel.");
    }

    void OnSettingsClicked()
    {
        if (loadGamePanel != null)
            loadGamePanel.Hide();

        if (settingsPanel == null)
            settingsPanel = SettingsPanelView.Ensure(transform);

        if (settingsPanel != null)
            settingsPanel.Show();
    }

    static void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
