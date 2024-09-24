using DlibFaceLandmarkDetector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DlibFaceLandmarkDetector.UnityUtils;

public class FaceDetector : MonoBehaviour
{
    private FaceLandmarkDetector landmarkDetector => FaceLandmarkDetectorUtil.FaceLandmarkDetector;

    [SerializeField]
    private List<List<Vector2>> detectedLandmarkPoints = new List<List<Vector2>>(); // List to store detected landmark points

    private List<Vector2> landmarkPoints;

    public bool IsFaceDetected(Texture2D inputTexture)
    {
        // Ensure the input texture is assigned
        if (inputTexture == null)
        {
            Debug.LogError("Input Texture not assigned.");
            return false;
        }

        // Set the image for the FaceLandmarkDetector
        landmarkDetector.SetImage(inputTexture);

        // Detect faces in the input image
        List<Rect> detectedFaces = landmarkDetector.Detect();

        Debug.Log("Detected Faces: " + detectedFaces.Count);

        // Return whether any face is detected
        return detectedFaces.Count > 0;
    }

    public bool IsMoreThanOneFaceDetected(Texture2D inputTexture)
    {
        // Ensure the input texture is assigned
        if (inputTexture == null)
        {
            Debug.LogError("Input Texture not assigned.");
            return false;
        }

        // Set the image for the FaceLandmarkDetector
        landmarkDetector.SetImage(inputTexture);

        // Detect faces in the input image
        List<Rect> detectedFaces = landmarkDetector.Detect();

        // Return whether more than one face is detected
        return detectedFaces.Count > 1;
    }

    public void DetectLandmarks(Texture2D inputTexture)
    {
        // Ensure the input texture is assigned
        if (inputTexture == null)
        {
            Debug.LogError("Input Texture not assigned.");
            return;
        }

        // Set the image for the FaceLandmarkDetector
        landmarkDetector.SetImage(inputTexture);

        // Detect faces in the input image
        List<Rect> detectedFaces = landmarkDetector.Detect();

        // Check if no face is detected
        if (detectedFaces.Count == 0)
        {
            Debug.LogError("No Face Detected on the input image. Try with a different image.");
            return;
        }

        // Clear previous landmark points
        detectedLandmarkPoints.Clear();

        // Process each detected face for landmark detection
        foreach (Rect faceRect in detectedFaces)
        {
            // Detect landmark points for the current face region
            landmarkPoints = landmarkDetector.DetectLandmark(faceRect);

            // Store detected landmark points
            detectedLandmarkPoints.Add(landmarkPoints);
        }
    }

    public void AugmentPoints(Texture2D texture2D, Rect faceRect, int pointSize, Color pointColor)
    {
        if (texture2D == null)
            throw new ArgumentNullException("texture2D == null");

        // Draw points on the detected landmarks
        foreach (var point in landmarkPoints)
        {
            int pixelX = (int)point.x;
            int pixelY = (int)(texture2D.height - point.y - 1); // Invert y-coordinate

            // Draw a filled circle around the point
            for (int x = pixelX - pointSize / 2; x <= pixelX + pointSize / 2; x++)
            {
                for (int y = pixelY - pointSize / 2; y <= pixelY + pointSize / 2; y++)
                {
                    // Check if the pixel is within the bounds of the texture
                    if (x >= 0 && x < texture2D.width && y >= 0 && y < texture2D.height)
                    {
                        texture2D.SetPixel(x, y, pointColor);
                    }
                }
            }
        }

        // Apply the changes to the texture
        texture2D.Apply();
    }

    public List<Vector2> GetOneLandmarkPoints()
    {
        return landmarkPoints;
    }
}

public static class FaceLandmarkDetectorUtil
{
    static FaceLandmarkDetector landmarkDetector = null;

    public static FaceLandmarkDetector FaceLandmarkDetector
    {
        get
        {
            if (landmarkDetector == null)
            {
                Debug.Log("DLIB Initialized");
                string shapePredictorFileName = "shape_predictor_68_face_landmarks.dat";// "shape_predictor_68_face_landmarks.dat";
                string shapePredictorFilePath = Utils.getFilePath(shapePredictorFileName);
                landmarkDetector = new FaceLandmarkDetector(shapePredictorFilePath);
            }

            if (landmarkDetector == null)
            {
                Debug.LogError("FaceLandmarkDetector is not initialized.");
            }

            return landmarkDetector;
        }
    }
    public static void Unload()
    {
        landmarkDetector?.Dispose();
        landmarkDetector = null;
        Debug.Log("DLIB Unloaded");
    }
}