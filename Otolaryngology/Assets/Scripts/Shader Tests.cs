using UnityEngine;
using UnityEngine.Rendering;
using UnityVolumeRendering;

/// <summary>
/// Keeps a drilling mask alive between frames. 
/// C# creates the texture and starts the compute work
/// Drilling.compute writes the mask; the volume shader only reads it.
/// A mask value of 1 keeps a cell, and 0 hides it.
/// This changes rendering, not the original dataset or Unity physics colliders.
/// </summary>
public class ShaderTests : MonoBehaviour
{
    // Keep these field names so existing Inspector assignments survive.
    // Assign the SphereCollider on the drill tip in the Inspector.
    [SerializeField] private SphereCollider drillSphere;
    // Assign the VolumeRenderedObject that should respond to this drill.
    [SerializeField] private VolumeRenderedObject volume;
    // Check drill status.
    [SerializeField] public bool drillRunning = false;
    [SerializeField] private ComputeShader computeShader;

    // Start small when testing: 128^3 RFloat cells use about 8 MiB.
    // The mask covers the entire volume even when its resolution differs from the dataset. Fewer cells mean larger, less precise cuts. 
    // Set this BEFORE entering Play mode
    [SerializeField] private Vector3Int maskSize = new Vector3Int(128, 128, 128);

    private RenderTexture texture;
    private ComputeShader runtimeCompute;
    private int clearKernel;
    private int drillKernel;
    private bool initialized;
    private bool bindingWarningShown;
    private Material boundMaterial;
    private VolumeRenderedObject maskOwner;

    // Cache property IDs. The exact original spelling of the sphere parameter is intentional and matches Drilling.compute.
    private static readonly int MaskId = Shader.PropertyToID("_VolumeMask");
    private static readonly int MaskEnabledId = Shader.PropertyToID("_VolumeMaskEnabled");
    private static readonly int SphereId = Shader.PropertyToID("_DrillSpherrWorldPos");
    private static readonly int MatrixId = Shader.PropertyToID("_VolumeLocalToWorld");

