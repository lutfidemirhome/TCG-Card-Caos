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

    public bool IsOpen => root != null && root.activeSelf;

    void Awake()
    {
        UiEventSystem.Ensure();

        if (root == null)
            root = gameObject;

        if (loadGamePanel == null)
            loadGamePanel = GetComponentInChildren<LoadGamePanelView>(true);

        Wire(resumeButton, Resume);
        Wire(saveButton, OnSaveGame);
        Wire(loadButton, OnLoadGame);
        Wire(settingsButton, OnSettings);
        Wire(quitButton, OnQuit);

        if (loadGamePanel != null)
            loadGamePanel.Hide();

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

        if (IsOpen && Input.GetMouseButtonDown(0) && !PointerOverLoadGame())
            TryClickPauseButton();
    }

    bool PointerOverLoadGame()
    {
        return loadGamePanel != null && loadGamePanel.IsOpen;
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
        if (root != null)
            root.SetActive(true);
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

        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    void Resume()
    {
        Hide();
        GamePause.SetPaused(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnSaveGame()
    {
        GameSaveStore.SaveManual();
    }

    void OnLoadGame()
    {
        if (loadGamePanel != null)
            loadGamePanel.Show();
        else
            Debug.Log("[Pause] Load Game panel is missing. Run TCG Card Caos → UI → Add In-Game Pause Menu.");
    }

    static void OnSettings()
    {
        Debug.Log("[Pause] Settings is not implemented yet.");
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
