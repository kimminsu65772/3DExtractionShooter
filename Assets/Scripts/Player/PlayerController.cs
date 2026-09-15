using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private const float MoveInputThreshold = 0.01f;

    [Header("참조 설정")]
    [SerializeField] private PlayerInputHandler _inputHandler;
    [SerializeField] private PlayerAimController aimController;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform cameraTransform;

    [Header("바닥 판정 설정")]
    [SerializeField] private Transform groundChecker;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float checkStartOffset = 0.1f;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private float groundCheckDistance = 0.4f;
    [SerializeField] private float groundSnapDistance = 0.2f;

    [Header("플레이어 이동 속도 설정")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float fallMultiplier = 2f;

    [Header("플레이어 회전 설정")]
    [SerializeField] private float rotationSpeed = 12f;

    [Header("플레이어 상태 변화 확인")]
    [SerializeField] private float currentMoveSpeed;
    [SerializeField] private float _verticalVelocity;
    [SerializeField] private PlayerLocomotionState playerLocomotionState = PlayerLocomotionState.None;


    private float forwardProbeDistance;
    private readonly RaycastHit[] _groundProbeHits = new RaycastHit[8];

    private struct GroundProbeState
    {
        public bool hasGround;
        public Vector3 groundNormal;
        public float groundDistance;
    }

    private GroundProbeState slopeProbe;
    private GroundProbeState centerProbe;
    private GroundProbeState forwardProbe;

    private void Awake()
    {
        if (_inputHandler == null)
        {
            Debug.LogError("인풋 핸들러의 참조 설정이 완료되지 않았습니다.");
            return;
        }
            

        if (characterController == null)
        {
            Debug.LogError("캐릭터 컨트롤러의 참조 설정이 완료되지 않았습니다.");
            return;
        }

        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        ResolveAimController();

        forwardProbeDistance = characterController.radius + 0.05f;
    }

    void Update()
    {
        UpdateLocomotionState();
        MoveCharacter();
    }

    private void UpdateLocomotionState()
    {
        bool hasMoveInput = _inputHandler.MoveInput.sqrMagnitude > MoveInputThreshold * MoveInputThreshold;

        if (!hasMoveInput)
        {
            playerLocomotionState = PlayerLocomotionState.Idle;
            return;
        }
        else
        {
            if (_inputHandler.IsSprintHeld)
            {
                playerLocomotionState = PlayerLocomotionState.Sprint;
                return;
            }
            else
            {
                playerLocomotionState = PlayerLocomotionState.Move;
            }
        }
    }

    private Vector3 CalculateMoveDirection()
    {
        Vector3 moveDirection = Vector3.zero;

        bool hasMoveInput = _inputHandler.MoveInput.sqrMagnitude > MoveInputThreshold * MoveInputThreshold;

        if (!hasMoveInput) return moveDirection;

        // Vector3.ProjectOnPlane를 사용하여 카메라의 forward와 right 벡터를 y축(수직 방향)으로 투영하여
        // 평면상에서의 이동 방향을 계산한다.
        Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        moveDirection = cameraForward * _inputHandler.MoveInput.y + 
            cameraRight * _inputHandler.MoveInput.x;

        return Vector3.ClampMagnitude(moveDirection, 1f);
    }

    private float GetCurrentMoveSpeed()
    {
        switch (playerLocomotionState)
        {
            case PlayerLocomotionState.Idle:
                return 0f;
            case PlayerLocomotionState.Move:
                return moveSpeed;
            case PlayerLocomotionState.Sprint:
                return sprintSpeed;
            default:
                return 0f;
        }
    }

    private void MoveCharacter()
    {
        Vector3 moveDirection = CalculateMoveDirection();
        currentMoveSpeed = GetCurrentMoveSpeed();

        RefreshGroundProbe(moveDirection);



        if (slopeProbe.hasGround && moveDirection.sqrMagnitude > 0.001f)
        {
            moveDirection = Vector3.ProjectOnPlane(moveDirection, slopeProbe.groundNormal);
        }

        UpdateVerticalVelocity();

        Vector3 velocity = moveDirection * currentMoveSpeed + Vector3.up * _verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
        RotateCharacter(moveDirection);
    }

    private void UpdateVerticalVelocity()
    {
        bool isFallingOrGrounded = _verticalVelocity <= 0f;

        bool isStandingOnGround = centerProbe.hasGround && centerProbe.groundDistance <= groundSnapDistance;

        bool canStepDown = forwardProbe.hasGround && forwardProbe.groundDistance <= characterController.stepOffset;

        if (isFallingOrGrounded && isStandingOnGround)
        {
            _verticalVelocity = -2f;
        }
        else if (isFallingOrGrounded && canStepDown)
        {
            _verticalVelocity = -5f;
        }
        else
        {
            _verticalVelocity += gravity * fallMultiplier * Time.deltaTime;
        }
    }
    private bool TryProbeGround(Vector3 origin, float distance, bool isRay, float radius, out GroundProbeState result)
    {
        result = new GroundProbeState
        {
            hasGround = false,
            groundNormal = Vector3.up,
            groundDistance = float.MaxValue,
        };

        int hitCount = 0;

        if (isRay)
        {
            hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, _groundProbeHits, distance, groundLayer, QueryTriggerInteraction.Ignore);
        }
        else
        {
            hitCount = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _groundProbeHits, distance, groundLayer, QueryTriggerInteraction.Ignore);
        }

        if (hitCount <= 0)
            return false;

        if (!TryFindClosestWalkableHit(hitCount, out RaycastHit closestHit))
            return false;

        result.hasGround = true;
        result.groundNormal = closestHit.normal;
        result.groundDistance = closestHit.distance;

        return true;
    }

    private bool TryFindClosestWalkableHit(int hitCount, out RaycastHit closestHit)
    {
        closestHit = new RaycastHit();
        float minGroundY = Mathf.Cos(characterController.slopeLimit * Mathf.Deg2Rad);
        int closestHitIndex = -1;
        float closestHitDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _groundProbeHits[i];
            if (hit.collider == null)
                continue;
            if (hit.normal.y < minGroundY)
                continue;
            if (_groundProbeHits[i].distance < closestHitDistance)
            {
                closestHitDistance = _groundProbeHits[i].distance;
                closestHitIndex = i;
            }
        }
        if (closestHitIndex < 0)
            return false;
        closestHit = _groundProbeHits[closestHitIndex];
        return true;
    }

    private void RefreshGroundProbe(Vector3 moveDirection)
    {
        Vector3 centerPos = groundChecker.position + Vector3.up * checkStartOffset;
        Vector3 forwardPos = centerPos + moveDirection.normalized * forwardProbeDistance;

        TryProbeGround(centerPos, groundCheckDistance, false, groundCheckRadius, out GroundProbeState slopeProbeResult);
        slopeProbe = slopeProbeResult;
        TryProbeGround(centerPos, groundCheckDistance, true, groundCheckRadius, out GroundProbeState centerProbeResult);
        centerProbe = centerProbeResult;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            TryProbeGround(forwardPos, groundCheckDistance, true, groundCheckDistance, out GroundProbeState forwardProbeResult);
            forwardProbe = forwardProbeResult;
        }
        else
        {
            forwardProbe = default;
        }

        return;
    }

    private void RotateCharacter(Vector3 moveDirection)
    {
        if (_inputHandler.IsAimHeld)
        {
            if (aimController == null)
            {
                ResolveAimController();
            }

            if (aimController == null)
            {
                return;
            }

            aimController.RefreshAim();

            if (!aimController.HasValidAimDirection)
            {
                return;
            }

            RotateTowards(aimController.AimDirection);
            return;
        }

        RotateTowards(moveDirection);
    }

    private void RotateTowards(Vector3 direction)
    {
        // y축 회전을 방지하기 위해 y축 성분을 0으로 설정
        direction.y = 0f;

        // 이동 입력이 없을 때 회전을 방지하기 위해 이동 입력의 크기를 확인한다.
        if (direction.sqrMagnitude < MoveInputThreshold * MoveInputThreshold)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void ResolveAimController()
    {
        if (aimController != null)
        {
            return;
        }

        aimController = GetComponent<PlayerAimController>();
    }
    private void OnDrawGizmosSelected()
    {
        if (groundChecker != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundChecker.position, groundCheckRadius);

            Vector3 moveDirection = CalculateMoveDirection();

            Vector3 centerPos = groundChecker.position + Vector3.up * checkStartOffset;
            Vector3 forwardPos = centerPos + moveDirection.normalized * forwardProbeDistance;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(centerPos, centerPos + Vector3.down * groundCheckDistance);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(forwardPos, forwardPos + Vector3.down * groundSnapDistance);
        }
    }
}
