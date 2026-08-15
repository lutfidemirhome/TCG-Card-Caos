using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Camera-local pack opening: center reveal, five cards with backs first, flip to fronts,
/// wait for Enter, then fly into the hand fan.
/// </summary>
public static class PackOpenSequence
{
    const float FlipDuration = 0.16f;
    const float FlipStagger = 0.07f;
    const float RevealScreenScaleMultiplier = 1.2f;
    const float RevealCardSpacingFactor = 1.12f;
    const float PackDriftDurationFactor = 0.11f;
    const float PackExitDropFactor = 1.35f;
    const float PackDriftSwayFactor = 0.28f;
    const float PackPostShakePause = 0.05f;

    public static IEnumerator Run(PlayerCardHand hand, WorldBoosterPack pack, Camera camera)
    {
        if (hand == null || pack == null || camera == null)
            yield break;

        pack.BeginOpening();
        hand.SetHandInputLocked(true);

        Transform revealRoot = new GameObject("PackRevealRoot").transform;
        revealRoot.SetParent(camera.transform, false);

        float revealDistance = hand.OpenRevealDistance;
        float revealHeight = hand.OpenRevealHeight;
        revealRoot.localPosition = new Vector3(0f, revealHeight, revealDistance);
        revealRoot.localRotation = Quaternion.identity;

        float heldScale = hand.EffectiveHeldScale;
        float revealScale = heldScale * RevealScreenScaleMultiplier;
        float duration = hand.OpenSequenceDuration;
        Quaternion revealFaceRotation = CardArtLibrary.RevealRootLocalRotation;
        Quaternion packRevealWorldRotation = revealRoot.rotation * revealFaceRotation;
        Vector3 packRevealLocalStart = new Vector3(0f, -CardDimensions.Height * heldScale * 0.18f, 0f);

        // Move pack toward the reveal anchor, facing the player like a held pack.
        float moveInDuration = duration * 0.22f;
        float elapsed = 0f;
        Vector3 packStartWorldPos = pack.transform.position;
        Quaternion packStartWorldRot = pack.transform.rotation;
        Vector3 packStartScale = pack.transform.localScale;
        Vector3 packRevealWorldPos = revealRoot.TransformPoint(packRevealLocalStart);

        while (elapsed < moveInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveInDuration);
            pack.transform.position = Vector3.Lerp(packStartWorldPos, packRevealWorldPos, t);
            pack.transform.rotation = Quaternion.Slerp(packStartWorldRot, packRevealWorldRotation, t);
            pack.transform.localScale = Vector3.Lerp(packStartScale, Vector3.one * heldScale, t);
            yield return null;
        }

        pack.transform.SetParent(revealRoot, false);
        pack.transform.localPosition = packRevealLocalStart;
        pack.ApplyRevealOpenPose(revealFaceRotation);
        pack.transform.localScale = Vector3.one * heldScale;

        Transform packTransform = pack.transform;
        Vector3 baseLocalPos = packTransform.localPosition;
        yield return AnticipationHoldRoutine(
            packTransform,
            baseLocalPos,
            heldScale,
            hand.OpenPackAnticipationHold);

        // Spawn reveal cards in a horizontal row above the pack — backs toward the player.
        IReadOnlyList<CardDefinition> contents = pack.RollContents(CardDimensions.CardsPerBoosterPack);
        var revealCards = new List<WorldCard>(contents.Count);
        float cardSpacing = CardDimensions.Width * revealScale * RevealCardSpacingFactor;
        float rowWidth = cardSpacing * Mathf.Max(0, contents.Count - 1);
        float halfHeight = CardDimensions.Height * revealScale * 0.5f;
        float packDriftDuration = duration * PackDriftDurationFactor;
        float packExitLocalY = -CardDimensions.Height * heldScale * PackExitDropFactor;
        float packDriftSwayX = CardDimensions.Width * heldScale * PackDriftSwayFactor;
        float packDriftSwaySign = Random.value < 0.5f ? -1f : 1f;
        bool packDestroyed = false;

