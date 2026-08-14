using System;
using UnityEngine;

/// <summary>
/// Tunable colors for every card outline in the game.
/// Asset path: Resources/Settings/CardOutlineSettings
/// </summary>
[CreateAssetMenu(fileName = "CardOutlineSettings", menuName = "TCG Card Caos/Card Outline Settings")]
public class CardOutlineSettings : ScriptableObject
{
    public const string ResourcePath = "Settings/CardOutlineSettings";

    [Serializable]
    public struct Palette
    {
        public Color cardHover;
        public Color handSelection;
        public Color shelfPlacement;
        public Color shelfCorrect;
        public Color shelfIncorrect;

        public static Palette CreateDefaults()
        {
            return new Palette
            {
                cardHover = new Color(1f, 0.88f, 0.12f, 1f),
                handSelection = new Color(1f, 0.88f, 0.12f, 1f),
                shelfPlacement = new Color(1f, 0.88f, 0.12f, 1f),
                shelfCorrect = new Color(0.28f, 0.92f, 0.38f, 1f),
                shelfIncorrect = new Color(0.95f, 0.22f, 0.22f, 1f),
            };
        }
    }

    [Header("Kart etkileşimi")]
    [Tooltip("Yerdeki veya el dışındaki bir kartın üzerine gelince.")]
    [SerializeField] Color cardHover = new Color(1f, 0.88f, 0.12f, 1f);

    [Tooltip("Elde seçili kart.")]
    [SerializeField] Color handSelection = new Color(1f, 0.88f, 0.12f, 1f);

    [Header("Raf yerleştirme")]
    [Tooltip("Kart yerleştirirken dolaptaki hedef slotta.")]
    [SerializeField] Color shelfPlacement = new Color(1f, 0.88f, 0.12f, 1f);

    [Tooltip("Doğru dolaba/rafa koyunca kart üzerinde.")]
    [SerializeField] Color shelfCorrect = new Color(0.28f, 0.92f, 0.38f, 1f);

    [Tooltip("Yanlış dolaba/rafa koyunca kart üzerinde.")]
    [SerializeField] Color shelfIncorrect = new Color(0.95f, 0.22f, 0.22f, 1f);

    public Palette GetPalette()
    {
        return new Palette
        {
            cardHover = cardHover,
            handSelection = handSelection,
            shelfPlacement = shelfPlacement,
            shelfCorrect = shelfCorrect,
            shelfIncorrect = shelfIncorrect,
        };
    }

    public static Palette GetPaletteOrDefaults()
    {
        CardOutlineSettings settings = Resources.Load<CardOutlineSettings>(ResourcePath);
        return settings != null ? settings.GetPalette() : Palette.CreateDefaults();
    }
}
