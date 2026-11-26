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
    [SerializeField] Button howToPlayButton;
    [SerializeField] Button quitButton;
    [SerializeField] LoadGamePanelView loadGamePanel;
    [SerializeField] SaveGamePanelView saveGamePanel;
    [SerializeField] SettingsPanelView settingsPanel;

    CoopTeamPanel team;
    LoadGameConfirmView leaveAfterSaveFailure;
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
        if (CoopSession.InShop)
        {
            if (saveButton) saveButton.interactable = CoopSession.Rooms?.IsHost == true;
            if (loadButton)
            {
                loadButton.interactable = CoopSession.Rooms is ICoopRoomControls;
                var caption = loadButton.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (caption && caption.TryGetComponent(out LocalizedText localized)) localized.SetKey("coop.team");
                else if (caption) caption.text = Localization.Get("coop.team");
            }
        }
        Wire(saveButton, OnSaveGame);
        Wire(loadButton, OnLoadGame);
        Wire(settingsButton, OnSettings);
        Wire(howToPlayButton, OnHowToPlay);
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
        if (SkillPanelView.ConsumesPauseInput || CabinetTutorialView.ConsumesInput) return;
        if (GameSceneLoader.IsLoading)
            return;

        if (!GameScenes.IsActiveGameScene())
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (leaveAfterSaveFailure && leaveAfterSaveFailure.IsOpen) { leaveAfterSaveFailure.Hide(); return; }
            if (team && team.gameObject.activeSelf) { team.gameObject.SetActive(false); return; }
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
        return (leaveAfterSaveFailure && leaveAfterSaveFailure.IsOpen) || (team && team.gameObject.activeSelf) || (loadGamePanel != null && loadGamePanel.IsOpen)
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

        if (ContainsPointer(howToPlayButton))
        {
            OnHowToPlay();
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
        if (CoopSession.InShop) SetSaveCaption(LocalizationKeys.PauseSave);
        WelcomePopupView.CoverForPause();
        DemoCompleteView.CoverForPause();

        if (root != null)
        {
            root.SetActive(true);
            BringToFrontIfNeeded(root.transform);
        }
        else
            gameObject.SetActive(true);

        GamePause.SetPaused(true);
        UiEventSystem.Ensure();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        BringToFrontIfNeeded(transform);
    }

    static void BringToFrontIfNeeded(Transform target)
    {
        Transform parent = target.parent;
        int siblingCount = parent != null ? parent.childCount : target.gameObject.scene.rootCount;
        if (target.GetSiblingIndex() != siblingCount - 1)
            target.SetAsLastSibling();
    }

    public void Hide()
    {
        if (leaveAfterSaveFailure) leaveAfterSaveFailure.Hide();
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
        ResumeInternal();
    }

    void ResumeInternal()
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
        if (CoopSession.InShop)
        {
            if (CoopSession.Rooms?.IsHost == true && CoopShopCards.Active != null)
                SetSaveCaption(CoopShopCards.Active.FlushCheckpoint() ? LocalizationKeys.SaveGameSaved : "save.failed");
            return;
        }
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
        if (CoopSession.InShop)
        {
            if (CoopSession.Rooms is ICoopRoomControls)
            {
                if (!team) team = CoopTeamPanel.Create(root.transform);
                team.gameObject.SetActive(true);
                team.transform.SetAsLastSibling();
            }
            return;
        }
        if (saveGamePanel != null)
            saveGamePanel.Hide();
        if (settingsPanel != null)
            settingsPanel.Hide();

        if (loadGamePanel != null)
            loadGamePanel.Show();
        else
            Debug.Log("[Pause] Load Game panel is missing. Run TCG Card Chaos → UI → Add In-Game Pause Menu.");
    }

    void OnHowToPlay()
    {
        if (IsOpen && !GameSceneLoader.IsLoading)
            CabinetTutorialView.ShowFromMenu(transform);
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
        if (leaveAfterSaveFailure && leaveAfterSaveFailure.IsOpen) return;
        if (CoopSession.InShop && CoopSession.Rooms?.IsHost == true && CoopShopCards.Active != null &&
            !CoopShopCards.Active.FlushCheckpoint())
        {
            SetSaveCaption("save.failed");
            if (!leaveAfterSaveFailure && loadGamePanel)
            {
                var template = loadGamePanel.GetComponentInChildren<LoadGameConfirmView>(true);
                if (template)
                {
                    leaveAfterSaveFailure = Instantiate(template, root.transform, false);
                    leaveAfterSaveFailure.Show();
                    var message = leaveAfterSaveFailure.transform.Find("Band/Message");
                    var localized = message.GetComponent<LocalizedText>() ?? message.gameObject.AddComponent<LocalizedText>();
                    localized.SetKey("coop.leave_unsaved");
                    leaveAfterSaveFailure.YesButton.onClick = new Button.ButtonClickedEvent();
                    leaveAfterSaveFailure.NoButton.onClick = new Button.ButtonClickedEvent();
                    leaveAfterSaveFailure.YesButton.onClick.AddListener(LeaveToMenu);
                    leaveAfterSaveFailure.NoButton.onClick.AddListener(leaveAfterSaveFailure.Hide);
                }
            }
            if (leaveAfterSaveFailure) leaveAfterSaveFailure.Show();
            return;
        }
        LeaveToMenu();
    }

    void SetSaveCaption(string key)
    {
        var label = saveButton ? saveButton.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
        if (label && label.TryGetComponent(out LocalizedText localized)) localized.SetKey(key);
        else if (label) label.text = Localization.Get(key);
    }

    void LeaveToMenu()
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
