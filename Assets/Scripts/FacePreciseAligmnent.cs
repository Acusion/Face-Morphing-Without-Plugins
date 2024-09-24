using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using OpenCVForUnity;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgcodecsModule;
using OpenCVForUnity.ImgprocModule;
using Rect = OpenCVForUnity.CoreModule.Rect;
using OpenCVForUnity.UnityUtils;
using Debug = UnityEngine.Debug;
using UnityEngine.Events;
using DlibFaceLandmarkDetector;
using System.Linq;
using UnityEngine.UI;

public class FacePreciseAlignment : MonoBehaviour
{
    //public string inputDirectory; // Directory containing input images
    //public string outputDirectory; // Directory to save processed images

    [SerializeField]
    ImageSource imageSource;

    public FaceDetector fld;
    //private List<Vector2> facelandmarks;

    // Exposed parameters
    public int outputSize = 1024;

    // New parameters for face coverage adjustment
    public float topPadding = 0.7f; // Fraction of face height to include above the hairline
    public float bottomPadding = 0.5f; // Fraction of face height to include below the chin
    public float horizontalPadding = 0.5f; // Fraction of face width to include on the sides
    [SerializeField] private List<Texture2D> createdTextures = new List<Texture2D>();

    [SerializeField] RawImage outputImageViewer;
    [SerializeField] bool useOpenCV;

    void Start()
    {
       
    }

    [ContextMenu("ProcessAllImages")]
    public void ProcessAllImages()
    {
        ProcessAlignedImage(imageSource.image);
    }

    public string GetAlignedImage(Texture2D imageTexture)
    {
        if (imageTexture == null)
        {
            Debug.LogError("Failed to load image.");
            return "";
        }

        return ProcessAlignedImage(imageTexture);
    }

    private string ProcessAlignedImage(Texture2D sourceImageTexture, string originalImagePath = "")
    {
        if (!fld.IsFaceDetected(sourceImageTexture))
        {
            Debug.LogError("No Face Detected on the input image. Try with a different image.");
            Cleanup(sourceImageTexture, originalImagePath);

            return "";
        }

        if (fld.IsMoreThanOneFaceDetected(sourceImageTexture))
        {
            Debug.LogError("Multiple faces detected in the input image. Please try using a different image that contains only one face");
            Cleanup(sourceImageTexture, originalImagePath);

            return "";
        }
        Mat sourceImage = TextureToMat(sourceImageTexture);

        fld.DetectLandmarks(sourceImageTexture);
       var facelandmarks = fld.GetOneLandmarkPoints();

        Debug.Log("Landmarks detected: " + facelandmarks.Count);
        Point[] sourceLandmarksPoints = facelandmarks.ConvertAll(v => new Point((int)v.x, (int)v.y)).ToArray();

        // Align images
        Mat alignedImage = AlignImage(sourceImage, sourceLandmarksPoints);

        string outputDirectory = Paths.ALIGNED_IMAGES_PATH;

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
        else
        {
            string[] existingFiles = Directory.GetFiles(outputDirectory);
            foreach (string file in existingFiles)
            {
                File.Delete(file);
            }
        }

        // Convert aligned image to Texture2D and save it
        Texture2D alignedTexture = MatToTexture(alignedImage);

        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        string fileName = !string.IsNullOrEmpty(originalImagePath) ?
            Path.GetFileNameWithoutExtension(originalImagePath) + "_Aligned_" + timestamp + ".png" :
            "_Aligned_" + timestamp + ".png";

        string dstFile = Path.Combine(outputDirectory, fileName);
        SaveImage(alignedTexture, dstFile);
        Debug.Log("Aligned images and saved to: " + dstFile);

        //DestroyImmediate(sourceImageTexture);
        //DestroyImmediate(alignedTexture);
        FaceLandmarkDetectorUtil.Unload();

        outputImageViewer.texture = alignedTexture;

        return dstFile;
    }

    private Texture2D LoadTexture(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        texture.LoadImage(fileData, false);
        EnsureTextureSettings(texture);
        createdTextures.Add(texture); // Track the created texture
        return texture;
    }

    private void EnsureTextureSettings(Texture2D texture)
    {
        if (!texture.isReadable)
        {
            Debug.LogError("Source texture must be readable.");
        }
        if (texture.mipmapCount > 1)
        {
            Debug.LogError("Source texture should not have mipmaps enabled.");
        }
    }

