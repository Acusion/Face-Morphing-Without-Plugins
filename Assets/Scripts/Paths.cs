using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class Paths
{
    const string _FRAMES_PATH = "Cache/frames";
    public static string FRAMES_PATH
    {
        get
        {
            string path = Path.Combine(Application.persistentDataPath, _FRAMES_PATH);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }

    const string _RESULTS_PATH = "Cache/Results";
    public static string RESULTS_PATH
    {
        get
        {
            string path = Path.Combine(Application.persistentDataPath, _RESULTS_PATH);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }

    const string _ALIGNED_IMAGES_PATH = "Cache/AlignedImages";
    public static string ALIGNED_IMAGES_PATH
    {
        get
        {
            string path = Path.Combine(Application.persistentDataPath, _ALIGNED_IMAGES_PATH);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }

    const string _WATERMARK_IMAGE_PATH = "Cache/WaterMark.png";
    public static string WATERMARK_IMAGE_PATH
    {
        get
        {
            string path = Path.Combine(Application.persistentDataPath, _WATERMARK_IMAGE_PATH);
            return path;
        }
    }
}
