using UnityEngine;
using UnityEngine.Rendering.Universal;

// 눈 위치에서 전방 장애물 깊이를 기록한다. 적 탐지나 피해 판정은 변경하지 않는다.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class FOVEyeDepthRenderer : MonoBehaviour
{
    [SerializeField] private FOV sourceFOV;
    [SerializeField] private LayerMask occluderLayers;
    [SerializeField, Min(64)] private int textureSize = 512;
    [SerializeField] private int captureRendererIndex = 1;
    [SerializeField, Min(0f)] private float depthBias = 0.08f;
    [SerializeField, Min(0f)] private float normalBias = 0.12f;
    [SerializeField] private bool useEyeDepth = true;
    private Camera depthCamera;
    private RenderTexture depthTexture;
    public RenderTexture DepthTexture => depthTexture;
    public Camera DepthCamera => depthCamera;

    private void OnEnable()
    {
        if (sourceFOV == null) sourceFOV = GetComponent<FOV>();
        if (sourceFOV == null) { enabled = false; return; }
        var root = new GameObject("FOV Eye Depth Camera (runtime)");
        root.transform.SetParent(transform, false);
        depthCamera = root.AddComponent<Camera>();
        depthCamera.depth = -90f;
        depthCamera.clearFlags = CameraClearFlags.SolidColor;
        depthCamera.backgroundColor = Color.black;
        depthCamera.nearClipPlane = 0.05f;
        depthCamera.allowHDR = false;
        depthCamera.allowMSAA = false;
        depthCamera.useOcclusionCulling = false;
        var data = root.AddComponent<UniversalAdditionalCameraData>();
        // 메인 화면 합성이 없는 기존 마스크 전용 Renderer를 재사용한다.
        data.SetRenderer(captureRendererIndex);
        data.renderPostProcessing = false;
        data.renderShadows = false;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.Off;
        depthTexture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32)
        { name = "FOV Eye Depth", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        if (!depthTexture.Create()) { enabled = false; return; }
        depthCamera.targetTexture = depthTexture;
        Synchronize();
    }

    private void LateUpdate() => Synchronize();

    private void Synchronize()
    {
        if (depthCamera == null || depthTexture == null || sourceFOV == null) return;
        depthCamera.transform.position = sourceFOV.ViewOriginPosition;
        depthCamera.transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
        depthCamera.fieldOfView = Mathf.Clamp(sourceFOV.ViewAngle + 10f, 60f, 150f);
        depthCamera.aspect = 1f;
        depthCamera.farClipPlane = Mathf.Max(1f, sourceFOV.ViewRadius + 2f);
        depthCamera.cullingMask = occluderLayers;
        Shader.SetGlobalTexture("_FOVEyeDepth", depthTexture, UnityEngine.Rendering.RenderTextureSubElement.Depth);
        Shader.SetGlobalMatrix("_FOVEyeVP", GL.GetGPUProjectionMatrix(depthCamera.projectionMatrix, true)
            * depthCamera.worldToCameraMatrix);
        Shader.SetGlobalVector("_FOVEyeOrigin", depthCamera.transform.position);
        Shader.SetGlobalVector("_FOVEyeForward", depthCamera.transform.forward);
        Shader.SetGlobalVector("_FOVEyeSettings", new Vector4(sourceFOV.ViewRadius,
            sourceFOV.AwarenessRadius, Mathf.Cos(sourceFOV.ViewAngle * 0.5f * Mathf.Deg2Rad), depthBias));
        Shader.SetGlobalVector("_FOVEyeClip", new Vector4(depthCamera.nearClipPlane, depthCamera.farClipPlane, 0f, 0f));
        Shader.SetGlobalFloat("_FOVEyeEnabled", useEyeDepth ? 1f : 0f);
        Shader.SetGlobalFloat("_FOVEyeNormalBias", normalBias);
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat("_FOVEyeEnabled", 0f);
        Shader.SetGlobalTexture("_FOVEyeDepth", Texture2D.blackTexture);
        if (depthCamera != null) { depthCamera.enabled = false; depthCamera.targetTexture = null; Destroy(depthCamera.gameObject); }
        if (depthTexture != null) { depthTexture.Release(); Destroy(depthTexture); }
        depthCamera = null;
        depthTexture = null;
    }
}