    public Mat AlignImage(Mat sourceImage, Point[] landmarks)
    {
        // Calculate average points for eyes and mouth
        Point eyeLeft = AveragePoints(landmarks, 36, 41);
        Point eyeRight = AveragePoints(landmarks, 42, 47);
        Point eyeAvg = new Point((eyeLeft.x + eyeRight.x) * 0.5f, (eyeLeft.y + eyeRight.y) * 0.5f);

        Point mouthLeft = landmarks[48];
        Point mouthRight = landmarks[54];
        Point mouthAvg = new Point((mouthLeft.x + mouthRight.x) * 0.5f, (mouthLeft.y + mouthRight.y) * 0.5f);

        // Calculate center of the face
        Point faceCenter = new Point((eyeAvg.x + mouthAvg.x) * 0.5f, (eyeAvg.y + mouthAvg.y) * 0.5f);

        // Calculate the angle of rotation
        double dx = eyeRight.x - eyeLeft.x;
        double dy = eyeRight.y - eyeLeft.y;
        double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);

        // Get the rotation matrix
        Mat rotationMatrix = new Mat();
        rotationMatrix = Imgproc.getRotationMatrix2D(faceCenter, angle, 1.0);

        // Rotate the image
        Mat rotatedImage = new Mat();
        Imgproc.warpAffine(sourceImage, rotatedImage, rotationMatrix, new Size(sourceImage.cols(), sourceImage.rows()), Imgproc.INTER_LINEAR);

        // Apply padding and cropping
        Rect faceRect = GetFaceBoundingBox(landmarks);
        int faceWidth = faceRect.width;
        int faceHeight = faceRect.height;
        int paddingX = (int)(faceWidth * horizontalPadding);
        int paddingYTop = (int)(faceHeight * topPadding);
        int paddingYBottom = (int)(faceHeight * bottomPadding);

        int outputX = Math.Max(0, faceRect.x - paddingX);
        int outputY = Math.Max(0, faceRect.y - paddingYTop);
        int outputWidth = Math.Min(rotatedImage.cols() - outputX, faceWidth + 2 * paddingX);
        int outputHeight = Math.Min(rotatedImage.rows() - outputY, faceHeight + paddingYTop + paddingYBottom);

        // Use a single Mat for cropping and resizing
        Mat croppedFace = new Mat(rotatedImage, new Rect(outputX, outputY, outputWidth, outputHeight));

        // Maintain aspect ratio during resizing
        float aspectRatio = (float)croppedFace.cols() / croppedFace.rows();
        int newWidth, newHeight;

        if (aspectRatio > 1) // Wider than tall
        {
            newWidth = outputSize;
            newHeight = Mathf.RoundToInt(outputSize / aspectRatio);
        }
        else // Taller than wide
        {
            newWidth = Mathf.RoundToInt(outputSize * aspectRatio);
            newHeight = outputSize;
        }

        Mat resizedFace = new Mat();
        Imgproc.resize(croppedFace, resizedFace, new Size(newWidth, newHeight), 0, 0, Imgproc.INTER_LINEAR);

        // Center the resized face in the output image
        Mat outputImage = new Mat(new Size(outputSize, outputSize), rotatedImage.type(), new Scalar(0, 0, 0));
        int offsetX = (outputSize - newWidth) / 2;
        int offsetY = (outputSize - newHeight) / 2;

        Debug.Log($"Output image size: {outputImage.size()}             {offsetX}           {offsetY}");

        Mat submat = outputImage.submat(new Rect(offsetX, offsetY, newWidth, newHeight));
        resizedFace.copyTo(submat);

