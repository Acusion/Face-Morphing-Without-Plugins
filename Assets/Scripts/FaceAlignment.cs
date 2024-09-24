using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System;
using OpenCVForUnity.UnityUtils;

public class FaceAlignment : MonoBehaviour
{
    [SerializeField]
    ImageSource imageSource;
    [SerializeField] Visualizer visualizer;
    [SerializeField] RawImage outputImageViewer;
    [SerializeField] float paddingTopRatio = 0.1f; // Adjust as needed
    [SerializeField] float paddingBottomRatio = 0.1f; // Adjust as needed
    [SerializeField] float paddingHorizontalRatio = 0.05f; // Adjust as needed

    [SerializeField] TextAsset faceLandmarksText;

    private const int COLOR_RGBA2RGB_VALUE = 4;

    public void AlignFace()
    {
        //Mat sourceImage = TextureToMat(face);

        var landmarksList = visualizer.GetLandmarkPoints(imageSource.image).ToList().ConvertAll(v => new Vector2(v.x, v.y));

        //List<Vector2> landmarksList = new List<Vector2>();

        //string[] lines = faceLandmarksText.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        //foreach (string line in lines)
        //{
        //    string[] parts = line.Split(',');
        //    if (parts.Length == 2)
        //    {
        //        float x = float.Parse(parts[0]);
        //        float y = float.Parse(parts[1]);
        //        landmarksList.Add(new Vector2(x, y));

        //        //Debug.Log($"Landmark: x = {x}, y = {y}");
        //    }
        //}

        // Debug: Print total landmarks detected
        Debug.Log($"Total Landmarks Detected: {landmarksList.Count}");

        // Set desiredFaceWidth and desiredFaceHeight to the same value to maintain aspect ratio
        int desiredSize = 1024; // You can adjust this value as needed

        var alignedImage = GetAlignedFace(imageSource.image, landmarksList, desiredSize, desiredSize, paddingTopRatio, paddingBottomRatio, paddingHorizontalRatio);
        //Texture2D alignedTexture = MatToTexture(alignedImage);
        outputImageViewer.texture = alignedImage;
    }

    public void AlignFace(Texture2D texture)
    {
        var landmarksList = visualizer.GetLandmarkPoints(texture).ToList().ConvertAll(v => new Vector2(v.x, v.y));
        int desiredSize = 1024; 

        var alignedImage = GetAlignedFace(texture, landmarksList, desiredSize, desiredSize, paddingTopRatio, paddingBottomRatio, paddingHorizontalRatio);
        outputImageViewer.texture = alignedImage;
    }

