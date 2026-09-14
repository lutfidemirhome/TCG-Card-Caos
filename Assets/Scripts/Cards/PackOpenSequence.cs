using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Camera-local pack opening: center reveal, five cards with backs first, flip to fronts,
/// wait for F, then fly into the hand fan.
/// </summary>
public static class PackOpenSequence
{
    const float FlipDuration = 0.233f;
    const float FlipStagger = 0.054f;
    const float FlipRevealPopPeak = 1.09f;
    const float FlipRevealPopDuration = 0.117f;
    const float RevealWavePeak = 1.14f;
    const float RevealWavePulseDuration = 0.2f;
    const float RevealWaveStagger = 0.05f;
    const float RevealWaveDipHeightFactor = 0.042f;
    const float RevealScreenScaleMultiplier = 1.2f;
    const float RevealCardSpacingFactor = 1.12f;
    const float RevealScreenMargin = 0.05f;
    const float RevealCardAnchorHeight = 0.02f;
    const float PackRevealLocalYOffsetFactor = 0.44f;
    const float PackMoveToRevealDuration = 0.1f;
    const float PackDriftDurationFactor = 0.05f;
    const float PackExitDropFactor = 1.35f;
    const float PackPostShakePause = 0.05f;
    const float PackRevealSettleDropFactor = 0.14f;
    const float PackRevealSettleDurationFactor = 0.1f;
    const float CardEjectDuration = 0.26f;
    const float CardEjectStagger = 0.1f;
    const float CardEjectStartScaleFactor = 0.32f;
    const float CardEjectArcHeightFactor = 0.09f;
    const float PackEjectLocalYOffsetFactor = 0.22f;
    const float PackEjectLocalZInsetFactor = 2.8f;
    const float RevealBackdropAlpha = 0.62f;
    const float RevealBackdropFadeInDuration = 0.12f;
    const float RevealBackdropFadeOutDuration = 0.32f;