        for (int i = 0; i < contents.Count; i++)
        {
            float x = contents.Count <= 1 ? 0f : -rowWidth * 0.5f + i * cardSpacing;
            Vector3 localPos = new Vector3(x, halfHeight + CardDimensions.Height * revealScale * 0.12f, 0f);
            Vector3 worldPos = revealRoot.TransformPoint(localPos);

            WorldCard card = CardFactory.CreateWorldCard(
                worldPos,
                revealRoot.rotation * revealFaceRotation,
                contents[i],
                paletteIndex: 0,
                cardName: "PackCard_" + (i + 1));

            card.BeginRevealPreview(
                revealRoot,
                localPos,
                revealFaceRotation,
                0f,
                showsBack: true);
            revealCards.Add(card);
        }

        float spreadDuration = duration * 0.28f;
        elapsed = 0f;
        while (elapsed < spreadDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / spreadDuration);

            if (!packDestroyed && pack != null)
            {
                float packT = Mathf.Clamp01(elapsed / packDriftDuration);
                float packFall = packT * packT;
                float packY = Mathf.Lerp(baseLocalPos.y, packExitLocalY, packFall);
                float packX = baseLocalPos.x
                    + packDriftSwaySign * packDriftSwayX * Mathf.Sin(packT * Mathf.PI);
                packTransform.localPosition = new Vector3(packX, packY, baseLocalPos.z);
                packTransform.localScale = Vector3.one * heldScale;

                if (packT >= 1f)
                {
                    Object.Destroy(pack.gameObject);
                    packDestroyed = true;
                }
            }

            for (int i = 0; i < revealCards.Count; i++)
            {
                WorldCard card = revealCards[i];
                if (card == null)
                    continue;

                card.transform.localScale = Vector3.one * (revealScale * t);
            }

            yield return null;
        }

        if (!packDestroyed && pack != null)
            Object.Destroy(pack.gameObject);

        for (int i = 0; i < revealCards.Count; i++)
        {
            if (revealCards[i] != null)
                revealCards[i].transform.localScale = Vector3.one * revealScale;
        }

        float settleDuration = duration * 0.12f;
        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Flip cards one by one from back to front.
        for (int i = 0; i < revealCards.Count; i++)
        {
            WorldCard card = revealCards[i];
            if (card == null)
                continue;

            float flipElapsed = 0f;
            while (flipElapsed < FlipDuration)
            {
                flipElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, flipElapsed / FlipDuration);
                card.SetRevealVisualFlip(t);
                yield return null;
            }

            card.SetRevealVisualFlip(1f);

            if (i < revealCards.Count - 1)
            {
                float staggerElapsed = 0f;
                while (staggerElapsed < FlipStagger)
                {
                    staggerElapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }

        // Hold the reveal screen until the player presses Enter again.
        hand.SetAwaitingRevealCollect(true);
        while (!hand.ConsumeRevealCollectRequest())
            yield return null;
        hand.SetAwaitingRevealCollect(false);

        // Fly cards into the hand fan.
        float flyDuration = duration * 0.35f;
        for (int i = 0; i < revealCards.Count; i++)
        {
            WorldCard card = revealCards[i];
            if (card == null)
                continue;

            card.transform.SetParent(null, true);
            hand.AddRevealedCard(card, flyDuration, hand.PickupFlightArcHeight);
            yield return new WaitForSeconds(flyDuration * 0.12f);
        }

        float waitRemaining = flyDuration + 0.05f;
        while (waitRemaining > 0f)
        {
            waitRemaining -= Time.deltaTime;
            yield return null;
        }

        Object.Destroy(revealRoot.gameObject);
        hand.ClearHeldPackReference(pack);
        hand.SetHandInputLocked(false);
    }

    static IEnumerator AnticipationHoldRoutine(
        Transform packTransform,
        Vector3 baseLocalPos,
        float heldScale,
        float holdDuration)
    {
        if (packTransform == null || holdDuration <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / holdDuration);
            float intensity = Mathf.Lerp(0.002f, 0.007f, t * t);
            float tremor = Mathf.Sin(elapsed * 30f) * intensity * 0.25f;
            packTransform.localPosition = baseLocalPos + new Vector3(
                tremor + Random.Range(-intensity, intensity),
                tremor + Random.Range(-intensity, intensity),
                Random.Range(-intensity * 0.3f, intensity * 0.3f));
            packTransform.localScale = Vector3.one * heldScale * (1f + Mathf.Sin(elapsed * 22f) * 0.012f * t);
            yield return null;
        }

        packTransform.localPosition = baseLocalPos;
        packTransform.localScale = Vector3.one * heldScale;

        if (PackPostShakePause > 0f)
            yield return new WaitForSeconds(PackPostShakePause);
    }
}
