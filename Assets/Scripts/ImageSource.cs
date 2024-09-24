using Klak.TestTools;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ImageSource : MonoBehaviour
{
    public FaceDetector fld;
    [SerializeField] Visualizer visualizer;

    [SerializeField] RectTransform prefab;
    [SerializeField] RawImage parent;

    public Texture2D image;

    [ContextMenu("DetectLandmarksDLib")]
    public void DetectLandmarksDLib()
    {
        parent.texture = image;
        parent.rectTransform.sizeDelta = new Vector2(image.width, image.height);

        fld.DetectLandmarks(image);
        int index = 0;
        foreach (var landmark in fld.GetOneLandmarkPoints())
        {
            RectTransform rect = Instantiate(prefab, parent.transform);
            rect.anchoredPosition = landmark;
            rect.GetComponentInChildren<TMP_Text>().text = $"{index}";
            index++;
        }
    }

    [ContextMenu("DetectLandmarksMediaPipe")]
    public void DetectLandmarksMediaPipe()
    {
        parent.texture = image;
        parent.rectTransform.sizeDelta = new Vector2(image.width, image.height);

        var landmarksList = visualizer.GetLandmarkPoints(image).ToList().ConvertAll(v => new Vector2(v.x, v.y));

        Debug.Log($"Total Landmarks Detected: {landmarksList.Count}");

        int index = 0;
        foreach (var landmark in landmarksList)
        {
            RectTransform rect = Instantiate(prefab, parent.transform);
            rect.anchoredPosition = new Vector2(landmark.x * image.width, landmark.y * image.height);
            rect.GetComponentInChildren<TMP_Text>().text = $"{index}";
            rect.name = $"{index}";
            index++;
        }
    }
}