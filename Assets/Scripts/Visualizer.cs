using UnityEngine;
using UnityEngine.UI;
using Klak.TestTools;
using MediaPipe.FaceLandmark;
using System.Collections.Generic;
using System.Linq;

public sealed class Visualizer : MonoBehaviour
{
    #region Editable attributes

    [SerializeField] ImageSource _source = null;
    [SerializeField] ResourceSet _resources = null;
    [SerializeField] RawImage _previewUI = null;
    [SerializeField] Mesh _template = null;
    [SerializeField] Shader _shader = null;

    #endregion

    #region Private members

    FaceLandmarkDetector _detector;
    Material _material;

    #endregion

    #region MonoBehaviour implementation

    void Start()
    {
        _detector = new FaceLandmarkDetector(_resources);
        _material = new Material(_shader);
    }

    void OnDestroy()
    {
        _detector.Dispose();
        Destroy(_material);
    }

    void LateUpdate()
    {
        // Face landmark detection
        //_detector.ProcessImage(_source.Texture);

        ////Debug.Log(_detector.VertexArray.Length);
        //// UI update
        //_previewUI.texture = _source.Texture;
    }

    //void OnRenderObject()
    //{
    //    // Wireframe mesh rendering
    //    _material.SetBuffer("_Vertices", _detector.VertexBuffer);
    //    _material.SetPass(0);
    //    Graphics.DrawMeshNow(_template, Matrix4x4.identity);

    //    // Keypoint marking
    //    _material.SetBuffer("_Vertices", _detector.VertexBuffer);
    //    _material.SetPass(1);
    //    Graphics.DrawProceduralNow(MeshTopology.Lines, 400, 1);
    //}

    #endregion

    public static readonly int[] BurracudaToDlib68Indices = new int[]
    {
    // Jawline (17 points)
    10, 109, 67, 103, 54, 21, 162, 127, 234, 93, 132, 58, 172, 136, 150, 149, 176,

    // Right Eyebrow (5 points)
    362, 363, 364, 365, 366,

    // Left Eyebrow (5 points)
    33, 34, 35, 36, 37,

    // Nose Bridge (4 points)
    168, 197, 5, 4,

    // Nose Bottom (5 points)
    2, 94, 141, 20, 48,

    // Right Eye (6 points)
    33, 246, 161, 160, 159, 158,

    // Left Eye (6 points)
    263, 466, 388, 387, 386, 385,

    // Outer Lips (12 points)
    78, 95, 88, 178, 87, 14, 317, 402, 318, 324, 308, 191,

    // Inner Lips (8 points)
    80, 81, 82, 13, 312, 311, 310, 415
    };

    public List<Vector2> ConvertToVector2List(Vector4[] landmarks468, int imageWidth, int imageHeight)
    {
        //imageWidth = 1024;
        //imageHeight = 1024;

        //landmarks468 = landmarks468.OrderByDescending(p => p.x).ToArray();

        string str = "";
        int i = 0;

        foreach (var f in landmarks468)
        {
            str += $"{i} {(f.x)}  {(f.y)}"+"\n";
            i++;
        }

        Debug.Log(str);

        List<Vector2> landmarks68 = new List<Vector2>();

        foreach (int index in Visualizer.BurracudaToDlib68Indices)
        {
            // Get the Vector4 point from the 468-point array
            Vector4 point = landmarks468[index];

            int x = (int)point.x * imageWidth/2;
            int y = (int)point.y * imageHeight/2;

            // Convert to Vector2 (x, y) and add to the list
            landmarks68.Add(new Vector2(x, y));
        }

        return landmarks68;
    }

    public List<Vector2> GetLandmarkPoints68(Texture texture)
    {
        _detector.ProcessImage(texture);
        return ConvertToVector2List(_detector.VertexArray.ToArray(), texture.width, texture.height);
    }

    public Vector4[] GetLandmarkPoints(Texture texture)
    {
        _detector.ProcessImage(texture);
        ConvertToVector2List(_detector.VertexArray.ToArray(), texture.width, texture.height);
        return _detector.VertexArray.ToArray();
    }
}