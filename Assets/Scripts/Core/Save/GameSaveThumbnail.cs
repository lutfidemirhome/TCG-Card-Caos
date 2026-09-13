using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

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

        RenderTexture screenshot = null;
        RenderTexture thumbnail = null;
        Texture2D pixels = null;
        AsyncGPUReadbackRequest request = default;
        bool requestStarted = false;
        try
        {
            // Downsample on the GPU. Never read a full-resolution screen into
            // CPU memory merely to create a 256x144 save preview.
            bool captured = false;
            try
            {
                screenshot = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
                thumbnail = RenderTexture.GetTemporary(settings.ThumbnailWidth, settings.ThumbnailHeight, 0, RenderTextureFormat.ARGB32);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(screenshot);
                Graphics.Blit(screenshot, thumbnail);
                if (SystemInfo.supportsAsyncGPUReadback)
                {
                    request = AsyncGPUReadback.Request(thumbnail, 0, TextureFormat.RGBA32);
                    requestStarted = true;
                }
                captured = true;
            }
            catch (System.Exception exception)
            {
                LogCaptureFailure(exception.Message);
            }
            if (!captured)
                yield break;

            if (requestStarted)
            {
                while (!request.done)
                    yield return null;
                if (request.hasError)
                {
                    LogCaptureFailure("GPU readback failed.");
                    yield break;
                }
            }

            try
            {
                pixels = new Texture2D(settings.ThumbnailWidth, settings.ThumbnailHeight, TextureFormat.RGBA32, false);
                if (requestStarted)
                {
                    pixels.LoadRawTextureData(request.GetData<byte>());
                }
                else
                {
                    // Compatibility fallback reads only the already-small preview.
                    RenderTexture previous = RenderTexture.active;
                    try
                    {
                        RenderTexture.active = thumbnail;
                        pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0, false);
                    }
                    finally
                    {
                        RenderTexture.active = previous;
                    }
                }

                byte[] png = pixels.EncodeToPNG();
                if (png != null && png.Length > 0)
                    WriteThumbnail(slotId, png);
            }
            catch (System.Exception exception)
            {
                LogCaptureFailure(exception.Message);
            }
        }
        finally
        {
            // Cancellation/scene exit must not release a texture still in use
            // by a readback. Normal capture finishes asynchronously above.
            if (requestStarted && !request.done)
                request.WaitForCompletion();
            if (screenshot != null)
                RenderTexture.ReleaseTemporary(screenshot);
            if (thumbnail != null)
                RenderTexture.ReleaseTemporary(thumbnail);
            if (pixels != null)
                Object.Destroy(pixels);
        }
    }

    static void LogCaptureFailure(string message)
    {
        if (Debug.isDebugBuild)
            Debug.LogWarning("[Save] Thumbnail capture failed: " + message);
    }

    static void WriteThumbnail(string slotId, byte[] png)
    {
        if (!SaveFileIO.TryWriteThumbnail(slotId, png, out string error))
        {
            LogCaptureFailure(error);
            return;
        }
        if (SaveFileIO.TryLoadMetadata(slotId, out SaveSlotMetadata metadata))
        {
            metadata.thumbnailAvailable = true;
            SaveFileIO.TryWriteAtomic(SaveFileIO.GetMetaPath(slotId),
                JsonUtility.ToJson(metadata, false), out _);
        }
    }
}
