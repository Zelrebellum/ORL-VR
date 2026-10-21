using UnityEngine;
using UnityVolumeRendering;

/// <summary>
/// Sends the drill's spherical cutting region to the target volume's material.
/// </summary>
public class ShaderTests : MonoBehaviour
{
    //public Transform culler;

    // Assign the SphereCollider on the drill tip in the Inspector. --------------------------------------------------------------------
    [SerializeField]
    SphereCollider drillSphere;

    // Assign the VolumeRenderedObject that should respond to this drill. ---------------------------------------------------------------
    [SerializeField]
    private VolumeRenderedObject volume;

    // Check drill status ---------------------------------------------------------------------------------------------------------------
    [SerializeField]
    private bool drillRunning = false;

    //public Renderer myRenderer; 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //myRenderer = GetComponent<Renderer>();
    }


    // Each frame, check that the volume, its material, and the drill collider ----------------------------------------------------------
    // are available, then tell the shader whether cutting is allowed.
    //
    // When cutting is on, send the drill sphere's center and radius to the
    // shader in world space, which is the scene's coordinate system.
    // Also send the volume's transform matrix so the shader can convert
    // points inside the volume into that same coordinate system.
    //
    // The shader can then measure how far each sample point is from the
    // drill's center. Points inside the sphere are skipped when drawing
    // the volume, making that region transparent.
    //
    // We refresh this information every frame because the drill or volume
    // may move, rotate, or change size.
    //
    // The original voxel data stays unchanged. Hidden areas become visible
    // again when the drill moves away or cutting is turned off.
    void Update()
    {
        // Safe check: Stop if the target volume or its renderer has not been assigned or created.
        if (volume == null || volume.GetComponentInChildren<MeshRenderer>() == null)
        //{
        //    Debug.Log("Volume not assigned");
            return;
        //}
        
        // Use the material currently assigned to the volume renderer.
        Material material = volume.GetComponentInChildren<MeshRenderer>().sharedMaterial;

        // Safe check: Volume-rendering material is available or not
        if (material == null)
        //{
        //    Debug.Log("Material not assigned");
            return;
        //}

        // safe check: All conditions must be true before cutting is allowed:
        bool canCut = drillRunning && drillSphere != null && drillSphere.enabled && drillSphere.gameObject.activeInHierarchy && volume.volumeContainerObject != null;

        // Send 1 for enabled and 0 for disabled. The HLSL will checks this value
        material.SetFloat("_DrillEnabled", canCut ? 1f : 0f);

        // Safe check
        if (!canCut)
        //{
        //    Debug.Log(canCut);
            return;
        //}
        
        // Get sphere collider transform
        Transform drillTransform = drillSphere.transform;


        // SphereCollider.center is relative to the collider's transform, not world space.
        // TransformPoint() converts that local position into world space
        Vector3 centerWorldPos =
            drillTransform.TransformPoint(drillSphere.center);

        // Read the =world scale=
        Vector3 scale = drillTransform.lossyScale;

        // Convert the collider's local radius to a world-space radius.
        float radiusWS = drillSphere.radius * Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)
        );

        // Pack the sphere into one float4: xyz = world-space center, w = world-space radius.
        // The parameter name must exactly match the name both in shader and in HLSL
        // HLSL reads .xyz and .w, then checks whether distance squared <= radius squared.
        material.SetVector(
            "_DrillSpherrWorldPos",
            new Vector4(
                centerWorldPos.x,
                centerWorldPos.y,
                centerWorldPos.z,
                radiusWS
            )
        );

        // Tell the shader how to convert volume sample positions into world-space coordinates.
        material.SetMatrix(
            "_VolumeLocalToWorld",
            volume.volumeContainerObject.transform.localToWorldMatrix
        );

        //Debug.Log("End Update");
    }

    // Clear the material's cutting switch when this component is disabled. ----------------------------------------------------------
    //void OnDisable()
    //{
    //    if (volume == null || volume.meshRenderer == null)
    //        return;

    //    Material material = volume.meshRenderer.sharedMaterial;

    //    if (material != null)
    //        material.SetFloat("_DrillEnabled", 0f);
    //}


    /// Requests that cutting start or stop. ---------------------------------------------------------------------------------------------
    public void SetDrilling(bool running){
        drillRunning = running;
    }

}
