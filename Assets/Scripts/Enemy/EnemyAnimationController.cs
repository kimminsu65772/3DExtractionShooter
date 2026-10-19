using System.Collections.Generic;
using UnityEngine;

// 적의 실제 행동 결과를 모델의 Animator에 전달한다. 공격이나 장전 자체를 실행하지 않는다.
[DisallowMultipleComponent]
public class EnemyAnimationController : MonoBehaviour
{
    [Header("적의 행동과 모델")]
    [SerializeField] private EnemyRangedBrain brain;
    [SerializeField] private EnemyWeaponLoadout weaponLoadout;
    [SerializeField] private EnemyMotor motor;
    [SerializeField] private EnemyAttackController attackController;
    [SerializeField] private Animator animator;
    [SerializeField] private RuntimeAnimatorController animationSet;

    [Header("이동 애니메이션")]
    [SerializeField, Min(0.01f)] private float runAnimationReferenceSpeed = 3.5f;
    [SerializeField, Min(0f)] private float movementDampTime = 0.1f;

    [Header("사망 종료 보호")]
    [SerializeField, Min(0.1f)] private float deathCompletionTimeout = 3f;

    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
    private static readonly int IsReloadingHash = Animator.StringToHash("IsReloading");
    private static readonly int ReloadSpeedHash = Animator.StringToHash("ReloadSpeed");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int FireHash = Animator.StringToHash("Fire");
    private static readonly int ReloadHash = Animator.StringToHash("Reload");

    private RuntimeAnimatorController cachedController;
    private AnimationClip reloadClip;
    private AnimationClip deathClip;
    private bool hasRequiredParameters;
    private bool isPlayingDeath;
    private bool hasCompletedDeath;
    private float deathDeadline;
    private HealthComponent healthComponent;
    private int upperBodyLayerIndex = -1;
    private static readonly int UpperBodyBasePoseHash = Animator.StringToHash("UpperBody.BasePose");
    private UnityEngine.AI.NavMeshAgent navigationAgent;
    private bool hasDirectionalMovement;
    private EnemyDodgeController dodgeController; // [USEC 리뷰] 회피 실행 결과를 읽고 시작·종료 알림을 구독하는 선택적 참조다.
    private bool hasDodgeParameters; // [USEC 리뷰] 이 모델의 Controller가 회피를 지원하는지 보관한다. 일반 적 Controller에는 새 파라미터를 요구하지 않는다.
    private AnimationClip dodgeClip; // [USEC 리뷰] 회피 시간에 재생 길이를 맞출 실제 구르기 클립을 보관한다.
    private static readonly int IsDodgingHash = Animator.StringToHash("IsDodging"); // [USEC 리뷰] 실제 회피 여부를 전달할 Animator bool 이름을 한 번 해시로 만든다.
    private static readonly int DodgeSpeedHash = Animator.StringToHash("DodgeSpeed"); // [USEC 리뷰] 구르기 상태의 재생 속도를 조절할 float 파라미터의 해시다.

    public Animator ModelAnimator => animator;
    private bool CanAnimate => animator != null && animator.isActiveAndEnabled && cachedController != null && hasRequiredParameters;

    private void Awake()
    {
        ResolveReferences();
        if (animator != null && animationSet != null)
            animator.runtimeAnimatorController = animationSet;
        RefreshAnimatorConfiguration();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (dodgeController != null) // [USEC 리뷰] 회피 실행기가 붙은 적에게만 구르기 알림을 연결한다.
        {
            dodgeController.OnDodgeStarted += HandleDodgeStarted; // [USEC 리뷰] 실제 회피가 성공한 순간에만 구르기 자세를 시작한다.
            dodgeController.OnDodgeEnded += HandleDodgeEnded; // [USEC 리뷰] 시간 종료뿐 아니라 중도 취소에도 구르기 자세를 해제한다.
        }
        // 실제 발사 성공과 각 장전 시작의 알림을 구독한다.
        if (attackController != null)
        {
            attackController.OnFired += HandleFired;
            attackController.OnReloadStepStarted += HandleReloadStepStarted;
        }
    }

