using UnityEngine;

public class QuaterViewCameraController : MonoBehaviour
{
    private enum CameraState
    {
        Normal,
        Aiming
    }

    // 디버깅용 카메라 상태 노출
    [Header("카메라 설정")]
    [SerializeField] private CameraState currentState = CameraState.Normal;

    [Header("참조 설정")]
    [SerializeField] private Transform target;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PlayerInputHandler _inputHandler;

    [Header("Quarter View")]
    [SerializeField] private float yaw = 45f;
    [SerializeField] private float pitch = 45f;
    [SerializeField] private float normalDistance = 10f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Aim")]
    [SerializeField] private float aimDistance = 9f;
    [SerializeField] private float aimOffsetDistance = 3f;
    [SerializeField] private float aimMaxWorldDistance = 7f;
    [SerializeField] private float aimDeadZone = 0.3f;

    [Header("Follow")]
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private float aimOffsetSmoothTime = 0.08f;

    // SmoothDamp에서는 이전 속도를 바탕으로 새로운 속도를 계산한다. 따라서 이전 속도를 저장할 변수가 필요하다.
    private Vector3 followVelocity;
    private Quaternion cameraRotation;
    private Vector3 viewDirection;
    private Vector3 lastAimDirection = Vector3.forward;

    private Vector3 currentAimOffset = Vector3.zero;
    private Vector3 aimOffsetVelocity = Vector3.zero;

    private void Awake()
    {
        // 카메라 위치 설정 메서드
        CalculateCameraDirection();

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        ResolveInputManager();
    }

    // 인스펙터에서 값이 변경될 때마다 카메라 위치를 재설정
    private void OnValidate()
    {
        CalculateCameraDirection();
    }

    private void Update()
    {
        if (_inputHandler == null) return;

        // 조준 상태에 따른 카메라 상태 전환
        currentState = _inputHandler.IsAimHeld ? CameraState.Aiming : CameraState.Normal;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        UpdateCameraPosition(currentState);
    }

    // 카메라의 회전 방향을 계산
    private void CalculateCameraDirection()
    {
        cameraRotation = Quaternion.Euler(pitch, yaw, 0f);
        viewDirection = cameraRotation * Vector3.forward;
    }

    // 카메라 상태에 따라 카메라 위치를 업데이트
    private void UpdateCameraPosition(CameraState state)
    {
        Vector3 lookAtPoint = target.position + targetOffset;
        Vector3 targetAimOffset = Vector3.zero;
        float targetDistance = normalDistance;

        if (state == CameraState.Aiming)
        {
            targetAimOffset = GetAimOffset();
            targetDistance = aimDistance;
        }

        currentAimOffset = Vector3.SmoothDamp(currentAimOffset, targetAimOffset, ref aimOffsetVelocity, aimOffsetSmoothTime);
        lookAtPoint += currentAimOffset;

        Vector3 targetPosition = lookAtPoint - viewDirection * targetDistance;

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref followVelocity, smoothTime);
        transform.rotation = cameraRotation;
    }

    private Vector3 GetAimOffset()
    {
        // 마우스 커서의 월드 좌표를 가져오자.
        Vector3 mouseWorldPos = GetMouseWorldPos();

        // 마우스와 캐릭터 사이의 방향 벡터를 계산
        Vector3 mouseDirection = (mouseWorldPos - target.position);
        mouseDirection.y = 0f; // 수평 방향만 고려

        // 마우스와 캐릭터 사이의 거리를 계산
        float distanceToMouse = mouseDirection.magnitude;

        // 마우스가 캐릭터와 너무 가까이 있을 때 똑같이 1로 정규화하는 경우 카메라가 튕겨저 나가는 현상이 발생할 수 있다. 따라서 일정 거리 이하에서는 0을 반환하도록 한다.
        if (distanceToMouse <= aimDeadZone)
        {
            return lastAimDirection * 0f;
        }

        // 마우스 방향 벡터를 거리에 따라 정규화한다.
        lastAimDirection = mouseDirection / distanceToMouse;

        // 마우스와 캐릭터 사이의 거리를 aimMaxWorldDistance로 나눈 값을 0~1 사이로 제한하여 offsetRatio를 계산한다.
        float offsetRatio = Mathf.Clamp01(distanceToMouse / aimMaxWorldDistance);

        // aimOffsetDistance에 offsetRatio를 곱하여 카메라가 이동할 거리를 계산한다.
        return lastAimDirection * aimOffsetDistance * offsetRatio;
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 screenPosition = _inputHandler != null ? _inputHandler.AimScreenPosition : Vector3.zero;
        return AimTargetHelper.GetMouseWorldPositionOnPlane(mainCamera, screenPosition, target.position);
    }

    private void ResolveInputManager()
    {
        if (_inputHandler != null || target == null)
        {
            return;
        }

        _inputHandler = target.GetComponent<PlayerInputHandler>();
    }
}
