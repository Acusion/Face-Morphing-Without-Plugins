using System;
using System.Collections.Generic;
using UnityEngine;
using OpenCVForUnity;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgcodecsModule;
using OpenCVForUnity.ImgprocModule;
using Rect = OpenCVForUnity.CoreModule.Rect;

public class DelaunayTriangulation
{
    private bool RectContains(Rect rect, Point point)
    {
        if (point.x < rect.x || point.y < rect.y || point.x > rect.x + rect.width || point.y > rect.y + rect.height)
            return false;
        return true;
    }

    // Write the Delaunay triangles into a list
    private List<(int, int, int)> DrawDelaunay(int f_w, int f_h, Subdiv2D subdiv, Dictionary<string, int> dictionary1)
    {
        List<(int, int, int)> list4 = new List<(int, int, int)>();

        using (MatOfFloat6 triangleList = new MatOfFloat6())
        {
            subdiv.getTriangleList(triangleList);

            float[] triangles = triangleList.toArray();
            Rect rect = new Rect(0, 0, f_w, f_h);

            for (int i = 0; i < triangles.Length; i += 6)
            {
                Point pt1 = new Point(triangles[i], triangles[i + 1]);
                Point pt2 = new Point(triangles[i + 2], triangles[i + 3]);
                Point pt3 = new Point(triangles[i + 4], triangles[i + 5]);

                if (RectContains(rect, pt1) && RectContains(rect, pt2) && RectContains(rect, pt3))
                {
                    // Convert points to strings for dictionary lookup
                    string pt1Str = $"{pt1.x},{pt1.y}";
                    string pt2Str = $"{pt2.x},{pt2.y}";
                    string pt3Str = $"{pt3.x},{pt3.y}";

                    // Ensure points are found in the dictionary
                    if (dictionary1.ContainsKey(pt1Str) && dictionary1.ContainsKey(pt2Str) && dictionary1.ContainsKey(pt3Str))
                    {
                        list4.Add((dictionary1[pt1Str], dictionary1[pt2Str], dictionary1[pt3Str]));
                    }
                }
            }
        }

        dictionary1.Clear();
        return list4;
    }

    // Make the Delaunay triangulation
    public List<Tuple<int, int, int>> MakeDelaunay(int f_w, int f_h, List<Point> theList, Mat img1, Mat img2)
    {
        // Make a rectangle.
        Rect rect = new Rect(0, 0, f_w, f_h);

        // Create an instance of Subdiv2D.
        Subdiv2D subdiv = new Subdiv2D(rect);

        // Convert theList to points and a dictionary.
        List<Point> points = new List<Point>();
        Dictionary<string, int> dictionary = new Dictionary<string, int>();

        foreach (Point p in theList)
        {
            points.Add(new Point((int)p.x, (int)p.y));
        }

        for (int i = 0; i < points.Count; i++)
        {
            string pointStr = $"{points[i].x},{points[i].y}";
            dictionary[pointStr] = i;
        }

        // Insert points into subdiv
        foreach (Point p in points)
        {
            subdiv.insert(p);
        }

        // Make a Delaunay triangulation list.
        List<(int, int, int)> list4 = DrawDelaunay(f_w, f_h, subdiv, dictionary);

        // Return the list.
        return list4.ConvertAll(x => new Tuple<int, int, int>(x.Item1, x.Item2, x.Item3));
    }
}