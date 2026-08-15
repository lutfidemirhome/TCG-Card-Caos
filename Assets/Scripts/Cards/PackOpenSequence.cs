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

        // Move pack to screen center.
        float moveInDuration = duration * 0.22f;
        float elapsed = 0f;
        Vector3 packStartWorldPos = pack.transform.position;
        Quaternion packStartWorldRot = pack.transform.rotation;
        Vector3 packStartScale = pack.transform.localScale;

        while (elapsed < moveInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveInDuration);
            Vector3 targetWorldPos = revealRoot.TransformPoint(Vector3.zero);
            Quaternion targetWorldRot = revealRoot.rotation;
            pack.transform.position = Vector3.Lerp(packStartWorldPos, targetWorldPos, t);
            pack.transform.rotation = Quaternion.Slerp(packStartWorldRot, targetWorldRot, t);
            pack.transform.localScale = Vector3.Lerp(packStartScale, Vector3.one * heldScale, t);
            yield return null;
        }

        // Tear / shake.
        float tearDuration = duration * 0.18f;
        elapsed = 0f;
        Transform packTransform = pack.transform;
        Vector3 basePos = packTransform.position;
        while (elapsed < tearDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / tearDuration;
            float shake = (1f - t) * 0.012f;
            packTransform.position = basePos + new Vector3(
                Random.Range(-shake, shake),
                Random.Range(-shake, shake),
                Random.Range(-shake * 0.5f, shake * 0.5f));
            packTransform.localScale = Vector3.one * heldScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.04f);
            yield return null;
        }

        packTransform.position = basePos;

        // Spawn reveal cards in a horizontal row above the pack — backs toward the player.
        IReadOnlyList<CardDefinition> contents = pack.RollContents(CardDimensions.CardsPerBoosterPack);
        var revealCards = new List<WorldCard>(contents.Count);
        float cardSpacing = CardDimensions.Width * revealScale * RevealCardSpacingFactor;
        float rowWidth = cardSpacing * Mathf.Max(0, contents.Count - 1);
        float halfHeight = CardDimensions.Height * revealScale * 0.5f;

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
            packTransform.localScale = Vector3.one * heldScale * (1f - t * 0.85f);

            for (int i = 0; i < revealCards.Count; i++)
            {
                WorldCard card = revealCards[i];
                if (card == null)
                    continue;

                card.transform.localScale = Vector3.one * (revealScale * t);
            }

            yield return null;
        }

        for (int i = 0; i < revealCards.Count; i++)
        {
            if (revealCards[i] != null)
                revealCards[i].transform.localScale = Vector3.one * revealScale;
        }

        Object.Destroy(pack.gameObject);

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
        hand.ClearHeldPackReference();
        hand.SetHandInputLocked(false);
    }
}
