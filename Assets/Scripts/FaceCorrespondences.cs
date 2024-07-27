using System;
using System.Collections.Generic;
using UnityEngine;
using DlibFaceLandmarkDetector;
using OpenCVForUnity;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgcodecsModule;
using OpenCVForUnity.ImgprocModule;
using Rect = OpenCVForUnity.CoreModule.Rect;
using OpenCVForUnity.UnityUtils;

public class FaceCorrespondences
{
    public Tuple<Size, Mat, Mat, List<Point>, List<Point>, List<Point>> GenerateFaceCorrespondences(Mat theImage1, Mat theImage2, FaceLandmarkDetector faceLandmarkDetector)
    {
        using (Mat corresp = new Mat(68, 2, CvType.CV_64FC1))
        {

            var tempImgList = CropImage(theImage1, theImage2);

            var imgList = new List<Mat>() { tempImgList.Item1, tempImgList.Item2 };

            var list1 = new List<Point>();
            var list2 = new List<Point>();
            var list3 = new List<Point>();
            int j = 1;

            foreach (var img in imgList)
            {
                var size = img.size();
                List<Point> currList = (j == 1) ? list1 : list2;

                // Detect faces in the image
                faceLandmarkDetector.SetImage(img.ToTexture2D());

                var faceRects = faceLandmarkDetector.Detect();

                if (faceRects == null || faceRects.Count == 0)
                {
                    Debug.LogError("No faces detected in the image.");
                    continue; // Skip to the next image
                }

                j++;

                foreach (var rect in faceRects)
                {
                    var points = faceLandmarkDetector.DetectLandmark(rect);

                    if (points == null || points.Count == 0)
                    {
                        Debug.LogError("No landmarks detected for the face.");
                        continue; // Skip processing if no landmarks are found
                    }

                    for (int i = 0; i < 68; i++)
                    {
                        var point = points[i];
                        int x = Mathf.CeilToInt(point.x);
                        int y = Mathf.CeilToInt(point.y);
                        currList.Add(new Point(x, y));
                        corresp.put(i, 0, corresp.get(i, 0)[0] + x);
                        corresp.put(i, 1, corresp.get(i, 1)[0] + y);
                    }

                    // Add back the background
                    currList.Add(new Point(1, 1));
                    currList.Add(new Point(size.width - 1, 1));
                    currList.Add(new Point((size.width - 1) / 2, 1));
                    currList.Add(new Point(1, size.height - 1));
                    currList.Add(new Point(1, (size.height - 1) / 2));
                    currList.Add(new Point((size.width - 1) / 2, size.height - 1));
                    currList.Add(new Point(size.width - 1, size.height - 1));
                    currList.Add(new Point(size.width - 1, (size.height - 1) / 2));
                }

                Debug.Log("Face detected in image " + j);
            }

            // Average the landmark points
            for (int i = 0; i < 68; i++)
            {
                double avgX = corresp.get(i, 0)[0] / 2;
                double avgY = corresp.get(i, 1)[0] / 2;
                list3.Add(new Point(avgX, avgY));
            }

            // Add back the background
            AddBackgroundPoints(list3, theImage1.size());

            return new Tuple<Size, Mat, Mat, List<Point>, List<Point>, List<Point>>(theImage1.size(), tempImgList.Item1, tempImgList.Item2, list1, list2, list3);
        }
    }

    private static void AddBackgroundPoints(List<Point> list, Size size)
    {
        list.Add(new Point(1, 1));
        list.Add(new Point(size.width - 1, 1));
        list.Add(new Point((size.width - 1) / 2, 1));
        list.Add(new Point(1, size.height - 1));
        list.Add(new Point(1, (size.height - 1) / 2));
        list.Add(new Point((size.width - 1) / 2, size.height - 1));
        list.Add(new Point(size.width - 1, size.height - 1));
        list.Add(new Point(size.width - 1, (size.height - 1) / 2));
    }

