using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityVolumeRendering;

public class Opacity : MonoBehaviour
{
    [SerializeField] VolumeRenderedObject obj;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnValueChange(float value)
    {
        TFAlphaControlPoint newAlpha = obj.transferFunction.alphaControlPoints[obj.GetComponent<colorpoints>().alphaIndex[this.GetComponent<sliderIndex>().indexNumber]];
        newAlpha.alphaValue = value;
        obj.transferFunction.alphaControlPoints[obj.GetComponent<colorpoints>().alphaIndex[this.GetComponent<sliderIndex>().indexNumber]] = newAlpha;
        obj.SetTransferFunctionMode(TFRenderMode.TF1D);
    }

}
