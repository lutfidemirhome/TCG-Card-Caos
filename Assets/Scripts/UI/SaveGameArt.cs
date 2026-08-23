using System.IO;
using UnityEngine;

/// <summary>
/// Save Game panel art dropped in Assets/UI/SaveGame/Art.
/// Editor play loads that folder; builds load the Resources copies.
/// </summary>
public static class SaveGameArt
{
    const string ArtFolder = "Assets/UI/SaveGame/Art/";
    const string ResourceFolder = "UI/SaveGame/";

    public static Texture EmptySlotThumbnail => Load<Texture>(ArtFolder, ResourceFolder, "image_save_game.png");

    public static Texture FilledSlotThumbnail => Load<Texture>(
        "Assets/UI/LoadGame/Art/",
        "UI/LoadGame/",
        "image_load_game.png");

    public static Sprite DeleteHint => Load<Sprite>(ArtFolder, ResourceFolder, "DeleteHint.png");

    public static Sprite Spinner => Load<Sprite>(
        "Assets/UI/Loading/Art/",
        "UI/Loading/",
        "loading_spinner_yellow.png");

    static T Load<T>(string artFolder, string resourceFolder, string fileName) where T : Object
    {
#if UNITY_EDITOR
        T fromArt = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(artFolder + fileName);
        if (fromArt != null)
            return fromArt;
#endif
        T fromResources = Resources.Load<T>(resourceFolder + Path.GetFileNameWithoutExtension(fileName));
        if (fromResources != null)
            return fromResources;

        return Resources.Load<T>(ResourceFolder + Path.GetFileNameWithoutExtension(fileName));
    }
}
