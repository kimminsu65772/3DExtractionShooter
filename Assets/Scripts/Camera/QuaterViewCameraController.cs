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
    [SerializeField] private PlayerInputHandler _inputHandler;
    [SerializeField] private PlayerAimController aimController;

    [Header("Quarter View")]
    [SerializeField] private float yaw = 45f;
    [SerializeField] private float pitch = 45f;
    [SerializeField] private float normalDistance = 10f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Aim")]
    [SerializeField] private float aimDistance = 9f;
    [SerializeField] private float aimOffsetDistance = 3f;
    [SerializeField] private float aimMaxWorldDistance = 7f;

    [Header("Follow")]
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private float aimOffsetSmoothTime = 0.08f;

    // SmoothDamp에서는 이전 속도를 바탕으로 새로운 속도를 계산한다. 따라서 이전 속도를 저장할 변수가 필요하다.
    private Vector3 followVelocity;
    private Quaternion cameraRotation;
    private Vector3 viewDirection;

    private Vector3 currentAimOffset = Vector3.zero;
    private Vector3 aimOffsetVelocity = Vector3.zero;

    private void Awake()
    {
        // 카메라 위치 설정 메서드
        CalculateCameraDirection();

        ResolveInputManager();
        ResolveAimController();
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
        if (aimController == null)
        {
            ResolveAimController();
        }

        if (aimController == null)
        {
            return Vector3.zero;
        }

        aimController.RefreshAim();

        if (!aimController.HasValidAimDirection)
        {
            return Vector3.zero;
        }

        // 마우스와 캐릭터 사이의 거리를 aimMaxWorldDistance로 나눈 값을 0~1 사이로 제한하여 offsetRatio를 계산한다.
        float offsetRatio = Mathf.Clamp01(aimController.AimDistance / aimMaxWorldDistance);

        // aimOffsetDistance에 offsetRatio를 곱하여 카메라가 이동할 거리를 계산한다.
        return aimController.AimDirection * aimOffsetDistance * offsetRatio;
    }

    private void ResolveInputManager()
    {
        if (_inputHandler != null || target == null)
        {
            return;
        }

        _inputHandler = target.GetComponent<PlayerInputHandler>();
    }

    private void ResolveAimController()
    {
        if (aimController != null || target == null)
        {
            return;
        }

        aimController = target.GetComponent<PlayerAimController>();
    }
}