    public static IEnumerator Run(PlayerCardHand hand, WorldBoosterPack pack, Camera camera)
    {
        if (hand == null || pack == null || camera == null)
        {
            hand?.SetPackOpenMovementLocked(false);
            yield break;
        }

        pack.BeginOpening();
        hand.SetHandInputLocked(true);
        GameSoundEffects.EnsureExists();

        float revealDistance = hand.OpenRevealDistance;
        float packOnlyWorldDown = RevealCardAnchorHeight - hand.OpenRevealHeight;
        PackRevealBackdrop backdrop = PackRevealBackdrop.Create(camera, revealDistance);

        Transform revealRoot = new GameObject("PackRevealRoot").transform;
        revealRoot.SetParent(camera.transform, false);
        revealRoot.localPosition = new Vector3(0f, RevealCardAnchorHeight, revealDistance);
        revealRoot.localRotation = Quaternion.identity;

        float heldScale = hand.EffectiveHeldScale;
        float revealScale = FitRevealScale(
            camera,
            revealDistance,
            heldScale,
            CardDimensions.CardsPerBoosterPack);
        float cardRevealScale = revealScale * 0.75f;
        float packLowerOffset = CardDimensions.Height * revealScale * 0.4f;
        float duration = hand.OpenSequenceDuration;
        Quaternion revealFaceRotation = CardArtLibrary.RevealRootLocalRotation;
        Quaternion packRevealWorldRotation = revealRoot.rotation * revealFaceRotation;
        Vector3 packRevealLocalStart = new Vector3(
            0f,
            -CardDimensions.Height * revealScale * PackRevealLocalYOffsetFactor - packOnlyWorldDown - packLowerOffset,
            0f);

        // Snap pack toward the reveal anchor; backdrop fades in at the same time.
        float moveInDuration = PackMoveToRevealDuration;
        float elapsed = 0f;
        Vector3 packStartWorldPos = pack.transform.position;
        Quaternion packStartWorldRot = pack.transform.rotation;
        Vector3 packStartScale = pack.transform.localScale;
        Vector3 packRevealWorldPos = revealRoot.TransformPoint(packRevealLocalStart);

        while (elapsed < moveInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveInDuration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            pack.transform.position = Vector3.Lerp(packStartWorldPos, packRevealWorldPos, t);
            pack.transform.rotation = Quaternion.Slerp(packStartWorldRot, packRevealWorldRotation, t);
            pack.transform.localScale = Vector3.Lerp(packStartScale, Vector3.one * revealScale, t);
            if (backdrop != null)
                backdrop.SetFadeProgress(elapsed / RevealBackdropFadeInDuration, RevealBackdropAlpha);
            yield return null;
        }

        if (backdrop != null)
            backdrop.SetFadeProgress(1f, RevealBackdropAlpha);

        pack.transform.SetParent(revealRoot, false);
        pack.transform.localPosition = packRevealLocalStart;
        pack.ApplyRevealOpenPose(revealFaceRotation);
        pack.transform.localScale = Vector3.one * revealScale;

        Transform packTransform = pack.transform;
        Vector3 baseLocalPos = packTransform.localPosition;
        yield return AnticipationHoldRoutine(
            packTransform,
            baseLocalPos,
            revealScale,
            hand.OpenPackAnticipationHold);

        Vector3 ejectAnchorPos = baseLocalPos + new Vector3(
            0f,
            -CardDimensions.Height * revealScale * PackRevealSettleDropFactor,
            0f);
        yield return PackRevealSettleRoutine(
            packTransform,
            baseLocalPos,
            ejectAnchorPos,
            revealScale,
            duration * PackRevealSettleDurationFactor);

        // Eject cards one by one from inside the pack mouth, then spread into the reveal row.
        var revealCards = new List<WorldCard>(CardDimensions.CardsPerBoosterPack);
        var revealSparkles = new List<PackRevealCardSparkle>(CardDimensions.CardsPerBoosterPack);
        float packDriftDuration = duration * PackDriftDurationFactor;
        float packExitLocalY = -CardDimensions.Height * revealScale * PackExitDropFactor - packLowerOffset;

        yield return EjectRevealCardsRoutine(
            pack,
            camera,
            packTransform,
            revealRoot,
            revealFaceRotation,
            cardRevealScale,
            revealScale,
            ejectAnchorPos,
            packDriftDuration,
            packExitLocalY,
            revealCards,
            revealSparkles);

        float settleDuration = duration * 0.12f;
        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Flip cards one by one — page-turn from back to front.
        for (int i = 0; i < revealCards.Count; i++)
        {
            WorldCard card = revealCards[i];
            if (card == null)
                continue;

            GameSoundEffects.PlayPack(GameSoundEffects.PackId.CardRotation);

            float flipElapsed = 0f;
            while (flipElapsed < FlipDuration)
            {
                flipElapsed += Time.deltaTime;
                float t = flipElapsed / FlipDuration;
                card.SetRevealVisualFlip(t);
                yield return null;
            }

            card.SetRevealVisualFlip(1f);

            if (i < revealSparkles.Count && revealSparkles[i] != null)
                revealSparkles[i].Show();

            yield return RevealScalePulseRoutine(card.transform, cardRevealScale, FlipRevealPopPeak, FlipRevealPopDuration);

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

        yield return RevealMexicanWaveRoutine(revealCards, cardRevealScale);

        // Hold the reveal screen until the player presses F to collect cards into the hand.
        hand.SetAwaitingRevealCollect(true);
        while (!hand.ConsumeRevealCollectRequest())
            yield return null;
        hand.SetAwaitingRevealCollect(false);
        hand.SetPackOpenMovementLocked(false);

        if (backdrop != null)
        {
            yield return backdrop.FadeTo(0f, RevealBackdropFadeOutDuration);
            Object.Destroy(backdrop.gameObject);
        }

        DestroyRevealSparkles(revealSparkles);

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
        GameSaveSignals.NotifyMilestone();
    }

    /// Shrinks the five-card reveal row when the live frustum is too narrow (e.g. 1400x1920).
    /// 16:9 keeps the authored scale.
    static float FitRevealScale(Camera camera, float revealDistance, float heldScale, int cardCount)
    {
        float preferredScale = heldScale * RevealScreenScaleMultiplier;
        if (camera == null || cardCount <= 0)
            return preferredScale;

        float halfFovRad = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float frustumHeight = 2f * Mathf.Max(0.01f, revealDistance) * Mathf.Tan(halfFovRad);
        float availableWidth = frustumHeight * camera.aspect * (1f - RevealScreenMargin * 2f);
        float peakScale = Mathf.Max(RevealWavePeak, FlipRevealPopPeak);
        float widthFactor = RevealCardSpacingFactor * Mathf.Max(0, cardCount - 1) + peakScale;
        float neededWidth = CardDimensions.Width * preferredScale * widthFactor;
        float rowScale = neededWidth <= availableWidth || neededWidth <= 0.0001f
            ? preferredScale : preferredScale * (availableWidth / neededWidth);
        if (camera.aspect >= 1f)
            return rowScale;

        int rows = 1 + Mathf.CeilToInt(Mathf.Max(0, cardCount - 1) / 2f);
        float columnsFactor = cardCount > 1 ? RevealCardSpacingFactor + peakScale : peakScale;
        float widthLimit = availableWidth / (CardDimensions.Width * columnsFactor);
        float heightLimit = frustumHeight * 0.86f
            / (CardDimensions.Height * ((rows - 1) * 1.18f + peakScale));
        return Mathf.Min(rowScale * 2f, widthLimit, heightLimit);
    }

    static IEnumerator PackRevealSettleRoutine(
        Transform packTransform,
        Vector3 fromLocalPos,
        Vector3 toLocalPos,
        float packScale,
        float settleDuration)
    {
        if (packTransform == null)
            yield break;

        if (settleDuration <= 0f)
        {
            packTransform.localPosition = toLocalPos;
            packTransform.localScale = Vector3.one * packScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / settleDuration);
            packTransform.localPosition = Vector3.Lerp(fromLocalPos, toLocalPos, t);
            packTransform.localScale = Vector3.one * packScale;
            yield return null;
        }

        packTransform.localPosition = toLocalPos;
        packTransform.localScale = Vector3.one * packScale;
    }

