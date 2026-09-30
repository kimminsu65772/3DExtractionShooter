using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class EnemyRangedBrain : MonoBehaviour
{
    [Header("디버그")]
    [SerializeField] private bool showStateLog = true;
    
    [Header("판단에 필요한 기능")]
    [SerializeField] private EnemyPerception perception;
    [SerializeField] private HealthComponent healthComponent;

    [Header("이동에 필요한 기능")]
    [SerializeField] private EnemyMotor motor;

    [Header("공격에 필요한 기능")]
    [SerializeField] private EnemyAttackController attackController;

    [Header("공격 상태 관련 설정")]
    [SerializeField, Min(0)] private float attackEnterDistance = 5f;
    [SerializeField, Min(0)] private float attackExitDistance = 10f;
    [SerializeField, Range(0f, 1f)] private float headAimChance = 0.15f;

    [Header("상태 설정")]
    [SerializeField, Min(0f)] private float chaseStoppingDistance = 5f;
    [SerializeField, Min(0f)] private float investigateStoppingDistance = 0.2f;
    [SerializeField, Min(0f)] private float investigationWaitDuration = 3f;
    [SerializeField, Min(0f)] private float targetLostGraceDuration = 0.35f;

    [Header("기타 설정")]
    [SerializeField] private HitPartProvider hitPartProvider;

    private EnemyStateMachine enemyStateMachine;

    private readonly Dictionary<EnemyStateType, EnemyState> states = new();

    public EnemyStateType CurrentStateType => enemyStateMachine != null ? enemyStateMachine.CurrentStateType : EnemyStateType.None;
    public EnemyPerception Perception => perception;
    public EnemyMotor Motor => motor;
    public float ChaseStoppingDistance => chaseStoppingDistance;
    public float InvestigateStoppingDistance => investigateStoppingDistance;
    public float InvestigationWaitDuration => investigationWaitDuration;
    public float TargetLostGraceDuration => targetLostGraceDuration;
    public float AttackEnterDistance => attackEnterDistance;
    public float AttackExitDistance => attackExitDistance;
    public float HeadAimChance => headAimChance;
    public EnemyAttackController AttackController => attackController;
    public HitPartProvider HitPartProvider => hitPartProvider;

    private void Awake()
    {
        enemyStateMachine = new EnemyStateMachine();
        // 일반 원거리 적이 사용할 상태 객체를 한 번씩 생성해 등록한다.
        states.Add(
            EnemyStateType.Idle,
            new EnemyIdleState(this)
        );

        states.Add(
            EnemyStateType.Investigate,
            new EnemyInvestigateState(this)
        );

        states.Add(
            EnemyStateType.Chase,
            new EnemyChaseState(this)
        );

        states.Add(
            EnemyStateType.Attack,
            new EnemyAttackState(this)
        );

        states.Add(
            EnemyStateType.Dead,
            new EnemyDeadState(this)
        );

        if (perception == null)
        {
            perception = GetComponent<EnemyPerception>();
        }

        if (perception == null)
        {
            Debug.LogError("EnemyRangedBrain에 EnemyPerception이 필요합니다.", this);
            enabled = false;
            return;
        }

        if (motor == null)
            motor = GetComponent<EnemyMotor>();

        if (motor == null)
        {
            Debug.LogError("EnemyRangedBrain에 EnemyMotor가 필요합니다.", this);
            enabled = false;
            return;
        }

        if (attackController == null)
            attackController = GetComponent<EnemyAttackController>();

        if (attackController == null)
        {
            Debug.LogError(
                "EnemyRangedBrain에 EnemyAttackController가 필요합니다.",
                this
            );

            enabled = false;
            return;
        }

        if (healthComponent == null)
            healthComponent = GetComponent<HealthComponent>();

        if (healthComponent == null)
        {
            Debug.LogError(
                "EnemyRangedBrain에 HealthComponent가 필요합니다.",
                this
            );
            enabled = false;
            return;
        }
    }

    private void OnValidate()
    {
        attackEnterDistance = Mathf.Max(0f, attackEnterDistance);
        attackExitDistance = Mathf.Max(attackEnterDistance, attackExitDistance);
    }

    private void OnEnable()
    {
        if (enemyStateMachine != null)
        {
            enemyStateMachine.OnStateChanged -= HandleStateChanged;
            enemyStateMachine.OnStateChanged += HandleStateChanged;
        }

        if (healthComponent != null)
        {
            healthComponent.OnDamageTaken -= HandleDamageTaken;
            healthComponent.OnDeath -= HandleDeath;
            healthComponent.OnDamageTaken += HandleDamageTaken;
            healthComponent.OnDeath += HandleDeath;
        }

        if (perception != null)
        {
            perception.OnSoundHeard -= HandleSoundHeard;
            perception.OnSoundHeard += HandleSoundHeard;
        }
    }

    private void OnDisable()
    {
        if (enemyStateMachine != null)
            enemyStateMachine.OnStateChanged -= HandleStateChanged;

        if (healthComponent != null)
        {
            healthComponent.OnDamageTaken -= HandleDamageTaken;
            healthComponent.OnDeath -= HandleDeath;
        }

        if (perception != null)
            perception.OnSoundHeard -= HandleSoundHeard;

    }

    private void Start()
    {
        // 항상 초기 상태는 Idle 상태로 설정
        enemyStateMachine.Initialize(states[EnemyStateType.Idle]);
    }

    private void Update()
    {
        if (CurrentStateType == EnemyStateType.Dead)
            return;

        // 감지 정보를 먼저 갱신해야 현재 상태가 최신 결과를 사용할 수 있다.
        perception.Tick(Time.deltaTime);

        // 발사 간격과 재장전 완료 시점을 갱신한다.
        attackController.Tick();

        enemyStateMachine.Tick();
    }

    private void HandleStateChanged(EnemyStateType previousState, EnemyStateType currentState)
    {
        if (!showStateLog)
            return;

        Debug.Log($"[{name}] Enemy State: {previousState} → {currentState}", this);
    }

    public void ChangeState(EnemyStateType newStateType)
    {
        if (!states.TryGetValue(newStateType, out var newState))
        {
            Debug.LogError($"[{name}] EnemyRangedBrain: 상태 {newStateType}를 찾을 수 없습니다.", this);
            return;
        }
        
        if (!enemyStateMachine.TryChangeState(newState))
        {
            Debug.LogWarning($"[{name}] EnemyRangedBrain: 상태 {newStateType}로 전환할 수 없습니다.", this);
        }
    }

    private void HandleDamageTaken(DamageInfo damageInfo)
    {
        if (healthComponent.IsDead || CurrentStateType == EnemyStateType.Dead)
            return;

        GameObject attacker = damageInfo.attacker;

        if (attacker == null)
            return;

        Vector3 attackerPosition = attacker.transform.position;   

        // 공격자가 확인되지 않은 상태라면 조사 상태로 전환
        if (!perception.IsConfirmed)
        {
            RequestInvestigation(attackerPosition);
            return;
        }

        perception.ReportStimulusPosition(attackerPosition);

        // 공격자가 확인된 상태라면 공격자와의 수평 거리만 계산하여 공격 상태로 전환할지 추적 상태로 전환할지 결정
        Vector3 horizontalOffset = Vector3.ProjectOnPlane(attackerPosition - transform.position, Vector3.up);

        EnemyStateType nextState = horizontalOffset.sqrMagnitude <= attackEnterDistance * attackEnterDistance
            ? EnemyStateType.Attack
            : EnemyStateType.Chase;

        if (nextState != CurrentStateType)
            ChangeState(nextState);
    }

    private void HandleDeath(DamageInfo damageInfo)
    {
        ChangeState(EnemyStateType.Dead);
    }

    private void RequestInvestigation(Vector3 stimulusPosition)
    {
        perception.ReportStimulusPosition(stimulusPosition);

        if (CurrentStateType == EnemyStateType.Investigate)
        {
            motor.TryMoveTo(stimulusPosition, investigateStoppingDistance, true);

            return;
        }

        if (CurrentStateType != EnemyStateType.Investigate)
            ChangeState(EnemyStateType.Investigate);
    }

    private void HandleSoundHeard(SoundStimulus stimulus)
    {
        if (CurrentStateType == EnemyStateType.Dead || perception.IsConfirmed)
            return;
        RequestInvestigation(stimulus.Position);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        EnemyPerception currentPerception = perception != null
            ? perception
            : GetComponent<EnemyPerception>();

        if (currentPerception == null)
            return;

        Vector3 viewOriginPosition = currentPerception.ViewOriginPosition;
        Vector3 horizontalForward = Vector3.ProjectOnPlane(currentPerception.ViewForward, Vector3.up).normalized;

        if (horizontalForward.sqrMagnitude < 0.0001f)
            horizontalForward = transform.forward;

        DrawViewRange(
            viewOriginPosition,
            horizontalForward,
            currentPerception.SuspiciousDistance,
            currentPerception.ViewAngle,
            new Color(1f, 0.85f, 0.1f, 1f),
            "인지 범위"
        );

        DrawViewRange(
            viewOriginPosition,
            horizontalForward,
            currentPerception.ConfirmedDistance,
            currentPerception.ViewAngle,
            new Color(0.1f, 0.8f, 1f, 1f),
            "추적 전환 범위"
        );

        Vector3 investigationCenter = currentPerception.HasLastKnownPosition
            ? currentPerception.LastKnownPosition
            : transform.position;

        DrawDistanceRange(
            investigationCenter,
            investigateStoppingDistance,
            new Color(0.3f, 1f, 0.4f, 1f),
            "조사 도착 범위",
            Vector3.left
        );

        DrawDistanceRange(
            transform.position,
            attackEnterDistance,
            new Color(1f, 0.2f, 0.2f, 1f),
            "공격 진입 범위",
            Vector3.back
        );

        DrawDistanceRange(
            transform.position,
            attackExitDistance,
            new Color(1f, 0.3f, 0.85f, 1f),
            "공격 이탈 범위",
            Vector3.right
        );
    }

    private static void DrawViewRange(
        Vector3 center,
        Vector3 forward,
        float radius,
        float angle,
        Color color,
        string label)
    {
        if (radius <= 0f || angle <= 0f)
            return;

        Vector3 leftDirection = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up) * forward;
        Vector3 rightDirection = Quaternion.AngleAxis(angle * 0.5f, Vector3.up) * forward;

        Handles.color = color;
        Handles.DrawWireArc(center, Vector3.up, leftDirection, angle, radius);
        Handles.DrawLine(center, center + leftDirection * radius);
        Handles.DrawLine(center, center + rightDirection * radius);
        Handles.Label(center + forward * radius, $"{label}: {radius:0.##}m");
    }

    private static void DrawDistanceRange(
        Vector3 center,
        float radius,
        Color color,
        string label,
        Vector3 labelDirection)
    {
        if (radius <= 0f)
            return;

        Handles.color = color;
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.Label(center + labelDirection.normalized * radius, $"{label}: {radius:0.##}m");
    }
#endif
}