    private void OnDisable()
    {
        if (dodgeController != null) // [USEC 리뷰] 비활성화될 때 이전 실행기의 이벤트 참조를 남기지 않는다.
        {
            dodgeController.OnDodgeStarted -= HandleDodgeStarted; // [USEC 리뷰] 시작 때 등록한 연출 핸들러를 제거한다.
            dodgeController.OnDodgeEnded -= HandleDodgeEnded; // [USEC 리뷰] 꺼진 Animator가 종료 이벤트를 받아 조작되지 않게 한다.
        }
        if (attackController != null)
        {
            attackController.OnFired -= HandleFired;
            attackController.OnReloadStepStarted -= HandleReloadStepStarted;
        }
    }

    private void Start()
    {
        // 자식 Animator도 초기화를 마친 시점에 한 번 더 확인한다.
        RefreshAnimatorConfiguration();
    }

    private void ResolveReferences()
    {
        // 판단·이동·공격은 적 루트에서, Animator는 교체 가능한 자식 모델에서 찾는다.
        if (brain == null) brain = GetComponent<EnemyRangedBrain>();
        if (motor == null) motor = GetComponent<EnemyMotor>();
        if (attackController == null) attackController = GetComponent<EnemyAttackController>();
        if (weaponLoadout == null) weaponLoadout = GetComponent<EnemyWeaponLoadout>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (healthComponent == null) healthComponent = GetComponent<HealthComponent>();
        if (navigationAgent == null) navigationAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (dodgeController == null) dodgeController = GetComponent<EnemyDodgeController>(); // [USEC 리뷰] 모델 자식이 아니라 AI 루트의 회피 실행기를 찾는다. 없는 일반 적도 정상이다.
    }

