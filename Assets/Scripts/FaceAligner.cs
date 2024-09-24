using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using Rect = OpenCVForUnity.CoreModule.Rect;
using OpenCVForUnity.UnityUtils;
using System;

public class FaceAligner : MonoBehaviour
{
    public int outputSize = 256;
    public float horizontalPadding = 0.1f;
    public float topPadding = 0.1f;
    public float bottomPadding = 0.1f;

    [SerializeField] Visualizer visualizer;
    [SerializeField]
    ImageSource imageSource;
    [SerializeField] RawImage outputImageViewer;

    public void AlignFace()
    {
        Mat sourceImage = TextureToMat(imageSource.image);
        Point[] landmarks = visualizer.GetLandmarkPoints68(imageSource.image).ToList().ConvertAll(v => new Point((int)(v.x * sourceImage.width()), (int)(v.y * sourceImage.height()))).ToArray();

        var alignedImage = AlignImage(sourceImage, landmarks);
        Texture2D alignedTexture = MatToTexture(alignedImage);
        outputImageViewer.texture = alignedTexture;
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

        Debug.Log($"Output image size: {outputImage.size()}             {offsetX}           {offsetY}       {aspectRatio}");

        Mat submat = outputImage.submat(new Rect(offsetX, offsetY, newWidth, newHeight));
        resizedFace.copyTo(submat);

        return outputImage;
    }

    private OpenCVForUnity.CoreModule.Rect GetFaceBoundingBox(Point[] landmarks)
    {
        double minX = landmarks.Min(p => p.x);
        double maxX = landmarks.Max(p => p.x);
        double minY = landmarks.Min(p => p.y);
        double maxY = landmarks.Max(p => p.y);

        int width = (int)(maxX - minX);
        int height = (int)(maxY - minY);

        return new OpenCVForUnity.CoreModule.Rect((int)minX, (int)minY, width, height);
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
        return texture;
    }
}
