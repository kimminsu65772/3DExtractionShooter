using System.Collections.Generic;
using UnityEngine;

// 적의 실제 행동 결과를 모델의 Animator에 전달한다. 공격이나 장전 자체를 실행하지 않는다.
[DisallowMultipleComponent]
public class EnemyAnimationController : MonoBehaviour
{
    [Header("적의 행동과 모델")]
    [SerializeField] private EnemyRangedBrain brain;
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
        // 실제 발사 성공과 각 장전 시작의 알림을 구독한다.
        if (attackController != null)
        {
            attackController.OnFired += HandleFired;
            attackController.OnReloadStepStarted += HandleReloadStepStarted;
        }
    }

    private void OnDisable()
    {
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
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
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

        if (!CanAnimate || brain == null || motor == null || attackController == null)
            return;

        // 실제 이동 속도를 읽어 조사 위치에서 멈췄을 때도 대기 자세로 돌아간다.
        float moveSpeed = Mathf.Clamp01(motor.HorizontalSpeed / Mathf.Max(0.01f, runAnimationReferenceSpeed));
        animator.SetFloat(MoveSpeedHash, moveSpeed, movementDampTime, Time.deltaTime);
        animator.SetBool(IsAimingHash, brain.CurrentStateType == EnemyStateType.Attack);
        animator.SetBool(IsReloadingHash, attackController.IsReloading);
    }

    private void HandleFired()
    {
        if (!CanAnimate || isPlayingDeath)
            return;

        // 첫 공격 프레임에서도 조준 자세로 들어가도록 LateUpdate보다 먼저 설정한다.
        animator.SetBool(IsAimingHash, true);
        animator.ResetTrigger(FireHash);
        animator.SetTrigger(FireHash);
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
        hasRequiredParameters = false;
        reloadClip = null;
        deathClip = null;
        if (cachedController == null)
            return;

        // NavMeshAgent가 위치를 옮기므로 애니메이션이 중복으로 위치를 움직이지 못하게 한다.
        animator.applyRootMotion = false;
        AnimatorControllerParameter[] parameters = animator.parameters;
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
