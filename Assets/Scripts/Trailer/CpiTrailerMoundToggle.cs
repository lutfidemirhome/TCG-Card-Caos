using UnityEngine;
using System.Collections.Generic;

/// <summary>Trailer set dressing on the cpi branch. Reuses authored copies; never respawns cards.</summary>
[DisallowMultipleComponent]
public sealed class CpiTrailerMoundToggle : MonoBehaviour
{
    static CpiTrailerMoundToggle _instance;
    public static bool IsCaptureScene => _instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _instance = null;
    void Awake() => _instance = this;
    void OnDestroy() { if (_instance == this) _instance = null; }

    [SerializeField] GameObject moundRoot;
    [SerializeField] KeyCode toggleKey = KeyCode.Y;
    WorldCard[] _cards;
    readonly List<WorldCard> _remaining = new List<WorldCard>();

    public bool IsVisible => moundRoot != null && moundRoot.activeSelf;

    void Update()
    {
        if (!Input.GetKeyDown(toggleKey) || !CardInstancedRenderManager.IsGameplayReady
            || GamePause.IsPaused || GameSceneLoader.IsLoading || WelcomePopupView.IsWaitingForStart
            || SkillPanelView.ConsumesPauseInput || MinorSkillUnlockView.ConsumesInput)
            return;

        SetVisible(!IsVisible);
    }

    public void SetVisible(bool visible)
    {
        if (moundRoot == null || moundRoot.activeSelf == visible) return;
        if (_cards == null) _cards = moundRoot.GetComponentsInChildren<WorldCard>(true);
        if (!visible && Application.isPlaying)
        {
            _remaining.Clear();
            foreach (var card in _cards)
                if (card != null && card.transform.IsChildOf(moundRoot.transform)) _remaining.Add(card);
            CardGroundStack.UntrackBatch(_remaining);
        }
        moundRoot.SetActive(visible);
        if (!visible || !Application.isPlaying) return;

        foreach (var card in _cards)
        {
            // Picked-up copies leave this hierarchy and must stay in the player's hand/shelf.
            if (card == null || !card.transform.IsChildOf(moundRoot.transform)) continue;
            card.RegisterForInstancedGround();
            CardGroundStack.TrackPhysicsPose(card);
        }
    }
}