    private void LateUpdate()
    {
        // Controller가 바뀔 때만 설정을 다시 읽어 매 프레임 배열을 만들지 않는다.
        if (animator != null && animator.runtimeAnimatorController != cachedController)
            RefreshAnimatorConfiguration();

        if (isPlayingDeath)
        {
            // Animator 정지나 종료 알림 누락으로 죽은 적이 계속 남지 않도록 한다.
            if (!CanAnimate || Time.time >= deathDeadline)
                OnDeathAnimationCompleted();
            return;
        }

        if (!CanAnimate || motor == null || attackController == null)
            return;

        if (hasDodgeParameters) // [USEC 리뷰] USEC Controller처럼 회피 파라미터가 있는 경우에만 이 분기를 실행한다.
        {
            bool dodging = dodgeController != null && dodgeController.IsDodging; // [USEC 리뷰] FSM 상태 이름이 아니라 실행기의 실제 회피 여부를 읽는다.
            animator.SetBool(IsDodgingHash, dodging); // [USEC 리뷰] 이벤트와 별도로 현재 상태를 맞춰 구르기 bool이 오래 남지 않게 한다.
            if (dodging) // [USEC 리뷰] 구르는 동안 일반 이동·조준 연출 갱신을 건너뛴다.
            {
                // 회피 전신 자세 위에 사격용 상체 레이어를 덮지 않는다.
                if (upperBodyLayerIndex >= 0) animator.SetLayerWeight(upperBodyLayerIndex, 0f); // [USEC 리뷰] 총을 드는 상체 레이어가 전신 구르기 포즈를 덮지 않게 한다.
                return; // [USEC 리뷰] 아래 코드가 상체 레이어를 다시 켜거나 조준 bool을 덮어쓰지 못하게 한다.
            }
        }

        // 실제 이동 속도를 읽어 조사 위치에서 멈췄을 때도 대기 자세로 돌아간다.
        float moveSpeed = Mathf.Clamp01(motor.HorizontalSpeed / Mathf.Max(0.01f, runAnimationReferenceSpeed));
        animator.SetFloat(MoveSpeedHash, moveSpeed, movementDampTime, Time.deltaTime);
        if (hasDirectionalMovement)
        {
            // 몸이 바라보는 방향과 이동 방향은 다르므로 실제 속도를 캐릭터 기준으로 변환한다.
            Vector3 velocity = navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh // [USEC 리뷰] 기존 이동 연출이다. Agent가 유효한 경우에만 실제 속도를 읽는다.
                ? navigationAgent.velocity : Vector3.zero; // [USEC 리뷰] 유효하지 않으면 이동 중인 것처럼 보이지 않게 영벡터를 사용한다.
            Vector3 localVelocity = transform.InverseTransformDirection(Vector3.ProjectOnPlane(velocity, Vector3.up)); // [USEC 리뷰] 월드 이동 방향을 몸 기준 좌우·전후로 바꿔 조준하며 옆걸음·뒷걸음을 재생한다.
            Vector2 direction = Vector2.ClampMagnitude(new Vector2(localVelocity.x, localVelocity.z) // [USEC 리뷰] 수평 이동의 몸 기준 x와 z만 2D 블렌드 트리 입력으로 만든다.
                / Mathf.Max(0.01f, runAnimationReferenceSpeed), 1f); // [USEC 리뷰] 기준 속도로 나누고 벡터 길이를 1 이하로 제한해 애니메이션 입력 범위에 맞춘다.
            animator.SetFloat(MoveXHash, direction.x, movementDampTime, Time.deltaTime); // [USEC 리뷰] 몸 기준 좌우 이동 비중을 부드럽게 갱신한다.
            animator.SetFloat(MoveYHash, direction.y, movementDampTime, Time.deltaTime); // [USEC 리뷰] 몸 기준 전진·후진 이동 비중을 부드럽게 갱신한다.
        }
        // 순찰·대기 여부와 관계없이 손에 든 총의 상체 자세를 유지한다.
        // Brain의 실제 조준·공격 판단은 변경하지 않고, 장전은 별도 파라미터가 우선한다.
        bool isAiming = weaponLoadout != null && weaponLoadout.CurrentWeaponInstance != null
            && (healthComponent == null || healthComponent.IsAlive);
        animator.SetBool(IsAimingHash, isAiming);
        animator.SetBool(IsReloadingHash, attackController.IsReloading);

        // 비조준 복귀가 끝나면 상체 덮기를 해제해 이동 클립의 팔 동작도 복원한다.
        if (upperBodyLayerIndex >= 0)
        {
            bool isBasePose = animator.GetCurrentAnimatorStateInfo(upperBodyLayerIndex).fullPathHash == UpperBodyBasePoseHash;
            animator.SetLayerWeight(upperBodyLayerIndex,
                isBasePose && !animator.IsInTransition(upperBodyLayerIndex) ? 0f : 1f);
        }
    }

    private void HandleFired()
    {
        if (!CanAnimate || isPlayingDeath || (dodgeController != null && dodgeController.IsDodging)) // [USEC 리뷰] 회피 중에는 지연된 발사 알림이 오더라도 구르기를 발사 연출로 덮지 않는다.
            return; // [USEC 리뷰] 연출 요청을 거절할 뿐 실제 탄환을 여기서 생성하거나 취소하지는 않는다.

        // 첫 공격 프레임에서도 조준 자세로 들어가도록 LateUpdate보다 먼저 설정한다.
        animator.SetBool(IsAimingHash, true);
        animator.ResetTrigger(FireHash);
        animator.SetTrigger(FireHash);
    }