    private Tuple<Mat, Mat> CropImage(Mat img1, Mat img2)
    {
        //return new Tuple<Mat, Mat>(img1, img2);

        var margins = CalculateMarginHelp(img1, img2);
        var size1 = margins.Item1;
        var size2 = margins.Item2;
        var diff0 = margins.Item3;
        var diff1 = margins.Item4;
        var avg0 = margins.Item5;
        var avg1 = margins.Item6;

        int size1Height = (int)size1.height;
        int size1Width = (int)size1.width;
        int size2Height = (int)size2.height;
        int size2Width = (int)size2.width;

        if (size1.Equals(size2))
            return new Tuple<Mat, Mat>(img1, img2);

        if (size1.height <= size2.height && size1.width <= size2.width)
        {
            double scale0 = (double)size1.height / size2.height;
            double scale1 = (double)size1.width / size2.width;

            using (Mat resizedImg = new Mat())
            {
                if (scale0 > scale1)
                    Imgproc.resize(img2, resizedImg, new Size(0, 0), scale0, scale0, Imgproc.INTER_AREA);
                else
                    Imgproc.resize(img2, resizedImg, new Size(0, 0), scale1, scale1, Imgproc.INTER_AREA);

                return CropImageHelp(img1, resizedImg);
            }
        }

        if (size1.height >= size2.height && size1.width >= size2.width)
        {
            double scale0 = (double)size2.height / size1.height;
            double scale1 = (double)size2.width / size1.width;

            using (Mat resizedImg = new Mat())
            {
                if (scale0 > scale1)
                    Imgproc.resize(img1, resizedImg, new Size(0, 0), scale0, scale0, Imgproc.INTER_AREA);
                else
                    Imgproc.resize(img1, resizedImg, new Size(0, 0), scale1, scale1, Imgproc.INTER_AREA);

                return CropImageHelp(resizedImg, img2);
            }
        }

        if (size1Height >= size2Height && size1Width <= size2Width)
        {
            // img1 height needs cropping, img2 width needs cropping
            Rect cropRect1 = new Rect(0, diff0, size1Width, avg0 - diff0);
            Rect cropRect2 = new Rect(Mathf.Abs(diff1), 0, avg1 - Mathf.Abs(diff1), size2Height);

            using (Mat croppedImg1 = new Mat(img1, cropRect1))
            using (Mat croppedImg2 = new Mat(img2, cropRect2))
            {
                return new Tuple<Mat, Mat>(croppedImg1, croppedImg2);
            }
        }
        else
        {
            // img1 width needs cropping, img2 height needs cropping
            Rect cropRect1 = new Rect(diff1, 0, avg1 - diff1, size1Height);
            Rect cropRect2 = new Rect(0, Mathf.Abs(diff0), size2Width, avg0 - Mathf.Abs(diff0));

            using (Mat croppedImg1 = new Mat(img1, cropRect1))
            using (Mat croppedImg2 = new Mat(img2, cropRect2))
            {
                return new Tuple<Mat, Mat>(croppedImg1, croppedImg2);
            }
        }
    }

    private Tuple<Size, Size, int, int, int, int> CalculateMarginHelp(Mat img1, Mat img2)
    {
        Size size1 = img1.size();
        Size size2 = img2.size();
        int diff0 = (int)(Math.Abs(size1.height - size2.height) / 2);
        int diff1 = (int)(Math.Abs(size1.width - size2.width) / 2);
        int avg0 = (int)((size1.height + size2.height) / 2);
        int avg1 = (int)((size1.width + size2.width) / 2);

        return new Tuple<Size, Size, int, int, int, int>(size1, size2, diff0, diff1, avg0, avg1);
    }

    private Tuple<Mat, Mat> CropImageHelp(Mat img1, Mat img2)
    {
        var margins = CalculateMarginHelp(img1, img2);
        var size1 = margins.Item1;
        var size2 = margins.Item2;

        int size1Height = (int)size1.height;
        int size1Width = (int)size1.width;
        int size2Height = (int)size2.height;
        int size2Width = (int)size2.width;

        var diff0 = margins.Item3;
        var diff1 = margins.Item4;
        var avg0 = margins.Item5;
        var avg1 = margins.Item6;

        if (size1Height == size2Height && size1Width == size2Width)
        {
            return new Tuple<Mat, Mat>(img1, img2);
        }
        else if (size1Height <= size2Height && size1Width <= size2Width)
        {
            // img2 needs cropping
            Rect cropRect = new Rect(Mathf.Abs(diff1), Mathf.Abs(diff0), avg1 - Mathf.Abs(diff1), avg0 - Mathf.Abs(diff0));
            using (Mat croppedImg2 = new Mat(img2, cropRect))
            {
                return new Tuple<Mat, Mat>(img1, croppedImg2);
            }
        }
        else if (size1Height >= size2Height && size1Width >= size2Width)
        {
            // img1 needs cropping
            Rect cropRect = new Rect(diff1, diff0, avg1 - diff1, avg0 - diff0);
            using (Mat croppedImg1 = new Mat(img1, cropRect))
            {
                return new Tuple<Mat, Mat>(croppedImg1, img2);
            }
        }
        else if (size1Height >= size2Height && size1Width <= size2Width)
        {
            // img1 height needs cropping, img2 width needs cropping
            Rect cropRect1 = new Rect(0, diff0, size1Width, avg0 - diff0);
            Rect cropRect2 = new Rect(Mathf.Abs(diff1), 0, avg1 - Mathf.Abs(diff1), size2Height);
            using (Mat croppedImg1 = new Mat(img1, cropRect1))
            using (Mat croppedImg2 = new Mat(img2, cropRect2))
            {
                return new Tuple<Mat, Mat>(croppedImg1, croppedImg2);
            }
        }
        else
        {
            // img1 width needs cropping, img2 height needs cropping
            Rect cropRect1 = new Rect(diff1, 0, avg1 - diff1, size1Height);
            Rect cropRect2 = new Rect(0, Mathf.Abs(diff0), size2Width, avg0 - Mathf.Abs(diff0));
            using (Mat croppedImg1 = new Mat(img1, cropRect1))
            using (Mat croppedImg2 = new Mat(img2, cropRect2))
            {
                return new Tuple<Mat, Mat>(croppedImg1, croppedImg2);
            }
        }
    }