    public static Texture2D GetAlignedFace(
        Texture2D image,
        List<Vector2> landmarks,
        int desiredFaceWidth = 1024,
        int? desiredFaceHeight = null,
        float paddingTopRatio = 0.1f,
        float paddingBottomRatio = 0.1f,
        float paddingHorizontalRatio = 0.05f) // Example default values
    {
        if (desiredFaceHeight == null)
        {
            desiredFaceHeight = desiredFaceWidth;
        }

        // Validate landmarks count
        if (landmarks == null)
        {
            Debug.LogError("Landmarks list is null.");
            return null;
        }

        if (landmarks.Count < 367) // Ensure at least up to index 366
        {
            Debug.LogError($"Insufficient landmarks provided. Expected at least 367 landmarks, but got {landmarks.Count}.");
            return null;
        }

        // Convert Texture2D to OpenCV Mat
        Mat src = new Mat(image.height, image.width, CvType.CV_8UC3);
        Utils.texture2DToMat(image, src);

        // Convert from RGBA to RGB if necessary
        if (image.format == TextureFormat.RGBA32 || image.format == TextureFormat.ARGB32)
        {
            Imgproc.cvtColor(src, src, COLOR_RGBA2RGB_VALUE);
        }

        // Extract left and right eye landmarks
        // Assuming landmarks list is similar to Mediapipe's:
        // Left eye: indices 33 to 37 (inclusive)
        // Right eye: indices 362 to 366 (inclusive)
        List<Vector2> leftEyeLandmarks = landmarks.GetRange(33, 5);
        List<Vector2> rightEyeLandmarks = landmarks.GetRange(362, 5);

        // Calculate eye centers with Y-axis inversion
        Point leftEyeCenter = CalculateMeanPoint(leftEyeLandmarks, src.cols(), src.rows());
        Point rightEyeCenter = CalculateMeanPoint(rightEyeLandmarks, src.cols(), src.rows());

        Debug.Log($"Left Eye Center: ({leftEyeCenter.x}, {leftEyeCenter.y})");
        Debug.Log($"Right Eye Center: ({rightEyeCenter.x}, {rightEyeCenter.y})");

        // Calculate the center of the face
        Point faceCenter = new Point(
            (leftEyeCenter.x + rightEyeCenter.x) / 2.0,
            (leftEyeCenter.y + rightEyeCenter.y) / 2.0
        );

        // Calculate the scale based on eye distance
        double eyeDistance = CalculateEuclideanDistance(leftEyeCenter, rightEyeCenter);
        double desiredEyeDistance = desiredFaceWidth * 0.4; // Adjust this value to change face size in output
        double scale = desiredEyeDistance / eyeDistance;

        // Get top and bottom points of the face (forehead and chin)
        // Assuming landmark 10 is approximate forehead and 152 is approximate chin
        if (landmarks.Count < 153)
        {
            Debug.LogError($"Insufficient landmarks provided. Expected at least 153 landmarks, but got {landmarks.Count}.");
            return null;
        }

        Vector2 foreheadNorm = landmarks[10];
        Vector2 chinNorm = landmarks[152];
        Point forehead = new Point(foreheadNorm.x * src.cols(), (1 - foreheadNorm.y) * src.rows()); // Invert Y
        Point chin = new Point(chinNorm.x * src.cols(), (1 - chinNorm.y) * src.rows());           // Invert Y

        // Calculate vertical distance from forehead to chin
        double verticalDistance = CalculateEuclideanDistance(forehead, chin);

        // Add separate margins for top and bottom
        double marginTop = verticalDistance * paddingTopRatio;
        double marginBottom = verticalDistance * paddingBottomRatio;
        double totalHeight = verticalDistance + marginTop + marginBottom; // Full face height with separate margins

        // Calculate horizontal distance (distance between eyes)
        double horizontalDistance = CalculateEuclideanDistance(new Point(leftEyeCenter.x, 0), new Point(rightEyeCenter.x, 0));
        double marginHorizontal = horizontalDistance * paddingHorizontalRatio;
        double totalWidth = horizontalDistance + 2 * marginHorizontal; // Full face width with horizontal margins

        // Adjust scaling to ensure both vertical and horizontal padding are respected
        double scaleY = (desiredFaceHeight.Value * 0.6) / totalHeight; // Adjust height scaling to include full face with padding
        double scaleX = (desiredFaceWidth * 0.8) / totalWidth; // Adjust width scaling to include horizontal padding

        // Choose the smaller scale to preserve aspect ratio
        scale = Math.Min(scaleY, scaleX);

        // Calculate the translation (center the face with respect to padding)
        double tx = desiredFaceWidth / 2.0 - faceCenter.x * scale;
        double ty = desiredFaceHeight.Value / 2.0 - faceCenter.y * scale;

        // Create the transformation matrix
        Mat M = new Mat(2, 3, CvType.CV_32F);
        float[] mData = new float[]
        {
            (float)scale, 0, (float)tx,
            0, (float)scale, (float)ty
        };
        M.put(0, 0, mData);

        // Apply the affine transformation
        Mat dst = new Mat();
        Imgproc.warpAffine(
            src,
            dst,
            M,
            new Size(desiredFaceWidth, desiredFaceHeight.Value),
            Imgproc.INTER_LINEAR,
            Core.BORDER_CONSTANT, // Use 0 for BORDER_CONSTANT
            new Scalar(0, 0, 0) // Border color (black)
        );

        // Convert Mat back to Texture2D
        Texture2D output = new Texture2D(desiredFaceWidth, desiredFaceHeight.Value, TextureFormat.RGB24, false);
        Utils.matToTexture2D(dst, output);

        // Release Mats to free memory
        src.Dispose();
        M.Dispose();
        dst.Dispose();

        return output;
    }

