using UnityEngine;

/// <summary>Shared guards for world interactions; keys never occupy card-hand slots.</summary>
public static class MinorSkillInteraction
{
    public static bool CanInteract()
    {
        if (!Application.isPlaying || !MinorSkillProgress.Ready || GamePause.IsPaused
            || GameSceneLoader.IsLoading || !CardInstancedRenderManager.IsGameplayReady
            || SkillPanelView.ConsumesPauseInput)
            return false;

        PlayerCardHand hand = PlayerCardHand.Instance;
        return hand == null || (!hand.IsHandInputLocked && !hand.IsAwaitingRevealCollect);
    }
}