    private void HandleDodgeStarted() // [USEC 리뷰] 회피 실행 성공 이벤트를 전신 구르기 Animator 상태로 연결한다.
    {
        if (!CanAnimate || !hasDodgeParameters || isPlayingDeath) return; // [USEC 리뷰] 회피를 표시할 수 없는 Controller나 사망 연출은 조작하지 않는다.
        animator.ResetTrigger(FireHash); // [USEC 리뷰] 아직 소비되지 않은 발사 연출 요청을 제거한다.
        animator.ResetTrigger(ReloadHash); // [USEC 리뷰] 회피 전에 예약된 장전 연출이 뒤늦게 시작되지 않게 한다.
        animator.SetBool(IsAimingHash, false); // [USEC 리뷰] 구르는 동안 팔을 뻗어 조준하지 않는다.
        animator.SetBool(IsReloadingHash, false); // [USEC 리뷰] 실제 실행기에서 취소한 재장전 상태를 연출에도 반영한다.
        animator.SetBool(IsDodgingHash, true); // [USEC 리뷰] USEC 전용 AnyState → Dodge 전이 조건을 만족시킨다.
        float length = dodgeClip != null ? dodgeClip.length : dodgeController.Duration; // [USEC 리뷰] 실제 클립 길이를 읽고 없으면 회피 시간과 같은 길이로 간주한다.
        animator.SetFloat(DodgeSpeedHash, length / Mathf.Max(0.01f, dodgeController.Duration)); // [USEC 리뷰] 클립 길이÷목표 시간 배율로 재생해 실제 이동 시간과 구르기 시간을 맞춘다.
        if (upperBodyLayerIndex >= 0) // [USEC 리뷰] 상체 레이어가 있는 Controller에만 다음 처리를 한다.
        {
            animator.Play(UpperBodyBasePoseHash, upperBodyLayerIndex, 0f); // [USEC 리뷰] 회피 후 예전 Fire·Reload 상태로 튀지 않도록 상체를 기본 상태로 돌린다.
            animator.SetLayerWeight(upperBodyLayerIndex, 0f); // [USEC 리뷰] 전신 회피가 보이도록 상체 덮어쓰기의 영향력을 없앤다.
        }
    }

    private void HandleDodgeEnded() // [USEC 리뷰] 회피 종료를 받아 구르기 상태에서 기본 이동 상태로 빠져나오게 한다.
    {
        if (CanAnimate && hasDodgeParameters) // [USEC 리뷰] 여전히 유효한 회피 지원 Controller만 조작한다.
            animator.SetBool(IsDodgingHash, false); // [USEC 리뷰] Dodge → Locomotion의 전이 조건을 만족시킨다. 사격 복귀 여부는 Brain이 다시 결정한다.
    }

    private void HandleReloadStepStarted(float duration)
    {
        if (!CanAnimate || isPlayingDeath)
            return;

        // 클립 길이를 실제 장전 시간에 맞춘다. 샷건은 한 발마다 재생을 다시 시작한다.
        float clipLength = reloadClip != null && reloadClip.length > 0f ? reloadClip.length : duration;
        animator.SetFloat(ReloadSpeedHash, clipLength / Mathf.Max(0.001f, duration));
        animator.SetBool(IsReloadingHash, true);
        animator.ResetTrigger(FireHash);
        animator.ResetTrigger(ReloadHash);
        animator.SetTrigger(ReloadHash);
    }

    public bool TryPlayDeath()
    {
        if (isPlayingDeath)
            return true;

        RefreshAnimatorConfiguration();
        if (!isActiveAndEnabled || !CanAnimate)
            return false;

        isPlayingDeath = true;
        // 화면 밖에서도 사망 상태의 재생 완료 알림이 진행되도록 한다.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.ResetTrigger(FireHash);
        animator.ResetTrigger(ReloadHash);
        animator.SetFloat(MoveSpeedHash, 0f);
        animator.SetBool(IsAimingHash, false);
        animator.SetBool(IsReloadingHash, false);
        animator.SetBool(IsDeadHash, true);
        if (upperBodyLayerIndex >= 0)
            animator.SetLayerWeight(upperBodyLayerIndex, 0f);

        float clipDuration = deathClip != null ? deathClip.length / Mathf.Max(0.01f, Mathf.Abs(animator.speed)) : 0f;
        deathDeadline = Time.time + Mathf.Max(deathCompletionTimeout, clipDuration + 0.5f);
        return true;
    }

