using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityVolumeRendering;

public class colorpoints : MonoBehaviour
{

    [SerializeField] VolumeRenderedObject obj;

    public List<float> tfPoints = new List<float>();
    public List<float> alphaPoints = new List<float>();
    public int[] alphaIndex = new int[] { 1, 4, 7, 10, 13, 37, 18, 21, 24, 27, 30, 33, 36 };
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < obj.transferFunction.colourControlPoints.Count; i++)
        {
            tfPoints.Add(obj.transferFunction.colourControlPoints[i].dataValue);
        }
        //Debug.Log(tfPoints.Count);
        tfPoints.Sort();

        //for (int i = 0;i < obj.transferFunction.alphaControlPoints.Count; i++)
        //{
        //    alphaPoints.Add(obj.transferFunction.alphaControlPoints[i].dataValue);
        //}
    }

    // Update is called once per frame
    void Update()
    {
        //Debug.Log(obj.transferFunction.colourControlPoints.Count);
        //obj.transferFunction.colourControlPoints.Clear();
        //Debug.Log(obj.transferFunction.colourControlPoints.Count);
        //obj.SetTransferFunctionMode(TFRenderMode.TF1D);

        //for (int i = 0; i < 1; i++)
        //{
        //    TFAlphaControlPoint test = obj.transferFunction.alphaControlPoints[alphaIndex[i]];
        //    test.alphaValue = 0; 
        //    obj.transferFunction.alphaControlPoints[i] = test;
        //    obj.SetTransferFunctionMode(TFRenderMode.TF1D);
        //}

    }
}
