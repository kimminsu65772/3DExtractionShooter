using UnityEngine;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class FOVMaskRenderer : MonoBehaviour
{
    [Header("마스크 한 변의 픽셀 수")]
    [SerializeField, Min(64)] private int maskTextureSize = 512;

    [SerializeField] private Camera maskCamera;

    [Header("추적할 FOV")]
    [SerializeField] private FOV sourceFOV;

    [Header("카메라 높이")]
    [SerializeField, Min(0.1f)] private float cameraHeight = 30f;

    // 시야 메시가 조금 낮은 곳에 있어도 잘리지 않도록 촬영 깊이에 여유를 준다.
    [SerializeField, Min(0f)] private float depthPadding = 5f;

    [Header("마스크 카메라가 촬영할 FOV 전용 마스크 레이어")]
    [SerializeField] private LayerMask maskLayer;

    // 마스크 카메라가 촬영한 결과를 저장하는 런타임 render texture
    private RenderTexture maskTexture;

    public RenderTexture MaskTexture => maskTexture;

    // shader에서 마스크 텍스텨를 찾을 때 사용할 이름을
    // 실행 중에 문자열로 검색하지 않도록 정수 ID로 미리 변환한다.
    private static readonly int MaskTexturePropertyId = Shader.PropertyToID("_FOVMaskTexture");

    // 월드 좌표를 마스크 카메라 좌표로 바꾸는 행렬의 이름도 정수 ID로 변환해 보관한다.
    private static readonly int WorldToMaskClipPropertyId = Shader.PropertyToID("_FOVWorldToMaskClip");

    private void Awake()
    {
        if (maskCamera == null)
        {
            TryGetComponent(out maskCamera);

            if (maskCamera == null)
            {
                Debug.LogError(
                    "마스크 카메라가 할당되지 않았습니다. FOVMaskRenderer를 사용하는 GameObject에 Camera 컴포넌트를 추가하거나, maskCamera 필드에 카메라를 할당해주세요.",
                    this);
            }
        }

        ConfigureMaskCamera();
    }

    private void OnEnable()
    {
        CreateMaskTexture();
    }

    private void OnDisable()
    {
        // 컴포넌트가 비활성화 되면 GPU가 사용하는 RenderTexture 자원을 반환한다.
        ReleaseMaskTexture();
    }

    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때도 남아 있을 수 있는 RenderTexture를 정리한다.
        // 이미 OnDisable에서 정리됐다면 내부 null 검사로 아무 작업도 하지 않는다.
        ReleaseMaskTexture();
    }

    private void LateUpdate()
    {
        SynchronizeCamera();

        // 카메라를 먼저 이동시킨 뒤 현재 위치의 변환 정보를 갱신한다.
        PublishMaskDataToShader();
    }

    private void CreateMaskTexture()
    {
        // 이전에 생성된 텍스쳐가 남아있다면 먼저 정리한다.
        ReleaseMaskTexture();

        // URP 카메라가 안정적으로 색상을 출력할 수 있도록
        // 일반적인 4채널 색상 텍스쳐를 만든다.
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
            maskTextureSize, // 텍스쳐의 가로 크기
            maskTextureSize, // 텍스쳐의 세로 크기
            RenderTextureFormat.ARGB32, // URP 카메라와 가장 호환성이 높은 4채널 색상 텍스쳐를 사용한다.
            16 // 깊이 버퍼를 16비트로 설정한다.
        );

        // 마스크 텍스쳐는 안티앨리어싱을 적용하지 않는다. (멀티 샘플링을 사용하는 경우 마스크 경계가 흐려질 수 있다.)
        descriptor.msaaSamples = 1;

        // 마스크는 여러 크기의 축소 이미지를 사용할 필요가 없으므로 mipmap을 생성하지 않는다.
        descriptor.useMipMap = false;

        // mipmap을 만들지 않으므로 mipmap을 자동으로 생성하지 않는다.
        descriptor.autoGenerateMips = false;

        // 마스크는 색상이 아닌 0~1 사이의 값으로만 사용되므로 sRGB 색상 공간을 사용하지 않는다.
        descriptor.sRGB = false;

        // 위에서 정한 규격으로 실제 렌더 텍스처 객체를 만든다.
        maskTexture = new RenderTexture(descriptor)
        {
            name = "FOV Mask Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        // 실제 GPU 메모리에 RenderTexture를 미리 생성한다.
        bool wasCreated = maskTexture.Create();

        if (!wasCreated)
        {
            Debug.LogError("마스크 RenderTexture를 생성하지 못했습니다. 그래픽 장치가 지원하는 최대 텍스쳐 크기를 확인해주세요.", this);

            // 생성 도중 남은 자원을 정리한다.
            ReleaseMaskTexture();

            enabled = false; // 컴포넌트를 비활성화하여 더 이상 Update가 호출되지 않도록 한다.

            return;
        }

        maskCamera.targetTexture = maskTexture;
    }

    private void ReleaseMaskTexture()
    {
        //maskCamera에 연결된 RenderTexture를 해제한다.
        if (maskCamera != null && maskCamera.targetTexture == maskTexture)
            maskCamera.targetTexture = null;

        if (maskTexture == null)
            return;

        // 마스크가 사라진 상태에서는 화면 전체를 보이는 영역으로 처리하도록
        // 기본 흰색 텍스처를 Shader에 전달한다.
        Shader.SetGlobalTexture(
            MaskTexturePropertyId,
            Texture2D.whiteTexture
        );

        // 해제된 카메라 행렬을 계속 사용하지 않도록 기본 행렬로 되돌린다.
        Shader.SetGlobalMatrix(
            WorldToMaskClipPropertyId,
            Matrix4x4.identity
        );

        // RenderTexture가 GPU 메모리에 생성되어 있다면 반환한다.
        if (maskTexture.IsCreated())
            maskTexture.Release();

        // 런타임에 만든 RenderTexture를 파괴한다.
        Destroy(maskTexture);

        // 제거한 객체를 다시 참조하지 않도록 null로 설정한다.
        maskTexture = null;
    }

    private void ConfigureMaskCamera()
    {
        // 메인 카메라가 화면을 합성하기 전에 최신 FOV 마스크가 먼저 렌더링되도록 한다.
        maskCamera.depth = -100f;

        // RenderTexture를 매 프레임 검은색으로 먼저 지운다.
        // FOV 메시가 그러지지 않은 부분은 검은색으로 남아야 하기 때문이다.
        maskCamera.clearFlags = CameraClearFlags.SolidColor;

        // 시야 밖을 뜻하는 값으로 검은색을 사용한다.
        maskCamera.backgroundColor = Color.black;

        // 지정한 FOV 전용 마스크 레이어만 촬영하도록 설정한다.
        maskCamera.cullingMask = maskLayer;

        // 마스크는 밝기 범위가 넓은 HDR이 필요하지 않으므로 HDR을 끈다.
        maskCamera.allowHDR = false;

        // 현재 마스크 경계 처리는 Shader에서 담당할 예정이므로 MSAA를 끈다.
        maskCamera.allowMSAA = false;

        // 마스크 메시 두 개만 촬영할 것이기 때문에 오클루전 컬링을 위한 계산은 필요 없다.
        maskCamera.useOcclusionCulling = false;
    }

    private void SynchronizeCamera()
    {
        if (sourceFOV == null || maskCamera == null)
            return;

        // 대상 탐지와 FOV 메시가 사용하는 중심점의 실제 월드 좌표를 가져온다.
        Vector3 viewOrigion = sourceFOV.ViewOriginPosition;

        // 시야 중심의 바로 위쪽에 카메라를 배치할 위치를 계산한다.
        Vector3 camearaPosiotion = viewOrigion + Vector3.up * cameraHeight;

        maskCamera.transform.position = camearaPosiotion;

        // 카메라가 월드의 아래를 똑바로 바라보도록 회전시킨다.
        maskCamera.transform.rotation = Quaternion.Euler(90f, 0, 0);

        // 거리에 따라 크기가 달라지지 않는 평면 촬영 방식을 사용한다.
        maskCamera.orthographic = true;

        // 카메라 세로 촬영 범위의 절반을 최대 시야 사거리에 맞춘다.
        maskCamera.orthographicSize = sourceFOV.ViewRadius;

        // 카메라 바로 앞에서 시작하여 불필요하게 가까운 영역을 줄인다.
        maskCamera.nearClipPlane = 0.1f;

        // 카메라에서 시야 평면까지의 거리와 여유 공간을 촬영한다.
        maskCamera.farClipPlane = cameraHeight + depthPadding;
    }

    private void PublishMaskDataToShader()
    {
        if (maskTexture == null || !maskTexture.IsCreated())
            return;

        // 카메라의 투영 행렬을 현재 그래픽 장치의 규칙에 맞게 변환한다.
        // 여기서 true는 렌더러 텍스쳐를 목적지로 한다는 것을 의미한다.
        Matrix4x4 gpuProjectionMatrix = GL.GetGPUProjectionMatrix(maskCamera.projectionMatrix, true);

        // 월드 좌표를 마스크 카메라의 최종 화면 좌표로 한 번에 바꾸는 변환 행렬을 만든다.
        Matrix4x4 worldToMaskClipMatrix = gpuProjectionMatrix * maskCamera.worldToCameraMatrix;

        // 현재 마스크 렌더러 텍스쳐를 모든 쉐이더에서 사용할 수 있게 설정한다.
        Shader.SetGlobalTexture(MaskTexturePropertyId, maskTexture);

        // 현재 월드 좌표를 마스크 화면 좌표로 바꿀 행렬도 쉐이더에 전달한다.
        Shader.SetGlobalMatrix(WorldToMaskClipPropertyId, worldToMaskClipMatrix);
    }
}
