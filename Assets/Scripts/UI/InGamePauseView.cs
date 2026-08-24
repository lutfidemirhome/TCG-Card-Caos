using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Authored in-game pause overlay. Visuals live in MainScene under InGamePauseCanvas.
/// ESC opens/closes it and freezes gameplay.
/// </summary>
public class InGamePauseView : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] Button resumeButton;
    [SerializeField] Button saveButton;
    [SerializeField] Button loadButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button quitButton;
    [SerializeField] LoadGamePanelView loadGamePanel;
    [SerializeField] SaveGamePanelView saveGamePanel;
    [SerializeField] SettingsPanelView settingsPanel;

    public bool IsOpen => root != null && root.activeSelf;

    void Awake()
    {
        UiEventSystem.Ensure();

        if (root == null)
            root = gameObject;

        if (loadGamePanel == null)
            loadGamePanel = GetComponentInChildren<LoadGamePanelView>(true);

        if (saveGamePanel == null)
            saveGamePanel = GetComponentInChildren<SaveGamePanelView>(true);

        if (saveGamePanel == null && loadGamePanel != null)
            saveGamePanel = SaveGamePanelView.CreateFromLoadPanel(loadGamePanel, transform);

        if (settingsPanel == null)
            settingsPanel = GetComponentInChildren<SettingsPanelView>(true);
        if (settingsPanel == null)
            settingsPanel = SettingsPanelView.Ensure(transform);

        Wire(resumeButton, Resume);
        Wire(saveButton, OnSaveGame);
        Wire(loadButton, OnLoadGame);
        Wire(settingsButton, OnSettings);
        Wire(quitButton, OnQuit);

        if (loadGamePanel != null)
            loadGamePanel.Hide();
        if (saveGamePanel != null)
            saveGamePanel.Hide();

        Hide();
    }

    void OnDestroy()
    {
        if (GamePause.IsPaused)
            GamePause.SetPaused(false);
    }

    void Update()
    {
        if (GameSceneLoader.IsLoading)
            return;

        if (!GameScenes.IsActiveGameScene())
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null && settingsPanel.IsOpen)
            {
                settingsPanel.Cancel();
                return;
            }

            if (saveGamePanel != null && saveGamePanel.IsOpen)
            {
                saveGamePanel.Hide();
                return;
            }

            if (loadGamePanel != null && loadGamePanel.IsOpen)
            {
                loadGamePanel.Hide();
                return;
            }

            if (IsOpen)
                Resume();
            else
                Show();
            return;
        }

        if (IsOpen && Input.GetMouseButtonDown(0) && !PointerOverOverlay())
            TryClickPauseButton();
    }

    bool PointerOverOverlay()
    {
        return (loadGamePanel != null && loadGamePanel.IsOpen)
            || (saveGamePanel != null && saveGamePanel.IsOpen)
            || (settingsPanel != null && settingsPanel.IsOpen);
    }

    void TryClickPauseButton()
    {
        if (ContainsPointer(resumeButton))
        {
            Resume();
            return;
        }

        if (ContainsPointer(saveButton))
        {
            OnSaveGame();
            return;
        }

        if (ContainsPointer(loadButton))
        {
            OnLoadGame();
            return;
        }

        if (ContainsPointer(settingsButton))
        {
            OnSettings();
            return;
        }

        if (ContainsPointer(quitButton))
            OnQuit();
    }

    static bool ContainsPointer(Button button)
    {
        if (button == null || !button.isActiveAndEnabled)
            return false;

        var rect = button.transform as RectTransform;
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    public void Show()
    {
        WelcomePopupView.CoverForPause();
        DemoCompleteView.CoverForPause();

        if (root != null)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }
        else
            gameObject.SetActive(true);

        GamePause.SetPaused(true);
        UiEventSystem.Ensure();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (loadGamePanel != null)
            loadGamePanel.Hide();
        if (saveGamePanel != null)
            saveGamePanel.Hide();
        if (settingsPanel != null)
            settingsPanel.Hide();

        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    void Resume()
    {
        Hide();

        if (WelcomePopupView.IsWaitingForStart)
        {
            WelcomePopupView.RestoreAfterPause();
            return;
        }

        if (DemoCompleteView.IsBlockingGameplay)
        {
            DemoCompleteView.RestoreAfterPause();
            return;
        }

        GamePause.SetPaused(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnSaveGame()
    {
        if (loadGamePanel != null)
            loadGamePanel.Hide();
        if (settingsPanel != null)
            settingsPanel.Hide();

        if (saveGamePanel != null)
            saveGamePanel.Show();
        else
            Debug.Log("[Pause] Save Game panel is missing.");
    }

    void OnLoadGame()
    {
        if (saveGamePanel != null)
            saveGamePanel.Hide();
        if (settingsPanel != null)
            settingsPanel.Hide();

        if (loadGamePanel != null)
            loadGamePanel.Show();
        else
            Debug.Log("[Pause] Load Game panel is missing. Run TCG Card Caos → UI → Add In-Game Pause Menu.");
    }

    void OnSettings()
    {
        if (loadGamePanel != null)
            loadGamePanel.Hide();
        if (saveGamePanel != null)
            saveGamePanel.Hide();

        if (settingsPanel == null)
            settingsPanel = SettingsPanelView.Ensure(transform);

        if (settingsPanel != null)
            settingsPanel.Show();
    }

    void OnQuit()
    {
        Hide();
        GamePause.SetPaused(false);
        GameSaveStore.SaveBeforeLeaveGameplay();
        SceneManager.LoadScene(GameScenes.MainMenu);
    }

    static void Wire(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
