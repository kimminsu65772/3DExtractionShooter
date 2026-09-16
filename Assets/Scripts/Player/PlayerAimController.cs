using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAimController : MonoBehaviour
{
    [Header("참조 설정")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform aimPlanePoint; // 만약에 수평을 맞출 기준 트랜스폼이 필요하다면 연결. 따로 설정이 없다면 플레이어의 중심 위치가 기준이 된다.

    [Header("조준 설정")]
    [SerializeField] private float aimDeadZone = 0.3f;
    [SerializeField] private LayerMask aimLayer;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    public Vector3 AimWorldPosition { get; private set; }
    public Vector3 AimDirection { get; private set; } = Vector3.forward;
    public float AimDistance { get; private set; }
    public bool HasValidAimDirection { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        AimWorldPosition = GetAimPlanePoint();
    }

    private void Update()
    {
        RefreshAim();
    }

    public void RefreshAim()
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

        

        if (AimTargetHelper.TryGetMouseWorldHit(mainCamera, inputHandler.AimScreenPosition, hitBuffer, aimLayer, out RaycastHit closestHit))
        {
            AimWorldPosition = closestHit.point;
        }
        else
        {
            Vector3 planePoint = GetAimPlanePoint();

            AimWorldPosition = AimTargetHelper.GetMouseWorldPositionOnPlane(
                mainCamera,
                inputHandler.AimScreenPosition,
                planePoint
            );
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

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }
}
