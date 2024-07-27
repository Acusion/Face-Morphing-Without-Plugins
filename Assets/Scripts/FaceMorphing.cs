using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using OpenCVForUnity;
//using FFmpegOut;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgcodecsModule;
using OpenCVForUnity.ImgprocModule;
using Rect = OpenCVForUnity.CoreModule.Rect;
using OpenCVForUnity.UnityUtils;
using Debug = UnityEngine.Debug;
using UnityEngine.Events;
using DlibFaceLandmarkDetector;

public class FaceMorphing : MonoBehaviour
{
    [SerializeField] List<Texture2D> inputImages;
    [SerializeField] UnityEvent<byte[]> onFrameCreated;
    [SerializeField] UnityEvent onMorphingCompleted;

    public string shapePredictorPath = "shape_predictor_68_face_landmarks.dat";
    private FaceLandmarkDetector faceLandmarkDetector;

    private void Start()
    {
        Debug.Log(Application.persistentDataPath);
    }

    private bool InitializeFaceLandmarkDetector()
    {
        string predictorPath = Utils.getFilePath(shapePredictorPath);
        if (string.IsNullOrEmpty(predictorPath))
        {
            Debug.LogError("Shape predictor path is invalid or file does not exist.");
            return false;
        }

        faceLandmarkDetector = new FaceLandmarkDetector(predictorPath);

        if (faceLandmarkDetector == null)
        {
            Debug.LogError("Failed to initialize FaceLandmarkDetector.");
            return false;
        }

        Debug.Log("FaceLandmarkDetector initialized successfully.");
        return true;
    }


    #region Morphing from Texture2D 
    //Will be called from scene button.
    public void DoMorphingTextures2D()
    {
        if (!InitializeFaceLandmarkDetector())
        {
            Debug.LogError("Failed to initialize FaceLandmarkDetector.");
            return;
        }

        for (int i = 0; i < inputImages.Count - 1; i++)
        {
            Texture2D path1 = inputImages[i];
            Texture2D path2 = inputImages[i + 1];

            Debug.Log($"Morphing between {path1} and {path2}");

            DoMorphing(path1, path2);
        }

        onMorphingCompleted.Invoke();
    }

    private void DoMorphing(Texture2D path1, Texture2D path2)
    {
        FaceCorrespondences faceCorrespondences = new FaceCorrespondences();

        Mat imgMat1 = new Mat(path1.height, path1.width, CvType.CV_8UC3);
        Utils.texture2DToMat(path1, imgMat1);
        Imgproc.cvtColor(imgMat1, imgMat1, Imgproc.COLOR_BGR2RGB);

        Mat imgMat2 = new Mat(path2.height, path2.width, CvType.CV_8UC3);
        Utils.texture2DToMat(path2, imgMat2);
        Imgproc.cvtColor(imgMat2, imgMat2, Imgproc.COLOR_BGR2RGB);

        (Size size, Mat img1, Mat img2, List<Point> points1, List<Point> points2, List<Point> triList) = faceCorrespondences.GenerateFaceCorrespondences(imgMat1, imgMat2, faceLandmarkDetector);

        var tri = new DelaunayTriangulation().MakeDelaunay((int)size.width, (int)size.height, triList, img1, img2);
        //var tri = new DelaunayTriangulation().MakeDelaunay(jsonDelaunay.text);

        GenerateMorphSequence(5, 20, img1, img2, points1, points2, tri, size);
    }
    #endregion

    #region Morphing From Paths
    [ContextMenu("DoMorphing")]
    public void DoMorphing()
    {
        if (!InitializeFaceLandmarkDetector())
        {
            Debug.LogError("Failed to initialize FaceLandmarkDetector.");
            return;
        }

        var framesDirectory = Path.Combine(Application.persistentDataPath, "PatientImages", "PatientId", "Temp", "frames");
        framesDirectory.ResetDirectory();

        string[] imagePaths = Directory.GetFiles(Paths.ALIGNED_IMAGES_PATH, "*.png");

        for (int i = 0; i < imagePaths.Length - 1; i++)
        {
            string path1 = imagePaths[i];
            string path2 = imagePaths[i + 1];

            Debug.Log($"Morphing between {path1} and {path2}");

            DoMorphing(path1, path2);
        }

        onMorphingCompleted.Invoke();
    }

