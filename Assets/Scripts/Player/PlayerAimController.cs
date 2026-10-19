using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAimController : MonoBehaviour
{
    [Header("참조 설정")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform aimPlanePoint; // 만약에 수평을 맞출 기준 트랜스폼이 필요하다면 연결. 따로 설정이 없다면 플레이어의 중심 위치가 기준이 된다.
    [SerializeField] private PlayerAimCursor aimCursor;

    [Header("조준 설정")]
    [SerializeField] private float aimDeadZone = 0.3f;
    [SerializeField] private LayerMask aimLayer;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];
    private PlayerWeaponHolder weaponHolder;
    private PlayerStateMachine stateMachine;
    private readonly WeaponAdsTiming adsTiming = new WeaponAdsTiming();

    // 조준 자세는 즉시 바뀌어도 수치 보정은 무기의 조준 준비 시간이 끝난 뒤에만 적용한다.
    public bool IsAdsReady
    {
        get
        {
            return RefreshAds();
        }
    }

    private bool RefreshAds()
    {
        if (weaponHolder == null) weaponHolder = GetComponent<PlayerWeaponHolder>();
        if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
        WeaponInstance weapon = weaponHolder != null ? weaponHolder.CurrentWeaponInstance : null;
        bool requested = weapon != null && inputHandler != null && inputHandler.IsAimHeld && !inputHandler.IsUIInputBlocked && stateMachine != null && stateMachine.CurrentState == PlayerActionState.Free;
        // 조준 해제, 행동 제한, 다른 총 선택은 이전 총의 준비 시간을 이어받지 않는다.
        return adsTiming.Evaluate(weapon, requested, Time.time);
    }

    private void OnDisable() { adsTiming.Evaluate(null, false, Time.time); }

    public Vector3 AimWorldPosition { get; private set; }
    public Vector3 AimDirection { get; private set; } = Vector3.forward;
    public float AimDistance { get; private set; }

    public bool HasAimTarget { get; private set; }
    public bool HasValidAimDirection { get; private set; }
    public Vector2 EffectiveAimScreenPosition { get; private set; }

    public void SetCamera(Camera camera)
    {
        mainCamera = camera;
    }

    private void Awake()
    {
        ResolveReferences();
        AimWorldPosition = GetAimPlanePoint();
    }

    private void Update()
    {
        RefreshAds();
        RefreshAim();
    }

    public Vector2 GetScreenAimDirection(Vector3 worldOrigin)
    {
        Vector2 originScreenPos = mainCamera.WorldToScreenPoint(worldOrigin);

        return (EffectiveAimScreenPosition - originScreenPos).normalized;
    }

    public void RefreshAim()
    {
        if (aimCursor != null)
        {
            aimCursor.RefreshCursor();
            EffectiveAimScreenPosition = aimCursor.AimScreenPosition;
        }
        else
        {
            ResolveReferences();
            EffectiveAimScreenPosition = inputHandler != null
                ? inputHandler.AimScreenPosition
                : Vector2.zero;
        }
        
        RefreshAim(EffectiveAimScreenPosition);
    }

    private void RefreshAim(Vector2 aimScreenPosition)
    {
        if (inputHandler == null || mainCamera == null)
        {
            ResolveReferences();
        }

        if (inputHandler == null || mainCamera == null)
        {
            HasValidAimDirection = false;
            AimDistance = 0f;
            AimWorldPosition = GetAimPlanePoint();
            return;
        }

        if (AimTargetHelper.TryGetMouseWorldHit(mainCamera, aimScreenPosition, hitBuffer, aimLayer, out RaycastHit closestHit))
        {
            AimWorldPosition = closestHit.point;
            HasAimTarget = true;
        }
        else
        {
            Vector3 planePoint = GetAimPlanePoint();

            AimWorldPosition = AimTargetHelper.GetMouseWorldPositionOnPlane(
                mainCamera,
                aimScreenPosition,
                planePoint
            );

            HasAimTarget = false;
        }

        Vector3 horizontalVector = AimTargetHelper.GetHorizontalDistance(transform.position, AimWorldPosition);
        AimDistance = horizontalVector.magnitude;

        if (AimDistance <= aimDeadZone)
        {
            HasValidAimDirection = false;
            return;
        }

        AimDirection = horizontalVector / AimDistance;
        HasValidAimDirection = true;
    }

    private Vector3 GetAimPlanePoint()
    {
        return aimPlanePoint != null ? aimPlanePoint.position : transform.position;
    }

    private void ResolveReferences()
    {
        if (inputHandler == null)
        {
            inputHandler = GetComponent<PlayerInputHandler>();
        }

        if (aimCursor == null)
        {
            aimCursor = GetComponent<PlayerAimCursor>();
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }
}