    #region Json Version
    /*
    [Serializable]
    public class Coordinate
    {
        public int x;
        public int y;
    }

    [Serializable]
    public class CoordinateContainer
    {
        public List<Coordinate> coordinates;
    }

    public Tuple<Size, Mat, Mat, List<Point>, List<Point>, List<Point>> GenerateFaceCorrespondences(Mat theImage1, Mat theImage2, string point1Jaon, string point2Json, string list3Json)
    {
        var faceLandmarkDetector = new FaceLandmarkDetector(shapePredictorPath);
        var corresp = new Mat(68, 2, CvType.CV_64FC1);

        var tempImgList = CropImage(theImage1, theImage2);

        var imgList = new List<Mat>() { tempImgList.Item1, tempImgList.Item2 };

        var list1 = new List<Point>();
        var list2 = new List<Point>();
        var list3 = new List<Point>();
        int j = 1;

        foreach (var img in imgList)
        {
            var size = img.size();
            List<Point> currList = (j == 1) ? list1 : list2;

            // Detect faces in the image
            faceLandmarkDetector.SetImage(img.ToTexture2D());

            var faceRects = faceLandmarkDetector.Detect();

            if (faceRects.Count == 0)
            {
                Debug.LogError("Sorry, but I couldn't find a face in the image.");
                continue;
            }

            j++;

            foreach (var rect in faceRects)
            {
                var points = faceLandmarkDetector.DetectLandmark(rect);

                for (int i = 0; i < 68; i++)
                {
                    var point = points[i];
                    int x = Mathf.CeilToInt(point.x);
                    int y = Mathf.CeilToInt(point.y);
                    currList.Add(new Point(x, y));
                    corresp.put(i, 0, corresp.get(i, 0)[0] + x);
                    corresp.put(i, 1, corresp.get(i, 1)[0] + y);
                }

                // Add back the background
                currList.Add(new Point(1, 1));
                currList.Add(new Point(size.width - 1, 1));
                currList.Add(new Point((size.width - 1) / 2, 1));
                currList.Add(new Point(1, size.height - 1));
                currList.Add(new Point(1, (size.height - 1) / 2));
                currList.Add(new Point((size.width - 1) / 2, size.height - 1));
                currList.Add(new Point(size.width - 1, size.height - 1));
                currList.Add(new Point(size.width - 1, (size.height - 1) / 2));
            }
        }

        // Average the landmark points
        for (int i = 0; i < 68; i++)
        {
            double avgX = corresp.get(i, 0)[0] / 2;
            double avgY = corresp.get(i, 1)[0] / 2;
            list3.Add(new Point(avgX, avgY));
        }

        // Add back the background
        AddBackgroundPoints(list3, theImage1.size());

        //return new Tuple<Size, Mat, Mat, List<Point>, List<Point>, List<Point>>(theImage1.size(), tempImgList.Item1, tempImgList.Item2, list1, list2, list3);

        CoordinateContainer point1Temp = JsonUtility.FromJson<CoordinateContainer>(point1Jaon);
        CoordinateContainer point2Temp = JsonUtility.FromJson<CoordinateContainer>(point2Json);
        CoordinateContainer list3Temp = JsonUtility.FromJson<CoordinateContainer>(list3Json);

        List<Point> point1list = new List<Point>();
        List<Point> point2list = new List<Point>();
        List<Point> list3List = new List<Point>();

        foreach (var point in point1Temp.coordinates)
        {
            point1list.Add(new Point(point.x, point.y));
        }

        foreach (var point in point2Temp.coordinates)
        {
            point2list.Add(new Point(point.x, point.y));
        }

        foreach (var point in list3Temp.coordinates)
        {
            list3List.Add(new Point(point.x, point.y));
        }

        return new Tuple<Size, Mat, Mat, List<Point>, List<Point>, List<Point>>(theImage1.size(), tempImgList.Item1, tempImgList.Item2, point1list, point2list, list3List);
    }
    */
    #endregion
}