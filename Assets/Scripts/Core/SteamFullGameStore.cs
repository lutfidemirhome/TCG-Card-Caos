using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Opens the full game Steam store (App ID 5125130), never the demo page.
/// Prefers Steam overlay; falls back to the default browser.
/// </summary>
public static class SteamFullGameStore
{
    public const uint FullGameAppId = 5125130u;
    public const string StoreUrl = "https://store.steampowered.com/app/5125130";

    const float CooldownSeconds = 2f;

    static float _nextAllowedUnscaledTime = -999f;

    public static void OpenWishlistPage()
    {
        if (Time.unscaledTime < _nextAllowedUnscaledTime)
            return;

        _nextAllowedUnscaledTime = Time.unscaledTime + CooldownSeconds;

        if (TryOpenSteamOverlay())
            return;

        Application.OpenURL(StoreUrl);
    }

    static bool TryOpenSteamOverlay()
    {
        try
        {
            if (!IsSteamOverlayUsable())
                return false;

            Type friendsType = FindType("Steamworks.SteamFriends");
            Type appIdType = FindType("Steamworks.AppId_t");
            Type flagType = FindType("Steamworks.EOverlayToStoreFlag");
            if (friendsType == null || appIdType == null)
                return false;

            object appId = CreateAppId(appIdType);
            if (appId == null)
                return false;

            object flag = flagType != null ? Enum.ToObject(flagType, 0) : null;
            MethodInfo method = friendsType.GetMethod(
                "ActivateGameOverlayToStore",
                BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                return false;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 1)
                method.Invoke(null, new[] { appId });
            else if (parameters.Length >= 2)
                method.Invoke(null, new[] { appId, flag });
            else
                return false;

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static bool IsSteamOverlayUsable()
    {
        Type managerType = FindType("SteamManager");
        if (managerType != null)
        {
            PropertyInfo initialized = managerType.GetProperty(
                "Initialized",
                BindingFlags.Public | BindingFlags.Static);
            if (initialized != null && initialized.GetValue(null) is bool managerOk && !managerOk)
                return false;
        }

        Type apiType = FindType("Steamworks.SteamAPI");
        if (apiType != null)
        {
            MethodInfo running = apiType.GetMethod(
                "IsSteamRunning",
                BindingFlags.Public | BindingFlags.Static);
            if (running != null && running.Invoke(null, null) is bool steamRunning && !steamRunning)
                return false;
        }

        Type utilsType = FindType("Steamworks.SteamUtils");
        if (utilsType != null)
        {
            MethodInfo overlay = utilsType.GetMethod(
                "IsOverlayEnabled",
                BindingFlags.Public | BindingFlags.Static);
            if (overlay != null && overlay.Invoke(null, null) is bool overlayOn && !overlayOn)
                return false;
        }

        return FindType("Steamworks.SteamFriends") != null;
    }

    static object CreateAppId(Type appIdType)
    {
        ConstructorInfo ctor = appIdType.GetConstructor(new[] { typeof(uint) });
        if (ctor != null)
            return ctor.Invoke(new object[] { FullGameAppId });

        ctor = appIdType.GetConstructor(new[] { typeof(int) });
        if (ctor != null)
            return ctor.Invoke(new object[] { (int)FullGameAppId });

        return Activator.CreateInstance(appIdType);
    }

    static Type FindType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName, false);
            if (type != null)
                return type;
        }

        return null;
    }
}