    // Start is called once before the first execution of Update after the MonoBehaviour is created.
    private void Start()
    {
        // Report setup problems before calling methods on missing objects.
        if (volume == null || computeShader == null)
        {
            StopWithError("Assign Volume and Compute Shader in the Inspector.");
            return;
        }

        // RFloat must match RWTexture3D<float> in Drilling.compute.
        // Do not silently switch to R8: that also needs a matching HLSL format.
        if (!SystemInfo.supportsComputeShaders || !SystemInfo.supports3DTextures ||
            !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat) ||
            !SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.RFloat))
        {
            StopWithError("This device does not support the required compute / 3D RFloat texture features.");
            return;
        }

        int limit = SystemInfo.maxTexture3DSize;
        if (maskSize.x < 1 || maskSize.y < 1 || maskSize.z < 1 ||
            maskSize.x > limit || maskSize.y > limit || maskSize.z > limit)
        {
            StopWithError("Each Mask Size component must be between 1 and " + limit + ".");
            return;
        }

        // Use a private compute instance so other components using the same .compute asset cannot replace this instance's resource bindings.
        runtimeCompute = Instantiate(computeShader);
        //if (!runtimeCompute.HasKernel("ClearMask") || !runtimeCompute.HasKernel("DrillMask"))
        //{
        //    StopWithError("The assigned Compute Shader must contain ClearMask and DrillMask.");
        //    return;
        //}
        clearKernel = runtimeCompute.FindKernel("ClearMask");
        drillKernel = runtimeCompute.FindKernel("DrillMask");

        if (!runtimeCompute.HasKernel("ClearMask") || !runtimeCompute.HasKernel("DrillMask"))
        {
            StopWithError("The assigned Compute Shader must contain ClearMask and DrillMask.");
            return;
        }

        // The third constructor argument is depth-buffer bits.
        // Set Tex3D and volumeDepth explicitly, before Create(). A linear RFloat texture stores one number per cell, without color-space conversion.
        texture = new RenderTexture(maskSize.x, maskSize.y, 0,
            RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
        {
            name = "Drilling Mask (Runtime)",
            dimension = TextureDimension.Tex3D,
            volumeDepth = maskSize.z,
            enableRandomWrite = true,
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        if (!texture.Create())
        {
            StopWithError("Failed to allocate the drilling mask. Try a smaller Mask Size.");
            return;
        }

        // Create() allocates storage but does not fill it with 1.
        // Run ClearMask ONCE before exposing the mask to the rendering material.
        // Calling this every frame would erase all previously drilled holes.
        DispatchMask(clearKernel);
        maskOwner = volume;
        initialized = true;
        BindMaskToVolume();
    }

    // Each frame, check that the volume, its material, and the drill collider are available, then decide whether to dispatch the cutting calculation.
    //
    // When cutting is on, send the drill sphere's center and radius to the compute shader in world space, which is the scene's coordinate system.
    // Also send the volume's transform matrix so the shader can convert points inside the volume into that same coordinate system.
    //
    // The compute shader measures how far each mask cell center is from the drill's center and marks cells inside the sphere as removed. The rendering shader skips samples in those cells, making that region transparent.
    //
    // We refresh this information every frame because the drill or volume may move, rotate, or change size
    private void Update()
    {
        if (!initialized)
            return;

        // A mask belongs to one volume. Do not silently transfer old drilling history to a different volume selected during Play mode.
        if (volume != maskOwner)
        {
            StopWithError("The Volume reference changed. Restart Play mode to create a mask for the new volume.");
            return;
        }

        if (texture == null || !texture.IsCreated())
        {
            StopWithError("The drilling mask was lost. Restart Play mode to initialize it again.");
            return;
        }

        // Bind and enable the existing mask before checking drillRunning.
        // Turning off the drill stops NEW writes; it must not reveal old holes.
        // Retry binding here because some volumes create their renderer later.
        if (!BindMaskToVolume())
            return;

        // Safe check: All conditions must be true before cutting is allowed.
        bool canCut = drillRunning && drillSphere != null && drillSphere.enabled &&
            drillSphere.gameObject.activeInHierarchy;
        if (!canCut)
            return;

        // Get sphere collider transform.
        Transform drillTransform = drillSphere.transform;

        // Collider.center is local to the collider's own transform.
        // TransformPoint applies its offset and all parent transforms to obtain the actual world-space center; transform.position alone can be wrong.
        // SphereCollider.center is relative to the collider's transform, not world space.
        // TransformPoint() converts that local position into world space.
        Vector3 centerWorldPos = drillTransform.TransformPoint(drillSphere.center);
        // Read the world scale, including the scale inherited from parent objects.
        Vector3 scale = drillTransform.lossyScale;

        // Convert the local radius to world units using the largest absolute scale component. This matches a spherical cutting region.
        // Rotated, non-uniformly scaled hierarchies can introduce shear, so this is an approximation in that case. Prefer a uniformly scaled drill.
        // Convert the collider's local radius to a world-space radius.
        float radiusWS = drillSphere.radius * Mathf.Max(
            Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        if (radiusWS <= 0f)
            return;

        // Send collision inputs to COMPUTE, not to the rendering material.
        // xyz = sphere center in world coordinates; w = radius in world units.
        // Pack the sphere into one float4: xyz = world-space center, w = world-space radius.
        // The parameter name must exactly match between C# and the compute shader.
        // HLSL reads .xyz and .w, then checks whether distance squared <= radius squared.
        runtimeCompute.SetVector(SphereId,
            new Vector4(centerWorldPos.x, centerWorldPos.y, centerWorldPos.z, radiusWS));

        // Use the actual volume container's matrix. Compute converts each mask cell center from texture coordinates to this container's local cube, then to world coordinates, before comparing it with the drill.
        // Tell the shader how to convert volume sample positions into world-space coordinates.
        runtimeCompute.SetMatrix(MatrixId,
            volume.volumeContainerObject.transform.localToWorldMatrix);

        // Setting parameters alone does not execute a compute shader.
        // Dispatch performs this frame's writes into the SAME persistent texture.
        // This beginner version scans all cells; profile before raising resolution.
        DispatchMask(drillKernel);
    }

    private void DispatchMask(int kernel)
    {
        // Texture bindings belong to individual kernels. Set the binding for the kernel we are about to execute, and send the real dimensions.
        runtimeCompute.SetInts("_MaskSize", texture.width, texture.height, texture.volumeDepth);
        runtimeCompute.SetTexture(kernel, "ResultTexture", texture);

        // Dispatch takes GROUP counts, not cell counts. Read numthreads
        // from the kernel so changing the HLSL group size cannot break this math.
        runtimeCompute.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out uint z);
        runtimeCompute.Dispatch(kernel,
            (texture.width + (int)x - 1) / (int)x,
            (texture.height + (int)y - 1) / (int)y,
            (texture.volumeDepth + (int)z - 1) / (int)z);
    }

    private bool BindMaskToVolume()
    {
        // Safe check: Stop binding if the target volume or its container has not been assigned or created.
        if (volume == null || volume.volumeContainerObject == null)
            return BindingNotReady("Waiting for the assigned volume to create its container.");

        // Look on the actual volume container, not on an arbitrary child
        // of the outer object, which could be a drill or a different mesh.
        MeshRenderer renderer = volume.volumeContainerObject.GetComponent<MeshRenderer>();
        // Use the material currently assigned to the volume renderer.
        Material material = renderer != null ? renderer.sharedMaterial : null;
        // Safe check: The volume-rendering material and required mask properties must be available.
        if (material == null || !material.HasProperty(MaskId) || !material.HasProperty(MaskEnabledId))
            return BindingNotReady("The volume material must use the corrected URP volume shader with _VolumeMask and _VolumeMaskEnabled.");

        // If the plugin replaces its material, detach our old binding
        // and apply the mask to the new one. Use one independent volume material
        // and one ShaderTests component per volume; do not share it across volumes.
        if (boundMaterial != material)
        {
            UnbindMask();
            boundMaterial = material;
        }

        // These two names do not have to match ResultTexture in compute.
        // Both names point to the same RenderTexture object held by this script.
        material.SetTexture(MaskId, texture);
        material.SetFloat(MaskEnabledId, 1f);
        bindingWarningShown = false;
        return true;
    }

    private bool BindingNotReady(string message)
    {
        // Keep retrying without flooding the Console each frame.
        if (!bindingWarningShown)
            Debug.LogWarning("ShaderTests: " + message, this);
        bindingWarningShown = true;
        UnbindMask();
        return false;
    }

    [ContextMenu("Reset Drilling Mask (Play Mode)")]
    public void ResetMask()
    {
        // Explicit reset is the only normal operation that writes 1
        // again. Stop the drill before resetting, or it will immediately recut.
        SetDrilling(false);
        if (!Application.isPlaying || !initialized || texture == null || !texture.IsCreated())
            return;
        DispatchMask(clearKernel);
        BindMaskToVolume();
    }

    // Existing buttons or interaction code can still call this method.
    /// Requests that cutting start or stop.
    public void SetDrilling(bool running)
    {
        drillRunning = running;
    }

    private void StopWithError(string message)
    {
        Debug.LogError("ShaderTests: " + message, this);
        initialized = false;
        UnbindMask();
        enabled = false;
    }

    private void UnbindMask()
    {
        //  Only clear the material if it still points to OUR texture.
        // This avoids removing a replacement binding owned by another script.
        if (boundMaterial != null && boundMaterial.GetTexture(MaskId) == texture)
        {
            boundMaterial.SetFloat(MaskEnabledId, 0f);
            boundMaterial.SetTexture(MaskId, null);
        }
        boundMaterial = null;
    }

    private void OnDestroy()
    {
        // Stop rendering from the texture before releasing GPU storage.
        // There is deliberately no OnDisable reset: temporarily disabling this
        // component should retain the existing holes while the volume stays alive.
        initialized = false;
        UnbindMask();
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
            texture = null;
        }
        if (runtimeCompute != null)
            Destroy(runtimeCompute);
    }
}
