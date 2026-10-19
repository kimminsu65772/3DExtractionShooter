using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class EnemyRangedBrain : MonoBehaviour, IEnemyAimState
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
    private EnemyCombatMovement combatMovement; // [USEC 리뷰] 선택적 이동 사격 기능이다. 일반 적에는 없으므로 null을 정상적인 경우로 취급한다.
    private EnemyDodgeController dodgeController; // [USEC 리뷰] 선택적 회피 실행 상태를 읽어 FSM과 제어권이 겹치지 않게 한다.

    private readonly Dictionary<EnemyStateType, EnemyState> states = new();

    public EnemyStateType CurrentStateType => enemyStateMachine != null ? enemyStateMachine.CurrentStateType : EnemyStateType.None;
    public bool IsDodging => dodgeController != null && dodgeController.IsDodging; // [USEC 리뷰] 회피 컴포넌트가 없는 적은 항상 false이며 같은 FSM 코드를 그대로 사용한다.
    public bool IsAiming => isActiveAndEnabled && !IsDodging && CurrentStateType == EnemyStateType.Attack; // [USEC 리뷰] Attack 상태가 잠시 남아 있어도 실제 회피 중에는 조준 연출을 끈다.
    public EnemyCombatMovement CombatMovement => combatMovement != null && combatMovement.isActiveAndEnabled ? combatMovement : null; // [USEC 리뷰] 붙어 있고 활성화된 기능만 노출해 Attack 상태가 이동 사격 허용 여부를 판단하게 한다.
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
        combatMovement = GetComponent<EnemyCombatMovement>(); // [USEC 리뷰] 생성 시 한 번 선택적 기능을 찾는다. 일반 적에 없다고 에러를 내지 않는다.
        dodgeController = GetComponent<EnemyDodgeController>(); // [USEC 리뷰] 회피 기능도 한 번 캐시해 Update에서 컴포넌트를 반복 탐색하지 않는다.
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
        if (dodgeController != null) // [USEC 리뷰] 회피 가능한 적만 복귀 알림을 구독한다.
            dodgeController.OnDodgeEnded += HandleDodgeEnded; // [USEC 리뷰] 회피 실행이 끝난 뒤 기존 FSM을 현재 정보로 재평가한다.
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
        if (dodgeController != null) // [USEC 리뷰] Brain이 꺼질 때 회피 중인 상태까지 정리한다.
        {
            dodgeController.OnDodgeEnded -= HandleDodgeEnded; // [USEC 리뷰] 먼저 구독을 끊어 아래 취소 알림이 꺼지는 Brain을 다시 움직이지 않게 한다.
            dodgeController.CancelDodge(); // [USEC 리뷰] 회피 중이었다면 무적과 Agent 설정을 복구한다.
        }
        if (motor != null && combatMovement != null) // [USEC 리뷰] 이동 사격 기능이 있는 적의 감속 설정만 정리한다.
            motor.SetCombatMovement(false); // [USEC 리뷰] 재활성화 이후 기본 이동에도 전투 감속이 남는 것을 막는다.
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
        perception.Tick(Time.deltaTime); // [USEC 리뷰] 기존 호출이다. 회피 차단보다 먼저 두어 회피 도중에도 시야 정보를 갱신한다.

        // 회피 실행기가 Agent와 회전을 소유하는 동안 FSM이 이동·사격을 덮어쓰지 않는다.
        if (IsDodging) return; // [USEC 리뷰] FSM과 공격 타이머를 건너뛰고 위치·회전 제어를 회피 실행기에만 맡긴다. 새 Dodge FSM 상태는 추가하지 않았다.

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

        if (IsDodging) // [USEC 리뷰] 피격 구독자 호출 순서상 회피가 이미 시작됐으면 여기서는 이동 상태를 바꾸지 않는다.
        {
            perception.ReportStimulusPosition(attackerPosition); // [USEC 리뷰] 공격자 위치라는 새 단서는 보존해 회피 종료 후 판단에 사용한다.
            return; // [USEC 리뷰] 조사 Enter 등이 Motor를 조작해 회피를 덮어쓰는 것을 막는다.
        }

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
        dodgeController?.CancelDodge(); // [USEC 리뷰] 사망 처리 전에 회피 무적·Agent 설정을 정리한다. 종료 핸들러는 생존 검사로 재행동을 막는다.
        ChangeState(EnemyStateType.Dead);
    }

    private void HandleDodgeEnded() // [USEC 리뷰] 정상 완료나 중도 취소 뒤 개인 관측을 기준으로 기존 FSM에 다시 합류한다.
    {
        if (!isActiveAndEnabled || healthComponent == null || !healthComponent.IsAlive) return; // [USEC 리뷰] 사망·비활성화 때문에 종료된 경우에는 새 행동을 시작하지 않는다.
        combatMovement?.ResetMovement(); // [USEC 리뷰] 회피 전 전투 위치는 더 이상 최선이 아닐 수 있으므로 다음 공격에서 새로 고른다.
        motor.RefreshPath(); // [USEC 리뷰] 저장된 목적지까지의 경로를 회피 후 현재 위치에서 다시 계산한다.
        // 오래된 공격 자세를 강제로 이어가지 않고 현재 개인 관측으로 다시 결정한다.
        if (perception.IsConfirmed) // [USEC 리뷰] 지금도 플레이어를 직접 보고 있는지 확인한다.
            ChangeState(EnemyStateType.Chase); // [USEC 리뷰] 추적 상태가 현재 거리를 다시 검사하고 사거리 안이면 다음 Tick에서 Attack으로 전환한다.
        else if (perception.HasLastKnownPosition) // [USEC 리뷰] 직접 시야는 잃었지만 마지막 단서가 있는지 검사한다.
            RequestInvestigation(perception.LastKnownPosition); // [USEC 리뷰] 안 보이는 플레이어의 실시간 위치 대신 기억한 위치로 조사를 요청한다.
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
        if (IsDodging) // [USEC 리뷰] 회피 중 소리를 들어도 즉시 이동 명령을 내리지 않는다.
        {
            perception.ReportStimulusPosition(stimulus.Position); // [USEC 리뷰] 소리의 위치만 기억해 회피가 끝난 뒤 조사할 수 있게 한다.
            return; // [USEC 리뷰] 아래 RequestInvestigation이 회피 중 Agent를 조작하지 못하게 한다.
        }
        RequestInvestigation(stimulus.Position);
    }

}
