using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("참조 설정")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerWeaponHolder weaponHolder;

    [Header("애니메이션 클립")]
    [SerializeField] private AnimationClip reloadClip;

    [Header("애니메이션 보간 시간")]
    [SerializeField] private float animationBlendTime = 0.1f;

    // 매 프레임 문자열 이름을 검색하지 않도록 미리 해시값을 계산하여 저장해둔다.
    private static readonly int MoveXHash =
        Animator.StringToHash("MoveX");

    private static readonly int MoveYHash =
        Animator.StringToHash("MoveY");

    private static readonly int MoveAmountHash =
        Animator.StringToHash("MoveAmount");

    private static readonly int IsSprintingHash =
        Animator.StringToHash("IsSprinting");

    private static readonly int IsAimingHash =
        Animator.StringToHash("IsAiming");

    private static readonly int ActionStateHash =
        Animator.StringToHash("ActionState");

    private static readonly int FireHash =
        Animator.StringToHash("Fire");

    private static readonly int IsFiringHash =
        Animator.StringToHash("IsFiring");

    private static readonly int ReloadSpeedHash =
        Animator.StringToHash("ReloadSpeed");

    private static readonly int DodgeSpeedHash = Animator.StringToHash("DodgeSpeed");
    private bool hasDodgeSpeedParameter;
    private AnimationClip dodgeClip;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }

        if (inputHandler == null)
        {
            inputHandler = GetComponent<PlayerInputHandler>();
        }

        if (stateMachine == null)
        {
            stateMachine = GetComponent<PlayerStateMachine>();
        }

        if (weaponHolder == null)
        {
            weaponHolder = GetComponent<PlayerWeaponHolder>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (playerController == null || inputHandler == null || stateMachine == null || animator == null)
        {
            Debug.LogError("PlayerAnimationController: 필요한 컴포넌트가 할당되지 않았습니다.", this);

            enabled = false;
            return;
        }

        ConfigureAnimationEventRelay();
        CacheDodgeAnimation();
    }

    private void OnEnable()
    {
        if (stateMachine == null || animator == null)
            return;

        stateMachine.OnStateChanged += HandleActionStateChanged;

        animator.SetInteger(ActionStateHash, (int)stateMachine.CurrentState);
        UpdateDodgeAnimationSpeed();
    }

    private void OnDisable()
    {
        if (stateMachine == null)
        {
            return;
        }

        // 비활성화된 컴포넌트가 계속 이벤트를 받는 일을 방지한다.
        stateMachine.OnStateChanged -= HandleActionStateChanged;
    }

    private void LateUpdate()
    {
        // 플레이어 컨트롤러에서 확정한 이동 방향을 애니메이터에 전달한다.
        UpdateMovementParameters();

        // 총을 손에 든 동안 기본 상체 자세를 유지한다. 실제 ADS 입력·수치와는 별개다.
        // 장전·회피·피격·사망 동작이 끝나 Free로 돌아오면 다시 총을 드는 자세를 사용한다.
        bool holdGunPose = stateMachine.CurrentState == PlayerActionState.Free
            && weaponHolder != null && weaponHolder.CurrentWeaponInstance != null;
        animator.SetBool(IsAimingHash, holdGunPose);

        bool canPlayFireAnimation = stateMachine.CurrentState == PlayerActionState.Free;
        animator.SetBool(IsFiringHash, inputHandler.IsAttackHeld && canPlayFireAnimation);
    }

    private void UpdateMovementParameters()
    {
        // 경사면의 높이 방향이 애니메이션에 반영되면 안 되므로 제거한다.
        Vector3 planarMoveDirection = playerController.CurrentMoveDirection;
        planarMoveDirection.y = 0f;

        // 월드 방향을 캐릭터가 향하는 로컬 방향으로 변환한다.
        Vector3 localMoveDirection = playerController.transform.InverseTransformDirection(planarMoveDirection);

        float moveAmount = Mathf.Clamp01(planarMoveDirection.magnitude);

        animator.SetFloat(MoveXHash, localMoveDirection.x, animationBlendTime, Time.deltaTime);

        animator.SetFloat(MoveYHash, localMoveDirection.z, animationBlendTime, Time.deltaTime);

        animator.SetFloat(MoveAmountHash, moveAmount, animationBlendTime, Time.deltaTime);

        bool isSprinting = playerController.PlayerLocomotionState == PlayerLocomotionState.Sprint;

        animator.SetBool(IsSprintingHash, isSprinting);
    }

    private void HandleActionStateChanged(PlayerActionState previousState, PlayerActionState currentState)
    {
        // 새로 확정된 행동 상태를 정수 매개변수로 전달한다.
        animator.SetInteger(ActionStateHash, (int)currentState);
        if (currentState == PlayerActionState.Dodge)
            UpdateDodgeAnimationSpeed();

        if (currentState != PlayerActionState.Free)
        {
            animator.ResetTrigger(FireHash);
            animator.SetBool(IsFiringHash, false);
        }
    }

    public void PlayFireAnimation()
    {
        // Animator가 없다면 애니메이션을 요청할 수 없다.
        if (animator == null)
            return;

        // 이전에 소비되지 않고 남아 있을 가능성이 있는 Trigger를 먼저 지운다.
        animator.ResetTrigger(FireHash);

        // Animator의 Any State → Fire 전환을 시작시킨다.
        animator.SetTrigger(FireHash);
    }

    public void SetReloadAnimationSpeed(float reloadDuration)
    {
        if (animator == null)
            return;

        if (reloadClip == null || reloadDuration <= 0f)
        {
            animator.SetFloat(ReloadSpeedHash, 1f);
            return;
        }

        float animationSpeed = reloadClip.length / reloadDuration;
        animator.SetFloat(ReloadSpeedHash, animationSpeed);
    }

    private void CacheDodgeAnimation()
    {
        // 기존 Controller는 새 파라미터를 강제하지 않는다. 지원하는 오리 Controller만 배속을 맞춘다.
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == DodgeSpeedHash && parameter.type == AnimatorControllerParameterType.Float)
                hasDodgeSpeedParameter = true;

        if (!hasDodgeSpeedParameter || animator.runtimeAnimatorController == null)
            return;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == "Roll")
            {
                dodgeClip = clip;
                break;
            }
    }

    private void UpdateDodgeAnimationSpeed()
    {
        if (hasDodgeSpeedParameter && dodgeClip != null && playerController != null)
            animator.SetFloat(DodgeSpeedHash, dodgeClip.length / playerController.DodgeDuration);
    }

    private void ConfigureAnimationEventRelay()
    {
        PlayerAnimationEventRelay eventRelay = animator.GetComponent<PlayerAnimationEventRelay>();

        if (eventRelay == null)
        {
            eventRelay = animator.gameObject.AddComponent<PlayerAnimationEventRelay>();
        }

        eventRelay.Initialize(GetComponent<PlayerAttackController>());
    }

    
}