        return outputImage;
    }


    private Rect GetFaceBoundingBox(Point[] landmarks)
    {
        double minX = landmarks.Min(p => p.x);
        double maxX = landmarks.Max(p => p.x);
        double minY = landmarks.Min(p => p.y);
        double maxY = landmarks.Max(p => p.y);

        int width = (int)(maxX - minX);
        int height = (int)(maxY - minY);

        return new Rect((int)minX, (int)minY, width, height);
    }

    private Point AveragePoints(Point[] points, int start, int end)
    {
        double xSum = 0;
        double ySum = 0;
        int count = end - start + 1;

        for (int i = start; i <= end; i++)
        {
            xSum += points[i].x;
            ySum += points[i].y;
        }

        return new Point(xSum / count, ySum / count);
    }

    private Mat AlignAndCropImage(Mat sourceImage, Vector4[] burracudaLandmarks, int imageWidth, int imageHeight, int outputSize = 1080)
    {
        // Convert Barracuda landmarks to pixel coordinates if they are normalized
        Point[] pixelLandmarks = new Point[Visualizer.BurracudaToDlib68Indices.Length];
        for (int i = 0; i < Visualizer.BurracudaToDlib68Indices.Length; i++)
        {
            // Get the index from our mapping
            int burracudaIndex = Visualizer.BurracudaToDlib68Indices[i];

            // Assuming landmarks are normalized (0 to 1)
            float x = burracudaLandmarks[burracudaIndex].x * imageWidth;
            float y = burracudaLandmarks[burracudaIndex].y * imageHeight;
            pixelLandmarks[i] = new Point(x, y);
        }

        // Calculate average points for eyes and mouth using the updated indices
        Point eyeLeft = AveragePoints(pixelLandmarks, 36, 41);  // Adjusted indices for left eye
        Point eyeRight = AveragePoints(pixelLandmarks, 42, 47); // Adjusted indices for right eye
        Point eyeAvg = new Point((eyeLeft.x + eyeRight.x) * 0.5f, (eyeLeft.y + eyeRight.y) * 0.5f);

        Point mouthLeft = pixelLandmarks[48];  // Adjusted index for left mouth corner
        Point mouthRight = pixelLandmarks[54]; // Adjusted index for right mouth corner
        Point mouthAvg = new Point((mouthLeft.x + mouthRight.x) * 0.5f, (mouthLeft.y + mouthRight.y) * 0.5f);

        // Calculate center of the face
        Point faceCenter = new Point((eyeAvg.x + mouthAvg.x) * 0.5f, (eyeAvg.y + mouthAvg.y) * 0.5f);

        // Calculate the angle of rotation
        double dx = eyeRight.x - eyeLeft.x;
        double dy = eyeRight.y - eyeLeft.y;
        double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);

        // Get the rotation matrix
        Mat rotationMatrix = Imgproc.getRotationMatrix2D(faceCenter, angle, 1.0);

        // Rotate the image
        Mat rotatedImage = new Mat();
        Imgproc.warpAffine(sourceImage, rotatedImage, rotationMatrix, new Size(sourceImage.cols(), sourceImage.rows()), Imgproc.INTER_LINEAR);

        // Calculate a tight bounding box around the face
        Rect faceRect = GetFaceBoundingBox(pixelLandmarks);

        // Define padding (optional, if you want to add some space around the face)
        float paddingRatio = 0.2f; // Adjust this value to add padding around the face
        int paddingX = (int)(faceRect.width * paddingRatio);
        int paddingY = (int)(faceRect.height * paddingRatio);

        // Adjust bounding box with padding
        int outputX = Math.Max(0, faceRect.x - paddingX);
        int outputY = Math.Max(0, faceRect.y - paddingY);
        int outputWidth = Math.Min(rotatedImage.cols() - outputX, faceRect.width + 2 * paddingX);
        int outputHeight = Math.Min(rotatedImage.rows() - outputY, faceRect.height + 2 * paddingY);

        // Crop the face from the rotated image
        Mat croppedFace = new Mat(rotatedImage, new Rect(outputX, outputY, outputWidth, outputHeight));

        // Determine the aspect ratio of the cropped face
        float aspectRatio = (float)outputWidth / outputHeight;

        // Resize the cropped face to fit within the output size while maintaining aspect ratio
        int newWidth, newHeight;
        if (aspectRatio > 1) // Wider than tall
        {
            newWidth = outputSize;
            newHeight = Mathf.RoundToInt(outputSize / aspectRatio);
        }
        else // Taller than wide
        {
            newWidth = Mathf.RoundToInt(outputSize * aspectRatio);
            newHeight = outputSize;
        }

        Mat resizedFace = new Mat();
        Imgproc.resize(croppedFace, resizedFace, new Size(newWidth, newHeight), 0, 0, Imgproc.INTER_LINEAR);

        // Create a new 1080x1080 canvas and center the resized face on it
        Mat outputImage = new Mat(new Size(outputSize, outputSize), rotatedImage.type(), new Scalar(0, 0, 0));
        int offsetX = (outputSize - newWidth) / 2;
        int offsetY = (outputSize - newHeight) / 2;

        Mat submat = outputImage.submat(new Rect(offsetX, offsetY, newWidth, newHeight));
        resizedFace.copyTo(submat);

        return outputImage;
    }

    private Mat TextureToMat(Texture2D texture)
    {
        Mat mat = new Mat(texture.height, texture.width, CvType.CV_8UC4);
        Utils.texture2DToMat(texture, mat);
        Imgproc.cvtColor(mat, mat, Imgproc.COLOR_RGBA2RGB);
        return mat;
    }

    private Texture2D MatToTexture(Mat mat)
    {
        Texture2D texture = new Texture2D(mat.width(), mat.height(), TextureFormat.RGBA32, mipChain: false);
        Utils.matToTexture2D(mat, texture);
        createdTextures.Add(texture); // Track the created texture
        return texture;
    }

    private void SaveImage(Texture2D texture, string filePath)
    {
        byte[] imageData = texture.EncodeToPNG();
        File.WriteAllBytes(filePath, imageData);
    }

    // Cleanup method for texture and file
    private void Cleanup(Texture2D texture, string filePath)
    {
        //DestroyImmediate(texture);
        //if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        //{
        //    File.Delete(filePath);
        //}
    }

    // Method to clear all created textures
    public void ClearTextures()
    {
        //foreach (var texture in createdTextures)
        //{
        //    DestroyImmediate(texture);
        //}
        //createdTextures.Clear();
        //Debug.Log("All textures cleared.");
    }
}