    private void DoMorphing(string path1, string path2)
    {
        FaceCorrespondences faceCorrespondences = new FaceCorrespondences();

        Mat imgMat1 = Imgcodecs.imread(path1);
        Mat imgMat2 = Imgcodecs.imread(path2);

        (Size size, Mat img1, Mat img2, List<Point> points1, List<Point> points2, List<Point> triList) = faceCorrespondences.GenerateFaceCorrespondences(imgMat1, imgMat2, faceLandmarkDetector);

        var tri = new DelaunayTriangulation().MakeDelaunay((int)size.width, (int)size.height, triList, img1, img2);
        //var tri = new DelaunayTriangulation().MakeDelaunay(jsonDelaunay.text);

        GenerateMorphSequence(5, 20, img1, img2, points1, points2, tri, size);
    }
    #endregion

    //private Mat ApplyAffineTransform(Mat src, List<Point> srcTri, List<Point> dstTri, Size size)
    //{
    //    Mat warpMat = Imgproc.getAffineTransform(new MatOfPoint2f(srcTri.ToArray()), new MatOfPoint2f(dstTri.ToArray()));
    //    Mat dst = new Mat(size, src.type());
    //    Imgproc.warpAffine(src, dst, warpMat, size, Imgproc.INTER_LINEAR, Core.BORDER_REFLECT_101);
    //    return dst;
    //}

    private Mat ApplyAffineTransform(Mat src, List<Point> srcTri, List<Point> dstTri, Size size)
    {
        //return ApplyAffineTransformManual(src, Imgproc.getAffineTransform(new MatOfPoint2f(srcTri.ToArray()), new MatOfPoint2f(dstTri.ToArray())), size);

        // Check for valid input Mat and points
        if (src.empty() || srcTri.Count != 3 || dstTri.Count != 3)
        {
            Debug.LogError("Invalid input data for ApplyAffineTransform.");
            return null;
        }

        using (MatOfPoint2f srcMat = new MatOfPoint2f(srcTri.ToArray()))
        using (MatOfPoint2f dstMat = new MatOfPoint2f(dstTri.ToArray()))
        {
            // Compute the affine transform
            using (Mat warpMat = Imgproc.getAffineTransform(srcMat, dstMat))
            {
                //Debug.Log("warpMat contents: " + warpMat.dump());
                // Ensure warpMat is not empty
                if (warpMat.empty())
                {
                    Debug.LogError("Failed to calculate affine transform.");
                    return null;
                }

                // Create the destination Mat with the specified size and same type as source
                Mat dst = new Mat(size, src.type());

                if (dst.empty())
                {
                    Debug.LogError("Failed to create destination Mat.");
                    return null;
                }

                try
                {
                    // Perform the affine warp
                    Imgproc.warpAffine(src, dst, warpMat, dst.size(), Imgproc.INTER_LINEAR, Core.BORDER_REFLECT_101);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception in warpAffine: {e.Message}");
                    dst.release(); // Release the destination Mat in case of failure
                    return null;
                }

                src.release();
                warpMat.release();
                return dst;
            }
        }
    }