    /// <summary>
    /// Calculates the mean point from a list of normalized landmarks with Y-axis inversion.
    /// </summary>
    /// <param name="landmarks">List of normalized landmarks (0 to 1).</param>
    /// <param name="width">Width of the image.</param>
    /// <param name="height">Height of the image.</param>
    /// <returns>Mean point as OpenCV Point.</returns>
    private static Point CalculateMeanPoint(List<Vector2> landmarks, int width, int height)
    {
        double sumX = 0.0;
        double sumY = 0.0;
        foreach (var lm in landmarks)
        {
            sumX += lm.x * width;
            sumY += (1 - lm.y) * height; // Invert Y-axis
        }
        return new Point(sumX / landmarks.Count, sumY / landmarks.Count);
    }

    /// <summary>
    /// Calculates the Euclidean distance between two points.
    /// </summary>
    /// <param name="p1">First point.</param>
    /// <param name="p2">Second point.</param>
    /// <returns>Euclidean distance as a double.</returns>
    private static double CalculateEuclideanDistance(Point p1, Point p2)
    {
        double dx = p2.x - p1.x;
        double dy = p2.y - p1.y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    //public static Texture2D GetAlignedFace(
    //    Texture2D image,
    //    List<Vector2> landmarks,
    //    int desiredFaceWidth = 1024,
    //    int? desiredFaceHeight = null,
    //    float marginRatio = 0.1f)
    //{
    //    if (desiredFaceHeight == null)
    //    {
    //        desiredFaceHeight = desiredFaceWidth;
    //    }

    //    // Validate landmarks count
    //    if (landmarks == null)
    //    {
    //        Debug.LogError("Landmarks list is null.");
    //        return null;
    //    }

    //    if (landmarks.Count < 367) // Ensure at least up to index 366
    //    {
    //        Debug.LogError($"Insufficient landmarks provided. Expected at least 367 landmarks, but got {landmarks.Count}.");
    //        return null;
    //    }

    //    // Convert Texture2D to OpenCV Mat
    //    Mat src = new Mat(image.height, image.width, CvType.CV_8UC3);
    //    Utils.texture2DToMat(image, src);

    //    // Convert from RGBA to RGB if necessary
    //    if (image.format == TextureFormat.RGBA32 || image.format == TextureFormat.ARGB32)
    //    {
    //        Imgproc.cvtColor(src, src, COLOR_RGBA2RGB_VALUE);
    //    }

    //    // Extract left and right eye landmarks
    //    // Assuming landmarks list is similar to Mediapipe's:
    //    // Left eye: indices 33 to 37 (inclusive)
    //    // Right eye: indices 362 to 366 (inclusive)
    //    List<Vector2> leftEyeLandmarks = landmarks.GetRange(33, 5);
    //    List<Vector2> rightEyeLandmarks = landmarks.GetRange(362, 5);

    //    // Calculate eye centers with Y-axis inversion
    //    Point leftEyeCenter = CalculateMeanPoint(leftEyeLandmarks, src.cols(), src.rows());
    //    Point rightEyeCenter = CalculateMeanPoint(rightEyeLandmarks, src.cols(), src.rows());

    //    Debug.Log($"Left Eye Center: ({leftEyeCenter.x}, {leftEyeCenter.y})");
    //    Debug.Log($"Right Eye Center: ({rightEyeCenter.x}, {rightEyeCenter.y})");

    //    // Optional: Print individual landmarks for debugging
    //    for (int i = 0; i < leftEyeLandmarks.Count; i++)
    //    {
    //        Vector2 lm = leftEyeLandmarks[i];
    //        Debug.Log($"Left Landmark {i + 33}: x = {lm.x}, y = {lm.y}");
    //    }

    //    // Calculate the center of the face
    //    Point faceCenter = new Point(
    //        (leftEyeCenter.x + rightEyeCenter.x) / 2.0,
    //        (leftEyeCenter.y + rightEyeCenter.y) / 2.0
    //    );

    //    // Calculate the scale based on eye distance
    //    double eyeDistance = CalculateEuclideanDistance(leftEyeCenter, rightEyeCenter);
    //    double desiredEyeDistance = desiredFaceWidth * 0.4; // Adjust this value to change face size in output
    //    double scale = desiredEyeDistance / eyeDistance;

    //    // Get top and bottom points of the face (forehead and chin)
    //    // Assuming landmark 10 is approximate forehead and 152 is approximate chin
    //    if (landmarks.Count < 153)
    //    {
    //        Debug.LogError($"Insufficient landmarks provided. Expected at least 153 landmarks, but got {landmarks.Count}.");
    //        return null;
    //    }

    //    Vector2 foreheadNorm = landmarks[10];
    //    Vector2 chinNorm = landmarks[152];
    //    Point forehead = new Point(foreheadNorm.x * src.cols(), (1 - foreheadNorm.y) * src.rows()); // Invert Y
    //    Point chin = new Point(chinNorm.x * src.cols(), (1 - chinNorm.y) * src.rows());           // Invert Y

    //    // Calculate vertical distance from forehead to chin
    //    double verticalDistance = CalculateEuclideanDistance(forehead, chin);

    //    // Add margin for forehead and chin (e.g., 10% extra height as margin)
    //    double margin = verticalDistance * marginRatio;
    //    double totalHeight = verticalDistance + 2 * margin; // Full face height with margins

    //    // Adjust scaling to ensure the full face (including forehead and chin) fits
    //    double scaleY = (desiredFaceHeight.Value * 0.6) / totalHeight; // Adjust height scaling to include full face
    //    scale = Math.Min(scale, scaleY); // Choose the smaller scale to preserve aspect ratio

    //    // Calculate the translation (center the face vertically with margins)
    //    double tx = desiredFaceWidth / 2.0 - faceCenter.x * scale;
    //    double ty = desiredFaceHeight.Value / 2.0 - ((forehead.y + chin.y) / 2.0) * scale;

    //    // Create the transformation matrix
    //    Mat M = new Mat(2, 3, CvType.CV_32F);
    //    float[] mData = new float[]
    //    {
    //        (float)scale, 0, (float)tx,
    //        0, (float)scale, (float)ty
    //    };
    //    M.put(0, 0, mData);

    //    // Apply the affine transformation
    //    Mat dst = new Mat();
    //    Imgproc.warpAffine(
    //        src,
    //        dst,
    //        M,
    //        new Size(desiredFaceWidth, desiredFaceHeight.Value),
    //        Imgproc.INTER_LINEAR,
    //        Core.BORDER_CONSTANT, // Use 0 for BORDER_CONSTANT
    //        new Scalar(0, 0, 0) // Border color (black)
    //    );

    //    // Convert Mat back to Texture2D
    //    Texture2D output = new Texture2D(desiredFaceWidth, desiredFaceHeight.Value, TextureFormat.RGB24, false);
    //    Utils.matToTexture2D(dst, output);

    //    // Release Mats to free memory
    //    src.Dispose();
    //    M.Dispose();
    //    dst.Dispose();

    //    return output;
    //}

    ///// <summary>
    ///// Calculates the mean point from a list of normalized landmarks with Y-axis inversion.
    ///// </summary>
    ///// <param name="landmarks">List of normalized landmarks (0 to 1).</param>
    ///// <param name="width">Width of the image.</param>
    ///// <param name="height">Height of the image.</param>
    ///// <returns>Mean point as OpenCV Point.</returns>
    //private static Point CalculateMeanPoint(List<Vector2> landmarks, int width, int height)
    //{
    //    Debug.Log(width + "," + height);
    //    double sumX = 0.0;
    //    double sumY = 0.0;
    //    foreach (var lm in landmarks)
    //    {
    //        sumX += lm.x * width;
    //        sumY += (1 - lm.y) * height; // Invert Y-axis
    //    }
    //    return new Point(sumX / landmarks.Count, sumY / landmarks.Count);
    //}

    ///// <summary>
    ///// Calculates the Euclidean distance between two points.
    ///// </summary>
    ///// <param name="p1">First point.</param>
    ///// <param name="p2">Second point.</param>
    ///// <returns>Euclidean distance as a double.</returns>
    //private static double CalculateEuclideanDistance(Point p1, Point p2)
    //{
    //    double dx = p2.x - p1.x;
    //    double dy = p2.y - p1.y;
    //    return Math.Sqrt(dx * dx + dy * dy);
    //}

    private Mat TextureToMat(Texture2D texture)
    {
        Mat mat = new Mat(texture.height, texture.width, CvType.CV_8UC4);
        Utils.texture2DToMat(texture, mat);
        // Convert from RGBA to RGB (since OpenCV uses BGR by default)
        Imgproc.cvtColor(mat, mat, Imgproc.COLOR_RGBA2RGB);
        return mat;
    }

    private Texture2D MatToTexture(Mat mat)
    {
        Texture2D texture = new Texture2D(mat.width(), mat.height(), TextureFormat.RGBA32, false);
        Utils.matToTexture2D(mat, texture);
        return texture;
    }
}