    static IEnumerator EjectRevealCardsRoutine(
        WorldBoosterPack pack,
        Camera camera,
        Transform packTransform,
        Transform revealRoot,
        Quaternion revealFaceRotation,
        float revealScale,
        float packScale,
        Vector3 baseLocalPos,
        float packDriftDuration,
        float packExitLocalY,
        List<WorldCard> revealCards,
        List<PackRevealCardSparkle> revealSparkles)
    {
        IReadOnlyList<CardDefinition> contents = pack.RollContents(CardDimensions.CardsPerBoosterPack);
        float cardSpacing = CardDimensions.Width * revealScale * RevealCardSpacingFactor;
        float rowWidth = cardSpacing * Mathf.Max(0, contents.Count - 1);
        float halfHeight = CardDimensions.Height * revealScale * 0.5f;
        float rowBaseY = halfHeight + CardDimensions.Height * revealScale * 0.12f;
        bool portrait = camera.aspect < 1f;
        float frustumHeight = 2f * revealRoot.localPosition.z
            * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float topRowY = frustumHeight * 0.46f - revealRoot.localPosition.y
            - halfHeight * Mathf.Max(RevealWavePeak, FlipRevealPopPeak);
        float ejectStartScale = revealScale * CardEjectStartScaleFactor;
        float arcHeight = CardDimensions.Height * revealScale * CardEjectArcHeightFactor;
        Vector3 ejectStartLocal = baseLocalPos + new Vector3(
            0f,
            CardDimensions.Height * packScale * PackEjectLocalYOffsetFactor,
            CardDimensions.Thickness * packScale * PackEjectLocalZInsetFactor);

        packTransform.localPosition = baseLocalPos;
        packTransform.localScale = Vector3.one * packScale;

        GameSoundEffects.PlayPack(GameSoundEffects.PackId.PackOpen);

        for (int i = 0; i < contents.Count; i++)
        {
            float targetX = contents.Count <= 1 ? 0f : -rowWidth * 0.5f + i * cardSpacing;
            Vector3 targetLocalPos = new Vector3(targetX, rowBaseY, 0f);
            if (portrait)
            {
                // First card centered at the top, then pairs from left to right.
                int row = i == 0 ? 0 : 1 + (i - 1) / 2;
                float column = i == 0 ? 0f : ((i - 1) % 2 == 0 ? -0.5f : 0.5f);
                targetLocalPos = new Vector3(column * cardSpacing,
                    topRowY - row * CardDimensions.Height * revealScale * 1.18f, 0f);
            }

            WorldCard card = CardFactory.CreateWorldCard(
                revealRoot.TransformPoint(ejectStartLocal),
                revealRoot.rotation * revealFaceRotation,
                contents[i],
                paletteIndex: 0,
                cardName: "PackCard_" + (i + 1));

            card.BeginRevealPreview(
                revealRoot,
                ejectStartLocal,
                revealFaceRotation,
                ejectStartScale,
                showsBack: true);
            revealCards.Add(card);

            PackRevealCardSparkle sparkle = card.AttachRevealSparkle(revealScale);
            if (sparkle != null)
            {
                sparkle.gameObject.SetActive(false);
                revealSparkles.Add(sparkle);
            }

            GameSoundEffects.PlayPack(GameSoundEffects.PackId.InCardLayout);

            float cardElapsed = 0f;
            while (cardElapsed < CardEjectDuration)
            {
                cardElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, cardElapsed / CardEjectDuration);
                float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
                Vector3 pos = Vector3.Lerp(ejectStartLocal, targetLocalPos, t);
                pos.y += arc;
                card.transform.localPosition = pos;
                card.transform.localScale = Vector3.one * Mathf.Lerp(ejectStartScale, revealScale, t);
                yield return null;
            }

            card.transform.localPosition = targetLocalPos;
            card.transform.localScale = Vector3.one * revealScale;

            if (i < contents.Count - 1)
            {
                float staggerElapsed = 0f;
                while (staggerElapsed < CardEjectStagger)
                {
                    staggerElapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }

        yield return PackExitDriftRoutine(
            pack,
            packTransform,
            baseLocalPos,
            packScale,
            packDriftDuration,
            packExitLocalY);
    }

    static IEnumerator PackExitDriftRoutine(
        WorldBoosterPack pack,
        Transform packTransform,
        Vector3 baseLocalPos,
        float packScale,
        float packDriftDuration,
        float packExitLocalY)
    {
        if (pack == null || packTransform == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < packDriftDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / packDriftDuration);
            float packY = Mathf.Lerp(baseLocalPos.y, packExitLocalY, t);
            packTransform.localPosition = new Vector3(baseLocalPos.x, packY, baseLocalPos.z);
            packTransform.localScale = Vector3.one * packScale;
            yield return null;
        }

        Object.Destroy(pack.gameObject);
    }