    public void OnDeathAnimationCompleted()
    {
        // 공통 Dead 상태의 알림을 한 번만 처리하고 생존 중 잘못 들어온 알림은 무시한다.
        if (!isPlayingDeath || hasCompletedDeath)
            return;

        hasCompletedDeath = true;
        Destroy(brain != null ? brain.gameObject : gameObject);
    }

    private void RefreshAnimatorConfiguration()
    {
        cachedController = animator != null ? animator.runtimeAnimatorController : null;
        upperBodyLayerIndex = animator != null ? animator.GetLayerIndex("UpperBody") : -1;
        hasRequiredParameters = false;
        hasDodgeParameters = false; // [USEC 리뷰] Controller를 교체했을 때 이전 모델의 회피 지원 여부를 재사용하지 않는다.
        dodgeClip = null; // [USEC 리뷰] 이전 Controller의 구르기 길이가 새 모델에 섞이지 않게 한다.
        reloadClip = null;
        deathClip = null;
        if (cachedController == null)
            return;

        // NavMeshAgent가 위치를 옮기므로 애니메이션이 중복으로 위치를 움직이지 못하게 한다.
        animator.applyRootMotion = false; // [USEC 리뷰] 기존 정책이다. 위치는 Agent가 옮기므로 애니메이션 루트 모션으로 이중 이동하지 않는다.
        AnimatorControllerParameter[] parameters = animator.parameters;
        hasDodgeParameters = HasParameter(parameters, IsDodgingHash, AnimatorControllerParameterType.Bool) // [USEC 리뷰] 회피 상태 bool이 이름뿐 아니라 올바른 타입으로 존재하는지 검사한다.
            && HasParameter(parameters, DodgeSpeedHash, AnimatorControllerParameterType.Float); // [USEC 리뷰] 재생 속도 float도 있어야 회피 연출 코드를 켠다.
        hasDirectionalMovement = HasParameter(parameters, MoveXHash, AnimatorControllerParameterType.Float)
            && HasParameter(parameters, MoveYHash, AnimatorControllerParameterType.Float);
        hasRequiredParameters = HasParameter(parameters, MoveSpeedHash, AnimatorControllerParameterType.Float)
            && HasParameter(parameters, IsAimingHash, AnimatorControllerParameterType.Bool)
            && HasParameter(parameters, IsReloadingHash, AnimatorControllerParameterType.Bool)
            && HasParameter(parameters, ReloadSpeedHash, AnimatorControllerParameterType.Float)
            && HasParameter(parameters, IsDeadHash, AnimatorControllerParameterType.Bool)
            && HasParameter(parameters, FireHash, AnimatorControllerParameterType.Trigger)
            && HasParameter(parameters, ReloadHash, AnimatorControllerParameterType.Trigger);

        if (!hasRequiredParameters)
        {
            Debug.LogWarning("적 Animator에 필요한 파라미터가 없다. AI는 유지하고 애니메이션 연결만 중지한다.", this);
            return;
        }

        reloadClip = FindEffectiveClip("Enemy_Reload");
        deathClip = FindEffectiveClip("Enemy_Dead");
        if (hasDodgeParameters) dodgeClip = FindEffectiveClip("Roll"); // [USEC 리뷰] Override가 있다면 교체된 실제 Roll 클립을 찾아 재생 속도 계산에 사용한다.
    }

    private AnimationClip FindEffectiveClip(string slotName)
    {
        // 교체된 실제 클립을 읽어 모델마다 다른 재생 길이에 대응한다.
        if (cachedController is AnimatorOverrideController overrides)
        {
            var clips = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrides.overridesCount);
            overrides.GetOverrides(clips);
            foreach (var pair in clips)
                if (pair.Key != null && pair.Key.name == slotName)
                    return pair.Value != null ? pair.Value : pair.Key;
            return null;
        }

        foreach (AnimationClip clip in cachedController.animationClips)
            if (clip != null && clip.name == slotName)
                return clip;
        return null;
    }

    private static bool HasParameter(AnimatorControllerParameter[] parameters, int hash, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in parameters)
            if (parameter.nameHash == hash && parameter.type == type)
                return true;
        return false;
    }
}
