using FFmpegUnityBind2;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

public class VideoCreator : MonoBehaviour, IFFmpegCallbacksHandler
{
    int frameCount = 0;

    public Vector2 watermarkImagePosition = new Vector2(0, 0);
    public float watermarkImageScale = 0.5f;
    [SerializeField] int frameRate = 20;
    [SerializeField] int videoSpeed = 1;
    public List<IFFmpegCallbacksHandler> _callbacksHandlers;
    public Texture2D WaterMarkImage;

    [SerializeField] VideoPlayer videoPlayer;

    string videoFileFullPath => Path.Combine(Paths.RESULTS_PATH, "MorphingOutput.mp4");

    private void Awake()
    {
        videoPlayer.targetTexture.Release();
    }

    private void Start()
    {
        _callbacksHandlers = new List<IFFmpegCallbacksHandler> { this };
    }

    public void OnFrameCreated(byte[] imageData)
    {
        string framePath = Path.Combine(Paths.FRAMES_PATH, $"frame_{frameCount.ToString("d4")}.png");
        File.WriteAllBytes(framePath, imageData);
        frameCount++;
    }

    bool IsValidPng(string filePath)
    {
        byte[] header = new byte[8];
        using (var fileStream = System.IO.File.OpenRead(filePath))
        {
            fileStream.Read(header, 0, 8);
        }

        // Check PNG signature
        return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E &&
               header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A &&
               header[6] == 0x1A && header[7] == 0x0A;
    }

    [ContextMenu("Create Video")]
    public void OnMorphongCompleted()
    {
        string framePathFormat = Path.Combine(Paths.FRAMES_PATH, "frame_%04d.png");

        if (!File.Exists(Paths.WATERMARK_IMAGE_PATH))
            SaveImageToPersistentPath(WaterMarkImage, Paths.WATERMARK_IMAGE_PATH);

        var imagesToVideoCommandWatermark = new ImagesToVideoWithWatermark(framePathFormat, Paths.WATERMARK_IMAGE_PATH, videoFileFullPath, frameRate, CRF.MAX_QUALITY, watermarkImagePosition, watermarkImageScale, videoSpeed);
        FFmpeg.Execute(imagesToVideoCommandWatermark.ToString(), _callbacksHandlers);
    }

    public void OnStart(long executionId)
    {
        Debug.Log($"On Start. Execution Id: {executionId}");
    }

    public void OnLog(long executionId, string message)
    {
        Debug.Log($"On Log. Execution Id: {executionId}. Message: {message}");
    }

    public void OnWarning(long executionId, string message)
    {
        Debug.LogWarning($"On Warning. Execution Id: {executionId}. Message: {message}");
    }

    public void OnError(long executionId, string message)
    {
        Debug.LogError($"On Error. Execution Id: {executionId}. Message: {message}");
    }

    public void OnCanceled(long executionId)
    {
        Debug.Log($"On Canceled. Execution Id: {executionId}");
    }

    public void OnFail(long executionId)
    {
        Debug.LogError($"On Fail. Execution Id: {executionId}");
    }

    public void OnSuccess(long executionId)
    {
        Debug.Log($"On Success. Execution Id: {executionId}");
        Debug.Log("completed");

        videoPlayer.url = videoFileFullPath;
        videoPlayer.Play();
    }

    public void Save()
    {
        if (File.Exists(videoFileFullPath))
            DownloadOrSaveVideo(videoFileFullPath);
    }

    public void DownloadOrSaveVideo(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            Debug.Log("File Loaded Successfully");

#if UNITY_ANDROID
                    new NativeShare().AddFile(fullPath)
                                     .SetCallback((result, shareTarget) => UnityEngine.Debug.Log("Share result: " + result + ", selected app: " + shareTarget))
                                     .Share();
#endif
#if UNITY_IOS

            new NativeShare().AddFile(fullPath)
                                 .SetCallback((result, shareTarget) => UnityEngine.Debug.Log("Share result: " + result + ", selected app: " + shareTarget))
                                 .Share();
#endif

        }
        else
        {
            Debug.Log("No video file exist at given path" + fullPath);
        }
        //  ..........................Sharing treatment plan via native Share End.................................

    }

    private void SaveImageToPersistentPath(Texture2D imageToSave, string path)
    {
        if (imageToSave == null)
        {
            Debug.LogError("Image to save is not assigned!");
            return;
        }

        // Convert the Texture2D to a byte array
        byte[] bytes = imageToSave.EncodeToPNG();

        // Write the bytes to the file
        File.WriteAllBytes(path, bytes);
    }
}