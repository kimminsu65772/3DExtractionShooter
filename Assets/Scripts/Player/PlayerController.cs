using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private const float MoveInputThreshold = 0.01f;
    private const float DefaultWalkSpeed = 5f;
    private const float DefaultSprintSpeed = 8f;
    private const float DefaultAimMoveSpeedMultiplier = 0.6f;

    [Header("참조 설정")]
    [SerializeField] private PlayerInputHandler _inputHandler;
    [SerializeField] private PlayerAimController aimController;
    [SerializeField] private PlayerWeaponHolder weaponHolder;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerReloadController reloadController;
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private StaminaComponent staminaComponent;
    [SerializeField] private PlayerCarryWeight carryWeight;
    private float carryMovementMultiplier = 1f;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private Transform cameraTransform;

    [Header("바닥 판정 설정")]
    [SerializeField] private Transform groundChecker;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float checkStartOffset = 0.1f;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private float groundCheckDistance = 0.4f;
    [SerializeField] private float groundSnapDistance = 0.2f;

    [Header("플레이어 이동 속도 설정")]
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float fallMultiplier = 2f;

    [Header("플레이어 행동 소비 스태미너 설정")]
    [SerializeField] private float sprintStaminaCostPerSecond = 10f;
    [SerializeField] private float dodgeStaminaCost = 20f;

    [Header("플레이어 회피 설정")]
    [SerializeField, Min(0.01f)] private float dodgeSpeed = 10f;
    [SerializeField, Min(0.01f)] private float dodgeDuration = 0.5f;
    [SerializeField, Min(0f)] private float dodgeCooldown = 0.5f;
    [SerializeField] private AnimationCurve dodgeSpeedCurve = DodgeMotion.CreateOriginalSpeedCurve();
    [SerializeField, Min(0.01f)] private float dodgeExitAcceleration = 50f;

    [Header("플레이어 회전 설정")]
    [SerializeField] private float rotationSpeed = 12f;

    [Header("플레이어 이동 소음 설정")]
    [SerializeField] private float walkNoiseRadius = 3f;
    [SerializeField] private float sprintNoiseRadius = 7f;
    [SerializeField] private float aimMoveNoiseRadius = 2f;
    [SerializeField] private float dodgeNoiseRadius = 5f;

    [Header("플레이어 상태 변화 확인")]
    [SerializeField] private float currentMoveSpeed;
    [SerializeField] private float _verticalVelocity;
    [SerializeField] private PlayerLocomotionState playerLocomotionState = PlayerLocomotionState.None;


    // 회피를 시작할 때 확정한 이동 방향
    private Vector3 dodgeDirection;

    // 현재 회피가 얼마나 진행됐는지 기록하는 시간
    private float dodgeElapsedTime;

    // 회피가 끝난 후 다음 회피를 시작할 수 있는 시간
    private float nextDodgeTime;

    // 구르기 직후에만 일반 이동으로 속도를 이어 주며 평소 이동에는 관성을 추가하지 않는다.
    private Vector3 dodgeExitVelocity;
    private bool hasDodgeExitVelocity;

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

    public PlayerLocomotionState PlayerLocomotionState => playerLocomotionState;

    public Vector3 CurrentMoveDirection { get; private set; }

    public float CurrentMoveSpeed => currentMoveSpeed;
    public float DodgeDuration => Mathf.Max(0.01f, dodgeDuration);

    public void SetCamera(Transform cameraTransform)
    {
        this.cameraTransform = cameraTransform;
    }

    private bool IsAimActive => stateMachine != null && stateMachine.CurrentState == PlayerActionState.Free && _inputHandler.IsAimHeld;
    private bool IsAttackActive => stateMachine != null && stateMachine.CurrentState == PlayerActionState.Free && _inputHandler.IsAttackHeld;

    public bool ShouldFOVFollowCharacter
    {
        get
        {
            if (stateMachine.CurrentState == PlayerActionState.Dodge)
                return false;

            if (IsAimActive || IsAttackActive)
                return false;

            return playerLocomotionState == PlayerLocomotionState.Sprint;
        }
    }

    private void Awake()
    {
        // 씬의 참조가 비어 있어도 같은 플레이어에 붙은 스태미너를 찾아 행동 소비에 사용한다.
        if (staminaComponent == null) staminaComponent = GetComponent<StaminaComponent>();
        // 손에 든 무기의 이동 보정을 읽을 참조를 한 번만 찾는다.
        if (weaponHolder == null) weaponHolder = GetComponent<PlayerWeaponHolder>();
        if (carryWeight == null) carryWeight = GetComponent<PlayerCarryWeight>();
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

        if (stateMachine == null)
        {
            Debug.LogError("상태 머신의 참조 설정이 완료되지 않았습니다.");
            return;
        }

        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }

        if (healthComponent == null)
        {
            healthComponent = GetComponent<HealthComponent>();
        }

        if (reloadController == null)
        {
            reloadController = GetComponent<PlayerReloadController>();
        }

        if (characterStats == null)
        {
            Debug.LogWarning("CharacterStats 참조가 없어 기본 이동 속도를 사용합니다.", this);
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
        bool dodgeRequested = _inputHandler.ConsumeDodgeInput();

        if (stateMachine.CurrentState == PlayerActionState.Dodge)
        {
            UpdateDodge();
            return;
        }

        if (dodgeRequested && TryStartDodge())
        {
            UpdateDodge();
            return;
        }

        // 회피 경로에는 적용하지 않는다. 일반 이동에만 소지 무게의 최종 배율을 적용한다.
        carryMovementMultiplier = carryWeight != null ? carryWeight.MovementMultiplier : 1f;
        UpdateLocomotionState();
        MoveCharacter();
    }

    private void UpdateLocomotionState()
    {
        bool hasMoveInput = _inputHandler.MoveInput.sqrMagnitude > MoveInputThreshold * MoveInputThreshold;

        // 먼저 이동 입력이 없는 경우를 처리한다. 이동 입력이 없으면 Idle 상태로 전환한다.
        if (!hasMoveInput || carryMovementMultiplier <= 0f)
        {
            // idle 상태에서 조준 중이면 AimIdle 상태로 전환한다.
            if (IsAimActive)
            {
                playerLocomotionState = PlayerLocomotionState.AimIdle;
                return;
            }
            playerLocomotionState = PlayerLocomotionState.Idle;
            return;
        }
        // 이동 입력이 있는 경우를 처리한다.
        else
        {
            // 이동 입력이 있는 경우, 조준 중이면 AimMove 상태로 전환한다.
            if (IsAimActive)
            {
                playerLocomotionState = PlayerLocomotionState.AimMove;
                return;
            }
            // 이동 입력이 있는 경우, 스프린트 입력이 있고 스태미너가 충분하면 Sprint 상태로 전환한다.
            if (_inputHandler.IsSprintHeld &&
                staminaComponent != null && staminaComponent.CanSprint &&
                staminaComponent.TryConsumeSprintStamina(sprintStaminaCostPerSecond * Time.deltaTime))
            {
                playerLocomotionState = PlayerLocomotionState.Sprint;
                return;
            }
            // 그 외의 경우에는 Move 상태로 전환한다.
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
        WeaponDefinition heldWeapon = weaponHolder != null ? weaponHolder.CurrentWeapon?.Definition : null;
        float weaponMultiplier = heldWeapon != null ? heldWeapon.movementSpeedMultiplier : 1f;
        switch (playerLocomotionState)
        {
            case PlayerLocomotionState.Idle:
            case PlayerLocomotionState.AimIdle:
                return 0f;

            case PlayerLocomotionState.Move:
                return GetStatValue(StatType.WalkSpeed, DefaultWalkSpeed) * weaponMultiplier;

            case PlayerLocomotionState.Sprint:
                return GetStatValue(StatType.SprintSpeed, DefaultSprintSpeed) * weaponMultiplier;

            case PlayerLocomotionState.AimMove:
                // 빈손에서는 기존의 조심스러운 걸음을 유지한다. 총은 ADS 준비 완료 뒤에만 조준 이동 보정을 받는다.
                float aimMultiplier = GetStatValue(StatType.AimMoveSpeedMultiplier, DefaultAimMoveSpeedMultiplier);
                if (heldWeapon != null)
                    aimMultiplier = aimController != null && aimController.IsAdsReady ? heldWeapon.adsMovementSpeedMultiplier * aimMultiplier / DefaultAimMoveSpeedMultiplier : 1f;
                return GetStatValue(StatType.WalkSpeed, DefaultWalkSpeed) * weaponMultiplier * aimMultiplier;

            default:
                return 0f;
        }
    }

    private float GetStatValue(StatType statType, float fallbackValue)
    {
        return characterStats != null ? Mathf.Max(0f, characterStats.GetValue(statType, fallbackValue)) : fallbackValue;
    }

    private void MoveCharacter()
    {
        Vector3 moveDirection = carryMovementMultiplier > 0f ? CalculateMoveDirection() : Vector3.zero;
        currentMoveSpeed = GetCurrentMoveSpeed() * carryMovementMultiplier;

        RefreshGroundProbe(moveDirection);

        if (slopeProbe.hasGround && moveDirection.sqrMagnitude > 0.001f)
        {
            moveDirection = Vector3.ProjectOnPlane(moveDirection, slopeProbe.groundNormal.normalized);
        }

        Vector3 planarVelocity = moveDirection * currentMoveSpeed;
        if (hasDodgeExitVelocity)
        {
            if (stateMachine.CurrentState != PlayerActionState.Free || carryMovementMultiplier <= 0f)
            {
                hasDodgeExitVelocity = false;
                dodgeExitVelocity = Vector3.zero;
            }
            else
            {
                // 과적 배율은 잔여 속도에도 한 번만 적용한다. 이동 불가 상태에는 잔여 이동도 없다.
                Vector3 targetVelocity = moveDirection * GetCurrentMoveSpeed();
                dodgeExitVelocity = Vector3.MoveTowards(dodgeExitVelocity, targetVelocity, dodgeExitAcceleration * Time.deltaTime);
                planarVelocity = dodgeExitVelocity * carryMovementMultiplier;
                currentMoveSpeed = planarVelocity.magnitude;
                moveDirection = currentMoveSpeed > 0.001f ? planarVelocity / currentMoveSpeed : Vector3.zero;
                if ((dodgeExitVelocity - targetVelocity).sqrMagnitude < 0.0001f)
                    hasDodgeExitVelocity = false;
            }
        }

        CurrentMoveDirection = moveDirection;

        UpdateVerticalVelocity();

        Vector3 velocity = planarVelocity + Vector3.up * _verticalVelocity;
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
    private bool TryProbeGround(Vector3 origin, float distance, bool isRay,  out GroundProbeState result, float radius = 0f)
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

        TryProbeGround(centerPos, groundCheckDistance, false, out GroundProbeState slopeProbeResult, groundCheckRadius);
        slopeProbe = slopeProbeResult;
        TryProbeGround(centerPos, groundCheckDistance, true, out GroundProbeState centerProbeResult);
        centerProbe = centerProbeResult;

        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            TryProbeGround(forwardPos, groundCheckDistance, true, out GroundProbeState forwardProbeResult);
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
        bool shouldFaceMoveDirection =
            playerLocomotionState == PlayerLocomotionState.Sprint &&
            !IsAimActive &&
            !IsAttackActive;

        if (shouldFaceMoveDirection)
        {
            RotateTowards(moveDirection);
            return;
        }

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
    }

    private void RotateTowards(Vector3 direction)
    {
        // y축 기준으로 위 아래 방향을 제거하여 수직 회전을 방지한다.
        direction.y = 0f;

        // 이동 입력이 없을 때 회전을 방지하기 위해 이동 입력의 크기를 확인한다.
        if (direction.sqrMagnitude < MoveInputThreshold * MoveInputThreshold)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private bool TryStartDodge()
    {
        PlayerActionState actionState = stateMachine.CurrentState;

        // 행동에 어떤 제약도 없는 Free 상태나 행동 취소가 가능한 Reload 상태에서만 회피를 시작할 수 있다.
        if (actionState != PlayerActionState.Free && actionState != PlayerActionState.Reload)
        {
            return false;
        }

        if (Time.time < nextDodgeTime)
        {
            return false;
        }

        // 현재 입력을 카메라 기준으로 변환하여 회피 방향을 계산한다.
        Vector3 requestedDirection = CalculateMoveDirection();

        // 만약 어떤 입력도 없으면 캐릭터가 바라보는 방향으로 회피한다.
        if (requestedDirection.sqrMagnitude < MoveInputThreshold * MoveInputThreshold)
            requestedDirection = transform.forward;

        // 회피 방향으로 실제로 이동할 수 있는 지면이 있는지 확인한다.
        RefreshGroundProbe(requestedDirection);

        // 캐릭터가 지면에 서있거나, 캐릭터 중심 아래에 지면이 있거나, 회피 방향에 경사면 지면이 있는 경우에만 회피를 허용한다.
        bool canDodge = characterController.isGrounded || centerProbe.hasGround || slopeProbe.hasGround;
        bool hasEnoughStamina = staminaComponent != null && canDodge && staminaComponent.TryUseStamina(dodgeStaminaCost);

        if (!canDodge)
            return false;
        if (!hasEnoughStamina)
            return false;

        // 경사면에서 회피할 때, 회피 방향을 경사면에 맞춰서 이동하도록 보정한다.
        if (slopeProbe.hasGround)
        {
            requestedDirection = Vector3.ProjectOnPlane(requestedDirection, slopeProbe.groundNormal.normalized);
        }

        // 회피 방향을 정규화하여 단위 벡터로 만든다.
        dodgeDirection = requestedDirection.normalized;

        dodgeElapsedTime = 0f;
        hasDodgeExitVelocity = false;
        dodgeExitVelocity = Vector3.zero;

        Vector3 lookDirection = dodgeDirection;

        lookDirection.y = 0f;

        // 플레이어가 회피를 시작할 때 회피 방향으로 캐릭터가 회전하도록 한다.
        if (lookDirection.sqrMagnitude > MoveInputThreshold * MoveInputThreshold)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        if (actionState == PlayerActionState.Reload)
        {
            if (reloadController != null)
            {
                reloadController.CancelReload();
            }
        }

        if (!stateMachine.TryChangeState(PlayerActionState.Dodge))
            return false;

        healthComponent?.SetInvulnerable(true);
        return true;
    }

    private void UpdateDodge()
    {
        // 기본 회피 속도에 진행률별 배율을 곱한다. 일반 이동·장비·과적 배율은 사용하지 않는다.
        float safeDodgeDuration = Mathf.Max(dodgeDuration, 0.01f);

        // 마지막 프레임에 회피 시간을 초과해서 이동하지 않도록 나머지 회피 시간을 계산한다.
        float remainingDodgeTime = Mathf.Max(safeDodgeDuration - dodgeElapsedTime, 0f);

        // 이번 프레임에서 이동할 시간을 계산한다.
        float stepTime = Mathf.Min(Time.deltaTime, remainingDodgeTime);

        RefreshGroundProbe(dodgeDirection);

        Vector3 moveDirection = dodgeDirection;

        if (slopeProbe.hasGround)
        {
            moveDirection = Vector3.ProjectOnPlane(dodgeDirection, slopeProbe.groundNormal).normalized;
        }

        CurrentMoveDirection = moveDirection;

        currentMoveSpeed = DodgeMotion.EvaluateSpeed(dodgeSpeed, dodgeSpeedCurve, dodgeElapsedTime / safeDodgeDuration);

        // 회피 중에도 중력 법칙을 유지한다.
        UpdateVerticalVelocity();

        Vector3 velocity = moveDirection * currentMoveSpeed + Vector3.up * _verticalVelocity;

        // 회피 중에는 CharacterController.Move를 사용하여 이동하여 벽을 통과하지 않도록 한다.
        characterController.Move(velocity * stepTime);

        dodgeElapsedTime += stepTime;

        if (dodgeElapsedTime >= safeDodgeDuration)
        {
            EndDodge();
        }
    }

    private void EndDodge()
    {
        // 원본처럼 달리기 속도를 넘긴 뒤 다음 일반 이동에서 현재 입력 속도에 합류시킨다.
        WeaponDefinition heldWeapon = weaponHolder != null ? weaponHolder.CurrentWeapon?.Definition : null;
        float runSpeed = GetStatValue(StatType.SprintSpeed, DefaultSprintSpeed) * (heldWeapon != null ? heldWeapon.movementSpeedMultiplier : 1f);
        dodgeExitVelocity = dodgeDirection * runSpeed;
        hasDodgeExitVelocity = true;
        dodgeElapsedTime = 0f;
        dodgeDirection = Vector3.zero;
        currentMoveSpeed = runSpeed * (carryWeight != null ? carryWeight.MovementMultiplier : 1f);

        nextDodgeTime = Time.time + dodgeCooldown;

        healthComponent?.SetInvulnerable(false);
        stateMachine.TryChangeState(PlayerActionState.Free);
    }

    private void OnDisable()
    {
        hasDodgeExitVelocity = false;
        dodgeExitVelocity = Vector3.zero;
        healthComponent?.SetInvulnerable(false);
    }

    private void OnValidate()
    {
        dodgeSpeed = Mathf.Max(0.01f, dodgeSpeed);
        dodgeDuration = Mathf.Max(0.01f, dodgeDuration);
        dodgeCooldown = Mathf.Max(0f, dodgeCooldown);
        dodgeExitAcceleration = Mathf.Max(0.01f, dodgeExitAcceleration);
        if (dodgeSpeedCurve == null || dodgeSpeedCurve.length == 0)
            dodgeSpeedCurve = DodgeMotion.CreateOriginalSpeedCurve();
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
