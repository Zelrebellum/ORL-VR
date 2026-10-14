using UnityEngine;

public class ShaderTests : MonoBehaviour
{
    public Transform culler;
    public Renderer myRenderer; 
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //myRenderer = GetComponent<Renderer>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!myRenderer) {  return; }
        //if(!culler) { return; }
        
        Material[] material = myRenderer.sharedMaterials;

        for(int i =0; i<material.Length; i++)
        {
            Debug.Log("its reading");
        }
    }
}
