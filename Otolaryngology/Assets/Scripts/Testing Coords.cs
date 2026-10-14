using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityVolumeRendering;

public class TestingCoords : MonoBehaviour
{
    [SerializeField] VolumeRenderedObject obj;
    Texture3D myTexture;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //Debug.Log(obj.dataset.GetDataTexture().GetPixel(0, 1, 2));
        myTexture = new Texture3D(1, 1, 1, TextureFormat.RGBA32, false);
        obj.dataset.RecreateDataTexture();
        myTexture = obj.dataset.GetDataTexture();
        Debug.Log(myTexture.isReadable);
        Debug.Log(myTexture.GetPixel(0, 1, 2));
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
