using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates standalone TMP material presets cloned from a font's default material.
/// Does not modify the font asset or its default material.
/// </summary>
public static class FontMaterialPresetBuilder
{
    const string OutputFolder = "Assets/TextMesh Pro/Fonts/Materials";
    const string PrimaryFontPath = "Assets/TextMesh Pro/Fonts/Baloo2-ExtraBold SDF.asset";
    const string FontAssetPrefix = "Baloo2-ExtraBold SDF";

    struct PresetDefinition
    {
        public string FileName;
        public System.Action<Material> Configure;
    }

    static readonly PresetDefinition[] Presets =
    {
        new PresetDefinition
        {
            FileName = FontAssetPrefix + " - Outline Thin",
            Configure = ApplyOutlineThin,
        },
        new PresetDefinition
        {
            FileName = FontAssetPrefix + " - Outline Thick",
            Configure = ApplyOutlineThick,
        },
        new PresetDefinition
        {
            FileName = FontAssetPrefix + " - Drop Shadow",
            Configure = ApplyDropShadow,
        },
        new PresetDefinition
        {
            FileName = FontAssetPrefix + " - Outline Shadow",
            Configure = ApplyOutlineAndShadow,
        },
        new PresetDefinition
        {
            FileName = FontAssetPrefix + " - Soft Glow",
            Configure = ApplySoftGlow,
        },
    };

    [MenuItem("TCG Card Chaos/UI/Create Font Material Presets")]
    public static void CreatePresetsFromMenu()
    {
        if (!CreatePresets())
            return;

        EditorUtility.DisplayDialog(
            "Font Materials",
            "Created 5 TMP material presets in:\n" + OutputFolder + "\n\n"
            + "Assign them from TextMeshPro → Material Preset dropdown "
            + "(names must start with the font asset name).\n\n"
            + "Default font material was not changed.",
            "OK");
    }

    /// <summary>Batch entry point for automation.</summary>
    public static void ExecuteBatchCreate()
    {
        if (!CreatePresets())
            EditorApplication.Exit(1);

        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    public static bool CreatePresets()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrimaryFontPath);
        if (font == null || font.material == null)
        {
            Debug.LogError("[FontMaterialPresetBuilder] Font or default material not found at " + PrimaryFontPath);
            return false;
        }

        EnsureFolder(OutputFolder);

        Material template = font.material;
        int created = 0;

        for (int i = 0; i < Presets.Length; i++)
        {
            PresetDefinition preset = Presets[i];
            string assetPath = OutputFolder + "/" + preset.FileName + ".mat";

            if (AssetDatabase.LoadAssetAtPath<Material>(assetPath) != null)
            {
                Debug.Log("[FontMaterialPresetBuilder] Already exists: " + assetPath);
                continue;
            }

            Material material = new Material(template)
            {
                name = preset.FileName,
            };

            preset.Configure(material);
            AssetDatabase.CreateAsset(material, assetPath);
            created++;
            Debug.Log("[FontMaterialPresetBuilder] Created " + assetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FontMaterialPresetBuilder] Done. Created " + created + " new material(s).");
        return true;
    }

    static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        const string root = "Assets";
        string[] parts = folderPath.Split('/');
        string current = root;

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    static void DisableOutline(Material material)
    {
        material.DisableKeyword("OUTLINE_ON");
        material.SetFloat("_OutlineWidth", 0f);
        material.SetFloat("_FaceDilate", 0f);
    }

    static void DisableUnderlay(Material material)
    {
        material.DisableKeyword("UNDERLAY_ON");
        material.SetFloat("_UnderlayOffsetX", 0f);
        material.SetFloat("_UnderlayOffsetY", 0f);
        material.SetFloat("_UnderlayDilate", 0f);
        material.SetFloat("_UnderlaySoftness", 0f);
    }

    static void DisableGlow(Material material)
    {
        material.DisableKeyword("GLOW_ON");
        material.SetColor("_GlowColor", new Color(0f, 1f, 0f, 0.5f));
        material.SetFloat("_GlowOffset", 0f);
        material.SetFloat("_GlowInner", 0.05f);
        material.SetFloat("_GlowOuter", 0.05f);
        material.SetFloat("_GlowPower", 0.75f);
    }

    static void ApplyOutlineThin(Material material)
    {
        DisableUnderlay(material);
        DisableGlow(material);

        material.EnableKeyword("OUTLINE_ON");
        material.SetColor("_OutlineColor", Color.black);
        material.SetFloat("_OutlineWidth", 0.12f);
        material.SetFloat("_OutlineSoftness", 0f);
        material.SetFloat("_FaceDilate", 0.04f);
    }

    static void ApplyOutlineThick(Material material)
    {
        DisableUnderlay(material);
        DisableGlow(material);

        material.EnableKeyword("OUTLINE_ON");
        material.SetColor("_OutlineColor", Color.black);
        material.SetFloat("_OutlineWidth", 0.34f);
        material.SetFloat("_OutlineSoftness", 0f);
        material.SetFloat("_FaceDilate", 0.12f);
    }

    static void ApplyDropShadow(Material material)
    {
        DisableOutline(material);
        DisableGlow(material);

        material.EnableKeyword("UNDERLAY_ON");
        material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.65f));
        material.SetFloat("_UnderlayOffsetX", 0.08f);
        material.SetFloat("_UnderlayOffsetY", -0.28f);
        material.SetFloat("_UnderlayDilate", 0.2f);
        material.SetFloat("_UnderlaySoftness", 0.08f);
    }

    static void ApplyOutlineAndShadow(Material material)
    {
        DisableGlow(material);

        material.EnableKeyword("OUTLINE_ON");
        material.SetColor("_OutlineColor", Color.black);
        material.SetFloat("_OutlineWidth", 0.22f);
        material.SetFloat("_OutlineSoftness", 0f);
        material.SetFloat("_FaceDilate", 0.1f);

        material.EnableKeyword("UNDERLAY_ON");
        material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.85f));
        material.SetFloat("_UnderlayOffsetX", 0f);
        material.SetFloat("_UnderlayOffsetY", -0.35f);
        material.SetFloat("_UnderlayDilate", 0.55f);
        material.SetFloat("_UnderlaySoftness", 0f);
    }

    static void ApplySoftGlow(Material material)
    {
        DisableOutline(material);
        DisableUnderlay(material);

        material.EnableKeyword("GLOW_ON");
        material.SetColor("_GlowColor", new Color(1f, 0.92f, 0.35f, 0.75f));
        material.SetFloat("_GlowOffset", 0.04f);
        material.SetFloat("_GlowInner", 0.08f);
        material.SetFloat("_GlowOuter", 0.42f);
        material.SetFloat("_GlowPower", 0.85f);
    }
}