    public void MorphTriangle(Mat img1, Mat img2, Mat img, List<Point> t1, List<Point> t2, List<Point> t, float alpha)
    {
        // Find bounding rectangle for each triangle
        Rect r1 = Imgproc.boundingRect(new MatOfPoint(t1.ToArray()));
        Rect r2 = Imgproc.boundingRect(new MatOfPoint(t2.ToArray()));
        Rect r = Imgproc.boundingRect(new MatOfPoint(t.ToArray()));

        // Offset points by left top corner of the respective rectangles
        List<Point> t1Rect = new List<Point>();
        List<Point> t2Rect = new List<Point>();
        List<Point> tRect = new List<Point>();

        for (int i = 0; i < 3; i++)
        {
            tRect.Add(new Point(t[i].x - r.x, t[i].y - r.y));
            t1Rect.Add(new Point(t1[i].x - r1.x, t1[i].y - r1.y));
            t2Rect.Add(new Point(t2[i].x - r2.x, t2[i].y - r2.y));
        }

        // Get mask by filling triangle
        Mat mask = Mat.zeros(r.height, r.width, CvType.CV_8UC3);
        MatOfPoint ptList = new MatOfPoint(tRect.ToArray());
        Imgproc.fillConvexPoly(mask, ptList, new Scalar(255.0, 255.0, 255.0), Imgproc.LINE_AA, 0);

        // Apply warpImage to small rectangular patches
        Mat img1Rect = new Mat(img1, r1);
        Mat img2Rect = new Mat(img2, r2);

        Size size = new Size(r.width, r.height);
        Mat warpImage1 = ApplyAffineTransform(img1Rect, t1Rect, tRect, size);
        Mat warpImage2 = ApplyAffineTransform(img2Rect, t2Rect, tRect, size);

        // Alpha blend rectangular patches
        Mat imgRect = new Mat();
        Core.addWeighted(warpImage1, 1.0 - alpha, warpImage2, alpha, 0.0, imgRect);

        // Convert imgRect to 8UC3 for blending
        Mat imgRect8UC3 = new Mat();
        imgRect.convertTo(imgRect8UC3, CvType.CV_8UC3);

        // Prepare the subregion of the output image
        Mat imgSubMat = img.submat(r);

        // Convert the subregion to 8UC3 if necessary
        if (imgSubMat.channels() != 3)
        {
            Mat temp = new Mat();
            Imgproc.cvtColor(imgSubMat, temp, Imgproc.COLOR_GRAY2BGR);
            imgSubMat = temp;
        }

        // Invert the mask
        Mat inverseMask = new Mat();
        Core.bitwise_not(mask, inverseMask);

        // Apply the inverse mask to the imgSubMat
        Mat maskedImgSubMat = new Mat();
        Core.bitwise_and(imgSubMat, inverseMask, maskedImgSubMat);

        // Apply the mask to the imgRect
        Mat maskedImgRect = new Mat();
        Core.bitwise_and(imgRect8UC3, mask, maskedImgRect);

        // Add the blended rectangular patch to the output image
        Core.add(maskedImgSubMat, maskedImgRect, imgSubMat);
    }

    public void GenerateMorphSequence(int duration, int frameRate, Mat img1, Mat img2, List<Point> points1, List<Point> points2, List<Tuple<int, int, int>> triList, Size size)
    {
        int numImages = duration * frameRate;

        for (int j = 0; j < numImages; j++)
        {
            Mat img1Float = new Mat();
            Mat img2Float = new Mat();
            img1.convertTo(img1Float, CvType.CV_32FC3);
            img2.convertTo(img2Float, CvType.CV_32FC3);

            List<Point> points = new List<Point>();
            float alpha = (float)j / (numImages - 1);

            //Debug.Log("Alpha:   "+alpha);
            for (int i = 0; i < points1.Count; i++)
            {
                var x = ((1 - alpha) * points1[i].x + alpha * points2[i].x);
                var y = ((1 - alpha) * points1[i].y + alpha * points2[i].y);
                points.Add(new Point(x, y));
                //Debug.Log($"Point: {x},{y}");
            }

            Mat morphedFrame = Mat.zeros(img1.size(), img1.type());

            for (int i = 0; i < triList.Count; i++)
            {
                int x = triList[i].Item1;
                int y = triList[i].Item2;
                int z = triList[i].Item3;

                List<Point> t1 = new List<Point> { points1[x], points1[y], points1[z] };
                List<Point> t2 = new List<Point> { points2[x], points2[y], points2[z] };
                List<Point> t = new List<Point> { points[x], points[y], points[z] };

                //Debug.Log($"T1: {points1[x]},{points1[y]},{points1[z]}   T2: {points2[x]},{points2[y]},{points2[z]}     T: {points[x]},{points[y]},{points[z]}");    
                MorphTriangle(img1Float, img2Float, morphedFrame, t1, t2, t, alpha);
            }

            byte[] imageData = ConvertMatToBytes(morphedFrame);
            onFrameCreated.Invoke(imageData);
        }
    }

    private byte[] ConvertMatToBytes(Mat mat)
    {
        // Convert the Mat object to a byte array to be written to the ffmpeg process
        var texture = mat.ToTexture2D();
        //resultTextures.Add(texture);

        var result = texture.EncodeToPNG();

        return result;
    }
}

public static class FaceMorphingExtensions
{
    public static Texture2D ToTexture2D(this Mat mat)
    {
        Mat temp = new Mat();
        Imgproc.cvtColor(mat, temp, Imgproc.COLOR_BGR2RGBA);

        Texture2D texture = new Texture2D(mat.cols(), mat.rows(), TextureFormat.RGBA32, false);
        Utils.matToTexture2D(temp, texture);
        return texture;
    }

    public static void ResetDirectory(this string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, true);
        }

        Directory.CreateDirectory(directoryPath);
    }
}