using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Low-res gameplay preview. Failure never invalidates the save.
/// </summary>
public static class GameSaveThumbnail
{
    public static Texture2D TryLoad(string slotId)
    {
        if (string.IsNullOrEmpty(slotId))
            return null;

        string path = SaveFileIO.GetThumbnailPath(slotId);
        if (!File.Exists(path))
            return null;

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes == null || bytes.Length == 0)
                return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!texture.LoadImage(bytes))
            {
                Object.Destroy(texture);
                return null;
            }

            return texture;
        }
        catch
        {
            return null;
        }
    }

    public static IEnumerator CaptureRoutine(string slotId, GameSaveSettings settings)
    {
        if (string.IsNullOrEmpty(slotId) || settings == null)
            yield break;

        yield return new WaitForEndOfFrame();

        Texture2D screenshot = null;
        Texture2D scaled = null;
        try
        {
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            if (screenshot == null)
                yield break;

            scaled = ScaleTexture(screenshot, settings.ThumbnailWidth, settings.ThumbnailHeight);
            byte[] png = scaled.EncodeToPNG();
            if (png == null || png.Length == 0)
                yield break;

            if (!SaveFileIO.TryWriteThumbnail(slotId, png, out string error))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[Save] Thumbnail write failed for " + slotId + ": " + error);
#endif
                yield break;
            }

            if (SaveFileIO.TryLoadMetadata(slotId, out SaveSlotMetadata metadata))
            {
                metadata.thumbnailAvailable = true;
                SaveFileIO.TryWriteAtomic(
                    SaveFileIO.GetMetaPath(slotId),
                    JsonUtility.ToJson(metadata, false),
                    out _);
            }
        }
        catch (System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[Save] Thumbnail capture failed: " + exception.Message);
#endif
        }
        finally
        {
            if (screenshot != null)
                Object.Destroy(screenshot);
            if (scaled != null)
                Object.Destroy(scaled);
        }
    }

    static Texture2D ScaleTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var result = new Texture2D(width, height, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply(false, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }
}