    static IEnumerator AnticipationHoldRoutine(
        Transform packTransform,
        Vector3 baseLocalPos,
        float packScale,
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
            packTransform.localScale = Vector3.one * packScale * (1f + Mathf.Sin(elapsed * 22f) * 0.012f * t);
            yield return null;
        }

        packTransform.localPosition = baseLocalPos;
        packTransform.localScale = Vector3.one * packScale;

        if (PackPostShakePause > 0f)
            yield return new WaitForSeconds(PackPostShakePause);
    }

    static IEnumerator RevealScalePulseRoutine(
        Transform target,
        float baseScale,
        float peakMultiplier,
        float duration)
    {
        if (target == null || duration <= 0f)
            yield break;

        float peakDelta = Mathf.Max(0f, peakMultiplier - 1f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scaleMultiplier = 1f + peakDelta * Mathf.Sin(t * Mathf.PI);
            target.localScale = Vector3.one * (baseScale * scaleMultiplier);
            yield return null;
        }

        target.localScale = Vector3.one * baseScale;
    }

    static IEnumerator RevealMexicanWaveRoutine(IReadOnlyList<WorldCard> cards, float baseScale)
    {
        if (cards == null || cards.Count == 0)
            yield break;

        int count = cards.Count;
        var baseLocalPositions = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            WorldCard card = cards[i];
            baseLocalPositions[i] = card != null ? card.transform.localPosition : Vector3.zero;
        }

        float peakDelta = Mathf.Max(0f, RevealWavePeak - 1f);
        float dipAmount = CardDimensions.Height * baseScale * RevealWaveDipHeightFactor;
        float totalDuration = RevealWavePulseDuration + RevealWaveStagger * Mathf.Max(0, count - 1);
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < count; i++)
            {
                WorldCard card = cards[i];
                if (card == null)
                    continue;

                float pulseElapsed = elapsed - i * RevealWaveStagger;
                float scaleMultiplier = 1f;
                float dipT = 0f;
                if (pulseElapsed >= 0f && pulseElapsed <= RevealWavePulseDuration)
                {
                    float t = pulseElapsed / RevealWavePulseDuration;
                    dipT = Mathf.Sin(t * Mathf.PI);
                    scaleMultiplier = 1f + peakDelta * dipT;
                }

                card.transform.localScale = Vector3.one * (baseScale * scaleMultiplier);
                card.transform.localPosition = baseLocalPositions[i] + new Vector3(0f, -dipAmount * dipT, 0f);
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            WorldCard card = cards[i];
            if (card == null)
                continue;

            card.transform.localScale = Vector3.one * baseScale;
            card.transform.localPosition = baseLocalPositions[i];
        }
    }

    static void DestroyRevealSparkles(List<PackRevealCardSparkle> sparkles)
    {
        if (sparkles == null)
            return;

        for (int i = 0; i < sparkles.Count; i++)
        {
            PackRevealCardSparkle sparkle = sparkles[i];
            if (sparkle != null)
                sparkle.DestroySparkle();
        }

        sparkles.Clear();
    }
}
