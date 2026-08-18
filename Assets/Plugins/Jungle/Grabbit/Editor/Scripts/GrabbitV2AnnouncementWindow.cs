#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Grabbit
{
    /// <summary>
    /// One-time announcement shown to existing Grabbit 1 users letting them know that
    /// Grabbit 2 has launched and that they can upgrade for $6 via the Asset Store
    /// Major Upgrade. Auto-shows once per machine and stays re-openable from Tools/Grabbit.
    /// </summary>
    public class GrabbitV2AnnouncementWindow : EditorWindow
    {
        // "Already shown" flag version. Bump the suffix if you ever want to re-announce to
        // everyone (e.g. for a later "Grabbit 2.1" splash).
        private const string SeenKeyPrefix = "Grabbit.V2AnnouncementSeen.2026.";

        // The seen flag is keyed PER PROJECT, not per machine. EditorPrefs is machine-global, so a
        // plain key means a user who saw the announcement in one project would never see it again
        // in a freshly imported project. Hashing the project path makes it auto-show once per
        // project on import. (String.GetHashCode is randomised per process on modern .NET and would
        // change the key every session, so we use a stable FNV-1a hash of the path.)
        private static string SeenKey
        {
            get
            {
                uint hash = 2166136261u;
                foreach (char c in Application.dataPath)
                    hash = (hash ^ c) * 16777619u;
                return SeenKeyPrefix + hash.ToString("X8");
            }
        }

        // Grabbit 2's Asset Store listing. As a Major Upgrade, Grabbit 1 owners see the
        // $6 upgrade price on this page.
        private const string Grabbit2Url =
            "https://assetstore.unity.com/packages/tools/level-design/grabbit-2-physics-placement-collider-generation-386736";

        private const string DiscordUrl = "https://discord.gg/XEET6D6vN9";

        // Grabbit 2 logo, bundled into the Grabbit 1 package so this window can show it even
        // though the buyer has not installed Grabbit 2 yet (its folder is not present).
        private const string Grabbit2LogoPath =
            "Assets/Plugins/Jungle/Grabbit/Textures/Grabbit 2 Logo.png";

        // Grabbit brand orange, sampled from the logo (#FE6400): used for the "2", the price,
        // the section header and the solid Get-Grabbit-2 button.
        private static readonly Color Accent = new Color(0.996f, 0.392f, 0.0f);

        // Header banner gradient, matching the light backdrop used on the Grabbit 2 marketing
        // images (Marketing/Grabbit Asset Store cards): a cool near-white top (#FAFFFF) fading
        // vertically to a warm cream bottom (#F7EADE).
        private static readonly Color BannerTop = new Color(0.980f, 1.000f, 1.000f); // #FAFFFF
        private static readonly Color BannerBottom = new Color(0.969f, 0.918f, 0.871f); // #F7EADE

        // Orange as a rich-text hex, for inline colouring of the "2" and the price.
        private const string AccentHex = "#FE6400";

        private Texture2D logo;
        private Texture2D bannerTex;
        private Texture2D btnNormalTex, btnHoverTex, btnActiveTex;

        private Vector2 scroll;

        [InitializeOnLoadMethod]
        private static void PlugToUpdate()
        {
            if (EditorPrefs.GetBool(SeenKey, false))
                return;

            EditorApplication.update += CheckShow;
        }

        private static void CheckShow()
        {
            // Wait for a quiet editor and for the settings asset (styles/logos) to load.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!GrabbitStartingWindow.CurrentSettings)
                GrabbitStartingWindow.GetOrFetchSettings();

            if (!GrabbitStartingWindow.CurrentSettings)
                return; // settings not imported yet — try again next tick

            EditorApplication.update -= CheckShow;

            if (EditorPrefs.GetBool(SeenKey, false))
                return;

            EditorPrefs.SetBool(SeenKey, true); // auto-show exactly once
            ShowAnnouncement();
        }

        [MenuItem("Tools/Grabbit/What's New in Grabbit 2")]
        public static void ShowAnnouncement()
        {
            var window = GetWindow<GrabbitV2AnnouncementWindow>(true, "Grabbit 2 is here!");
            window.minSize = new Vector2(560, 560);
            window.CenterOnMainWindow(600, 660);
            window.Focus();
        }

        private void CenterOnMainWindow(float width, float height)
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            var x = main.x + (main.width - width) * 0.5f;
            var y = main.y + (main.height - height) * 0.5f;
            position = new Rect(x, y, width, height);
        }

        private void OnDisable()
        {
            foreach (var t in new[] { bannerTex, btnNormalTex, btnHoverTex, btnActiveTex })
                if (t)
                    DestroyImmediate(t);
        }

        // 1x1 solid-colour texture, cached in the given field, for flat-filled button backgrounds.
        private static Texture2D Solid(ref Texture2D slot, Color c)
        {
            if (slot)
                return slot;

            slot = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            slot.SetPixel(0, 0, c);
            slot.Apply();
            return slot;
        }

        // Two-row vertical gradient (top #353535, bottom #222222) stretched behind the header,
        // matching the Grabbit 2 welcome banner. Bilinear filtering blends the two rows.
        private Texture2D BannerTexture()
        {
            if (bannerTex)
                return bannerTex;

            bannerTex = new Texture2D(1, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            bannerTex.SetPixel(0, 0, BannerBottom);
            bannerTex.SetPixel(0, 1, BannerTop);
            bannerTex.Apply();
            return bannerTex;
        }

        private Texture2D Logo
        {
            get
            {
                if (!logo)
                    logo = AssetDatabase.LoadAssetAtPath<Texture2D>(Grabbit2LogoPath);
                return logo;
            }
        }

        public void OnGUI()
        {
            var title = new GUIStyle(EditorStyles.boldLabel)
            {
                richText = true, wordWrap = true, fontSize = 30, alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(10, 10, 6, 2)
            };
            var subtitle = new GUIStyle(EditorStyles.label)
            {
                richText = true, wordWrap = true, fontSize = 14, alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(20, 20, 0, 8)
            };
            var section = new GUIStyle(EditorStyles.boldLabel)
            {
                richText = true, fontSize = 15, margin = new RectOffset(10, 10, 8, 4)
            };
            var bullet = new GUIStyle(EditorStyles.label)
            {
                richText = true, wordWrap = true, fontSize = 13, margin = new RectOffset(14, 10, 1, 1)
            };
            var muted = new GUIStyle(EditorStyles.miniLabel)
            {
                richText = true, wordWrap = true, alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(20, 20, 4, 4)
            };
            var bigButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17, fontStyle = FontStyle.Bold, richText = true, border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(10, 10, 12, 12), margin = new RectOffset(12, 12, 8, 4)
            };
            // Flat, solid logo-orange button (a plain tint would only muddy the grey skin texture).
            bigButton.normal.background = Solid(ref btnNormalTex, Accent);
            bigButton.hover.background = Solid(ref btnHoverTex, Color.Lerp(Accent, Color.white, 0.12f));
            bigButton.active.background = Solid(ref btnActiveTex, Color.Lerp(Accent, Color.black, 0.15f));
            bigButton.focused.background = bigButton.normal.background;
            bigButton.onNormal.background = bigButton.normal.background;
            bigButton.normal.textColor = bigButton.hover.textColor = bigButton.active.textColor =
                bigButton.focused.textColor = Color.white;

            var smallButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12, padding = new RectOffset(8, 8, 8, 8), margin = new RectOffset(12, 12, 2, 4)
            };

            scroll = EditorGUILayout.BeginScrollView(scroll);

            // --- Header: Grabbit 2 logo on the light marketing banner gradient ---
            var banner = GUILayoutUtility.GetRect(0, 190, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(banner, BannerTexture(), ScaleMode.StretchToFill);
            if (Logo)
            {
                const float logoH = 150f;
                float logoW = logoH * Logo.width / Logo.height;
                var logoRect = new Rect(
                    banner.x + (banner.width - logoW) * 0.5f,
                    banner.y + (banner.height - logoH) * 0.5f,
                    logoW, logoH);
                GUI.DrawTexture(logoRect, Logo, ScaleMode.ScaleToFit, true);
            }

            GUILayout.Space(8);

            GUILayout.Label($"Grabbit <color={AccentHex}><b>2</b></color> is here!", title);
            GUILayout.Label(
                "A complete, ground-up rebuild of Grabbit: faster, better integrated into Unity, packed with new tools and features, and including the much-requested runtime colliders.",
                subtitle);

            GUILayout.Space(4);

            // --- The upgrade offer (the headline CTA) ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(6);
            GUILayout.Label($"Upgrade to Grabbit 2 for just <color={AccentHex}><b>$6</b></color>",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    richText = true, fontSize = 22, alignment = TextAnchor.MiddleCenter
                });
            GUILayout.Space(6);
            EditorGUILayout.EndVertical();

            // --- What's new ---
            GUILayout.Label($"<color={AccentHex}>What's new in 2.0</color>", section);
            GUILayout.Label(
                "<b>Five tools, one toolbar</b>: Select, Create, Place, Arrange and Scatter, all a click away in the Scene View.",
                bullet);
            GUILayout.Label(
                "<b>New Scene Tool integration</b>: a real Unity Editor tool with its own overlay, for a smoother workflow and faster load times.",
                bullet);
            GUILayout.Label(
                "<b>Built for large scenes</b>: spatial caching and object pooling keep things fast in dense levels.",
                bullet);
            GUILayout.Label(
                "<b>Collider generation, now at runtime</b>: bake your colliders to have them available in your game.",
                bullet);
            GUILayout.Label(
                "<b>Every feature you ever asked for</b>: full hotkey support, runtime collider baking, robust undo, crash and exception safety, and background generation.",
                bullet);
            GUILayout.Label(
                "<b>Crash-safe and fully undoable</b>: interrupted sessions clean themselves up, and Ctrl+Z always does what you expect.",
                bullet);
            GUILayout.Label(
                "<b>AI-ready</b>: drive Grabbit from your AI assistant through the Unity MCP bridge.",
                bullet);

            GUILayout.Space(8);

            // --- Buttons ---
            if (GUILayout.Button("Get Grabbit 2   →   $6 upgrade", bigButton))
                Application.OpenURL(Grabbit2Url);

            if (GUILayout.Button("Need more information? Ask on Discord!", smallButton))
                Application.OpenURL(DiscordUrl);

            GUILayout.Space(2);
            GUILayout.Label(
                "Grabbit 2 requires Unity 6. Still on 2022.3+? Grabbit 1 stays fully supported.",
                muted);

            GUILayout.Space(8);
            EditorGUILayout.EndScrollView();
        }
    }
}

#endif
