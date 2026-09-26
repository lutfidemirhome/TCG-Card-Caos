#if TCG_STEAMWORKS_NET && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
using System;
using Steamworks;
using UnityEngine;

/// <summary>
/// Steamworks.NET 2025.164.1 adapter. Owns SteamAPI lifetime; do not add a second
/// SteamManager. This file deliberately lives outside TCGCardCaos.asmdef so the core game
/// stays independent of the installed SDK. The Standalone build enables TCG_STEAMWORKS_NET.
/// See Docs/SteamSkillAchievements.md for the remaining Steamworks backend configuration.
/// </summary>
public sealed class SteamSkillAchievementsProvider : MonoBehaviour
{
    const float InitializeRetrySeconds = 30f;
    const float FailureRetrySeconds = 120f;
    const float MinimumStoreInterval = 15f;
    const float CallbackTimeout = 60f;
    static SteamSkillAchievementsProvider instance;

    readonly bool[] confirmed = new bool[SteamSkillAchievements.Count];
    readonly bool[] locallySet = new bool[SteamSkillAchievements.Count];
    Callback<UserStatsStored_t> statsStored;
    Callback<UserAchievementStored_t> achievementStored;
    bool initialized, waitingForStore, warningShown, preferencesDirty;
    float nextAttempt, nextStoreAllowed, storeStarted, nextPreferencesFlush, nextPumpAllowed;
    int highestRows;
    ulong steamUser;
    string progressKey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        // Unity preview, including P testing, never changes a developer's real Steam account.
        if (Application.isEditor || !SteamSkillAchievements.IsAvailableForBuild || instance) return;
        var owner = new GameObject("Steam Skill Achievements");
        DontDestroyOnLoad(owner);
        instance = owner.AddComponent<SteamSkillAchievementsProvider>();
    }

    void Awake()
    {
        if (Application.isEditor || !SteamSkillAchievements.IsAvailableForBuild)
        {
            enabled = false;
            return;
        }
        highestRows = SteamSkillAchievements.HighestCompletedRows;
        SteamSkillAchievements.ProgressChanged += OnProgress;
    }

    void OnProgress(int rows)
    {
        try
        {
            if (initialized && !AccountMatches()) return;
            highestRows = Mathf.Max(highestRows, rows);
            RememberProgress();
            // Batch rows completed together; a failed upload retains its retry backoff.
            nextAttempt = Mathf.Max(Time.realtimeSinceStartup + 2f, nextStoreAllowed);
        }
        catch (Exception exception)
        {
            // This handler is invoked by the gameplay/save path, which must remain independent.
            nextAttempt = nextStoreAllowed = Time.realtimeSinceStartup + FailureRetrySeconds;
            WarnOnce("Steam achievement sync will retry: " + exception.Message);
        }
    }

    void Update()
    {
        float now = Time.realtimeSinceStartup;
        if (now < nextPumpAllowed) return;
        try
        {
            if (!initialized)
            {
                if (now < nextAttempt) return;
                nextAttempt = now + InitializeRetrySeconds;
                TryInitialize();
                return;
            }

            // Required Steam callback pump; no scene scans, reflection or achievement loops per frame.
            SteamAPI.RunCallbacks();
            if (preferencesDirty && now >= nextPreferencesFlush) FlushPreferences();
            if (waitingForStore)
            {
                if (now - storeStarted < CallbackTimeout) return;
                // No acknowledgement: keep every unconfirmed achievement and retry later.
                waitingForStore = false;
                nextAttempt = nextStoreAllowed = now + FailureRetrySeconds;
            }
            if (now < nextAttempt) return;
            nextAttempt = now + FailureRetrySeconds;
            SyncAchievements();
        }
        catch (Exception exception)
        {
            // Steam availability must never interrupt card placement, saving, or loading.
            nextAttempt = nextStoreAllowed = now + FailureRetrySeconds;
            nextPumpAllowed = now + InitializeRetrySeconds;
            waitingForStore = false;
            if (initialized && (statsStored == null || achievementStored == null))
            {
                statsStored?.Dispose();
                achievementStored?.Dispose();
                statsStored = null;
                achievementStored = null;
                initialized = false;
                try { SteamAPI.Shutdown(); } catch (Exception) { }
            }
            WarnOnce("Steam achievement sync will retry: " + exception.Message);
        }
    }

    void TryInitialize()
    {
        if (!SteamAPI.Init()) return; // Steam may start later; continue the game without it.
        initialized = true;
        if (SteamUtils.GetAppID().m_AppId != SteamFullGameStore.FullGameAppId)
        {
            WarnOnce("Steam achievements are disabled: the running App ID is not the full game's App ID.");
            SteamAPI.Shutdown();
            initialized = false;
            enabled = false;
            return;
        }
        steamUser = SteamUser.GetSteamID().m_SteamID;
        if (steamUser == 0)
        {
            SteamAPI.Shutdown();
            initialized = false;
            return;
        }

        // This is an earned-progress journal, NOT a claim that Steam has stored anything.
        // Keeping it per account prevents another Steam user's pending unlocks leaking across accounts.
        progressKey = "SteamSkillRows." + SteamFullGameStore.FullGameAppId + "." + steamUser;
        highestRows = Mathf.Clamp(Mathf.Max(highestRows, PlayerPrefs.GetInt(progressKey, 0)),
            0, SteamSkillAchievements.RequiredRows(SteamSkillAchievements.Count - 1));
        RememberProgress();
        statsStored = Callback<UserStatsStored_t>.Create(OnStatsStored);
        achievementStored = Callback<UserAchievementStored_t>.Create(OnAchievementStored);
        // Steamworks.NET 2025.164.1 uses the stats preloaded by Steam at launch;
        // its SDK no longer exposes the obsolete RequestCurrentStats method.
        nextAttempt = Time.realtimeSinceStartup + 2f;
    }

    void SyncAchievements()
    {
        // Steam account switching requires restarting this integration, never transferring its queue.
        if (!AccountMatches()) return;
        bool haveChanges = false;
        for (int i = 0; i < SteamSkillAchievements.Count; i++)
        {
            if (SteamSkillAchievements.RequiredRows(i) > highestRows || confirmed[i]) continue;
            string id = SteamSkillAchievements.ApiName(i);
            if (!SteamUserStats.GetAchievement(id, out bool achieved))
            {
                WarnOnce("Steam achievement definitions are unavailable. Check the published API names; sync will retry.");
                continue;
            }
            // A value we set this session is only in Steam's memory until acknowledged.
            if (achieved && !locallySet[i]) { confirmed[i] = true; continue; }
            if (!SteamUserStats.SetAchievement(id)) continue;
            locallySet[i] = true;
            haveChanges = true;
        }
        if (!haveChanges) return;
        float now = Time.realtimeSinceStartup;
        if (!SteamUserStats.StoreStats())
        {
            nextAttempt = nextStoreAllowed = now + FailureRetrySeconds;
            return;
        }
        waitingForStore = true;
        storeStarted = now;
        nextStoreAllowed = now + MinimumStoreInterval;
    }

    void OnStatsStored(UserStatsStored_t result)
    {
        if (result.m_nGameID != SteamFullGameStore.FullGameAppId || !AccountMatches()) return;
        waitingForStore = false;
        if (result.m_eResult != EResult.k_EResultOK)
        {
            // Keep locallySet flags: GetAchievement(true) after a failed store is not proof of upload.
            nextAttempt = nextStoreAllowed = Time.realtimeSinceStartup + FailureRetrySeconds;
            WarnOnce("Steam could not store achievements yet (" + result.m_eResult + "); sync will retry.");
            return;
        }
        // Per-achievement callbacks below identify exactly what was committed, including late replies.
        // Unacknowledged IDs remain retryable; an unrelated successful stats callback clears nothing.
        nextAttempt = Mathf.Max(Time.realtimeSinceStartup + 2f, nextStoreAllowed);
    }

    void OnAchievementStored(UserAchievementStored_t result)
    {
        if (result.m_nGameID != SteamFullGameStore.FullGameAppId || result.m_nCurProgress != 0 || result.m_nMaxProgress != 0 || !AccountMatches()) return;
        for (int i = 0; i < SteamSkillAchievements.Count; i++)
        {
            if (!string.Equals(result.m_rgchAchievementName, SteamSkillAchievements.ApiName(i), StringComparison.Ordinal)) continue;
            confirmed[i] = true;
            locallySet[i] = false;
            break;
        }
    }

    bool AccountMatches() => initialized && SteamUser.GetSteamID().m_SteamID == steamUser;

    void RememberProgress()
    {
        if (string.IsNullOrEmpty(progressKey) || PlayerPrefs.GetInt(progressKey, 0) >= highestRows) return;
        PlayerPrefs.SetInt(progressKey, highestRows);
        preferencesDirty = true;
        nextPreferencesFlush = Time.realtimeSinceStartup + 15f;
    }

    void FlushPreferences()
    {
        if (!preferencesDirty) return;
        PlayerPrefs.Save();
        preferencesDirty = false;
    }

    void OnApplicationPause(bool paused) { if (paused) FlushPreferences(); }
    void OnApplicationQuit() => FlushPreferences();

    void OnDestroy()
    {
        SteamSkillAchievements.ProgressChanged -= OnProgress;
        FlushPreferences();
        statsStored?.Dispose();
        achievementStored?.Dispose();
        if (initialized) SteamAPI.Shutdown();
        if (instance == this) instance = null;
    }

    void WarnOnce(string message)
    {
        if (warningShown) return;
        warningShown = true;
        Debug.LogWarning("[Steam Skills] " + message);
    }
}
#endif
