using ExtractionShooter.AI;
using UnityEngine;
using System;
using UnityEngine.AI;
using ExtractionShooter.DataPipeline;

public class SquadMemberBrain : MonoBehaviour, IEnemyAimState
{
    [SerializeField] private CombatDataCompositionRoot dataRoot;
    [SerializeField] private EnemyPerception perception;
    [SerializeField] private SquadPerceptionLink perceptionLink;
    [SerializeField] private EnemyMotor motor;
    [SerializeField] private EnemyAttackController attackController;
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private float investigateStopDistance = 0.2f;
    [SerializeField, Min(0f)] private float attackDistance = 10f;
    [SerializeField, Range(0f, 1f)] private float headAimChance = 0.15f;

    [Header("교전 거리 조절")]
    [SerializeField, Min(0.1f)] private float approachStartDistance = 5.5f;
    [SerializeField, Min(0.1f)] private float approachStopDistance = 4.5f;
    [SerializeField, Min(0.1f)] private float retreatStartDistance = 3f;
    [SerializeField, Min(0.1f)] private float retreatStopDistance = 4f;
    [SerializeField, Min(0.02f)] private float combatPositionRefreshInterval = 0.2f;
    [SerializeField, Min(0.1f)] private float combatPositionSampleRadius = 0.75f;

    [Header("지원 위치 유지")]
    [Tooltip("지원 위치와 공유 관측 위치 사이의 최소 수평 거리. 개인 교전 거리와 별개로 적용한다.")]
    [SerializeField, Min(0f)] private float supportThreatClearance = 1.8f;
    [SerializeField, Min(0.1f)] private float supportMoveThreshold = 0.8f;
    [SerializeField, Min(0.01f)] private float supportStopThreshold = 0.25f;

    [Header("도착 후 경계")]
    [SerializeField, Min(0f)] private float guardDuration = 3f;
    [SerializeField, Range(0f, 180f)] private float guardLookAngle = 60f;

    private bool recoveryRequested;
    private float recoveryElapsed;
    private enum CombatMovement { Hold, Approach, Retreat }
    private CombatMovement combatMovement;
    private NavMeshAgent navigationAgent;
    private NavMeshPath combatPath;
    private readonly Vector3[] combatPathCorners = new Vector3[64];
    private Vector3 combatDestination;
    private bool hasCombatDestination;
    private float nextCombatPositionTime;
    private bool isSupportMoving;
    private bool hasSupportDestination;
    private Vector3 supportDestination;
    private float nextSupportRefreshTime;
    private SquadRearPositionPlanner rearPositionPlanner;
    private bool defensiveOnly;
    private float guardElapsed;
    private bool isGuarding;
    private Vector3 guardForward;
    private SquadObservation investigatedObservation;
    private MedicineDefinition aedKitDefinition;
    private MedicineInventoryItem aedKit;

    private ReactiveSelector tree;
    AimPointProvider aimPointProvider;
    private HitPart aimedHitPart;
    private SquadCommand currentCommand;

    public bool HasCommand { get; private set; }
    public SquadCommand CurrentCommand => currentCommand;
    public bool IsAiming { get; private set; }
    public float CombatStartDistance => approachStartDistance;
    public float CombatStopDistance => approachStopDistance;
    public float SupportThreatClearance => supportThreatClearance;
    public NavMeshAgent NavigationAgent => navigationAgent;
    public bool IsAlive => healthComponent != null && healthComponent.IsAlive;
    public bool HasRearPosition => hasSupportDestination;
    // 분대 개인의 타겟 발견 여부를 제공한다.
    public bool HasConfirmedTarget => perception != null && perception.Target != null && perception.IsConfirmed;
    public Vector3 RearPosition => supportDestination;
    public DestructibleCover AssignedRearCover { get; private set; }
    public event Action<SquadMemberBrain, SquadCommand> OnCommandCompleted;

    // 부트스트랩이나 스포너가 최초 활성화 전에 데이터 루트를 전달한다.
    public void SetDataRoot(CombatDataCompositionRoot root)
    {
        dataRoot = root;
    }

    private void Awake()
    {
        navigationAgent = GetComponent<NavMeshAgent>();
        rearPositionPlanner = GetComponentInParent<SquadRearPositionPlanner>();
        combatPath = new NavMeshPath();
        if (perception != null)
            aimPointProvider = perception.TargetAimPointProvider;

        SelectNextAimPart();

        // 조사 행동을 별도의 시퀀스로 확장한다.
        Sequence investigateSequence = new Sequence(
                new EligibleAction(() => perceptionLink.HasSharedObservation, Investigate, StopAction),
                new EligibleAction(() => true, Guard, StopGuard)
            );

        tree = new ReactiveSelector(
                new EligibleAction(CanAttack, MaintainCombatDistance, StopCombatMovement),
                new EligibleAction(() => HasCommand && CurrentCommand.Type == SquadCommandType.Support, CommandSupport, StopSupportMovement),
                new EligibleAction(() => (HasCommand && CurrentCommand.Type == SquadCommandType.HoldPosition), CommandHoldPosition, StopAction),
                new EligibleAction(() => (HasCommand && (CurrentCommand.Type == SquadCommandType.MoveTo 
                || CurrentCommand.Type == SquadCommandType.Regroup 
                || CurrentCommand.Type == SquadCommandType.ReturnToBase)), 
                CommandMoveTo, StopAction),
                new EligibleAction(() => perceptionLink.HasSharedObservation 
                && ((HasCommand && CurrentCommand.Type == SquadCommandType.Investigate) || defensiveOnly), 
                investigateSequence.Tick, 
                investigateSequence.Abort),
                new EligibleAction(CanRecover, Recover, StopRecovery),
                new EligibleAction(() => true, Idle, StopAction)
            );
    }

    private void OnEnable()
    {
        if (healthComponent != null)
            healthComponent.OnDamageTaken += HandleDamageTaken;
    }

    private void OnValidate()
    {
        retreatStartDistance = Mathf.Max(0.1f, retreatStartDistance);
        retreatStopDistance = Mathf.Max(retreatStartDistance + 0.1f, retreatStopDistance);
        approachStopDistance = Mathf.Max(retreatStopDistance, approachStopDistance);
        approachStartDistance = Mathf.Max(approachStopDistance + 0.1f, approachStartDistance);
        supportThreatClearance = Mathf.Max(0f, supportThreatClearance);
        supportMoveThreshold = Mathf.Max(supportStopThreshold + 0.1f, supportMoveThreshold);
    }

    private NodeStatus MaintainCombatDistance()
    {
        Vector3 targetPosition = perception.Target.position;
        Vector3 away = Vector3.ProjectOnPlane(transform.position - targetPosition, Vector3.up);
        float distanceSqr = away.sqrMagnitude;

        // 시작·종료 거리를 분리해 경계 부근에서 이동과 정지가 반복되는 것을 막는다.
        if (distanceSqr < retreatStartDistance * retreatStartDistance)
            SetCombatMovementMode(CombatMovement.Retreat);
        else if (combatMovement == CombatMovement.Retreat)
        {
            if (distanceSqr >= retreatStopDistance * retreatStopDistance)
                SetCombatMovementMode(CombatMovement.Hold);
        }
        else if (combatMovement == CombatMovement.Approach)
        {
            if (distanceSqr <= approachStopDistance * approachStopDistance)
                SetCombatMovementMode(CombatMovement.Hold);
        }
        else if (distanceSqr > approachStartDistance * approachStartDistance)
            SetCombatMovementMode(CombatMovement.Approach);

        if (HasCommand && CurrentCommand.Type == SquadCommandType.Support && combatMovement != CombatMovement.Retreat)
        {
            // 지원 중에는 플레이어를 향한 개인 접근 대신 지원 위치를 따른다.
            SetCombatMovementMode(CombatMovement.Hold);
            return CommandSupport();
        }

        if (combatMovement != CombatMovement.Retreat && HasCommand)
        {
            if (CurrentCommand.Type == SquadCommandType.HoldPosition)
            {
                SetCombatMovementMode(CombatMovement.Hold);
                return CommandHoldPosition();
            }
            if (CurrentCommand.Type == SquadCommandType.MoveTo &&
                Vector3.ProjectOnPlane(CurrentCommand.Destination - targetPosition, Vector3.up).sqrMagnitude >=
                approachStopDistance * approachStopDistance)
            {
                SetCombatMovementMode(CombatMovement.Hold);
                return CommandMoveTo();
            }
        }

        if (defensiveOnly && combatMovement == CombatMovement.Approach)
            SetCombatMovementMode(CombatMovement.Hold);

        if (combatMovement == CombatMovement.Hold)
        {
            motor.Stop();
            return NodeStatus.Running;
        }

        if (Time.time >= nextCombatPositionTime)
        {
            nextCombatPositionTime = Time.time + combatPositionRefreshInterval;
            Vector3 direction = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
            hasCombatDestination = TryFindCombatDestination(targetPosition, direction);
            if (!hasCombatDestination)
                motor.Stop();
        }

        // 이동할 수 없어도 교전을 유지해 하위 명령·조사로 넘어가지 않는다.
        if (hasCombatDestination && !motor.TryMoveTo(combatDestination, investigateStopDistance))
        {
            hasCombatDestination = false;
            motor.Stop();
        }
        return NodeStatus.Running;
    }

    private NodeStatus CommandSupport()
    {
        Transform target = CurrentCommand.FollowTarget;

        // 대상이 없어졌다면 기존 목적지로 계속 이동하지 않는다.
        // 명령 해제와 전술 변경은 분대장이 처리한다.
        if (target == null || !motor.IsReady)
        {
            StopSupportMovement();
            return NodeStatus.Running;
        }

        Vector3 desiredPosition =
            target.position + CurrentCommand.FollowOffset;

        // 지원 대상은 계속 움직이지만 경로 검사는 일정 간격으로 제한한다.
        if (Time.time >= nextSupportRefreshTime)
        {
            nextSupportRefreshTime =
                Time.time + combatPositionRefreshInterval;

            hasSupportDestination = false;
            AssignedRearCover = null;

            if (rearPositionPlanner != null && perceptionLink.HasSharedObservation)
            {
                hasSupportDestination = rearPositionPlanner.TryGetPosition(this, target,
                    CurrentCommand.FollowOffset, perceptionLink.LatestSharedObservation.Position,
                    out supportDestination, out DestructibleCover selectedCover);
                AssignedRearCover = selectedCover;
            }
            else
            {
                var filter = new NavMeshQueryFilter
                {
                    agentTypeID = navigationAgent.agentTypeID,
                    areaMask = navigationAgent.areaMask
                };
                if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit,
                    combatPositionSampleRadius, filter))
                {
                    float supportThreatClearanceSqr = supportThreatClearance * supportThreatClearance;
                    bool safeDistance = !perceptionLink.HasSharedObservation ||
                        Vector3.ProjectOnPlane(hit.position - perceptionLink.LatestSharedObservation.Position, Vector3.up).sqrMagnitude >=
                        supportThreatClearanceSqr;
                    if (safeDistance && NavMesh.CalculatePath(navigationAgent.nextPosition, hit.position, filter, combatPath) &&
                        combatPath.status == NavMeshPathStatus.PathComplete)
                    {
                        supportDestination = hit.position;
                        hasSupportDestination = true;
                    }
                }
            }

            // 경로가 막혔을 때 이전 목적지로 계속 걷지 않는다.
            if (!hasSupportDestination)
                motor.Stop();
        }

        if (!hasSupportDestination)
            return NodeStatus.Running;

        float distanceSqr = Vector3.ProjectOnPlane(supportDestination - transform.position, Vector3.up).sqrMagnitude;
        if (distanceSqr <= supportStopThreshold * supportStopThreshold ||
            !isSupportMoving && distanceSqr < supportMoveThreshold * supportMoveThreshold)
        {
            // 선정한 엄폐 위치를 기준으로 정지한다. 이상적인 대형 위치와 비교하면 엄폐 옆에서 계속 왕복한다.
            isSupportMoving = false;
            motor.Stop();
            return NodeStatus.Running;
        }

        isSupportMoving = true;
        if (!motor.TryMoveTo(supportDestination, supportStopThreshold))
        {
            hasSupportDestination = false;
            AssignedRearCover = null;
            rearPositionPlanner?.Release(this);
            motor.Stop();
        }

        return NodeStatus.Running;
    }

    private NodeStatus Guard()
    {
        motor.Stop();
        if (!isGuarding)
        {
            // 도착 방향을 한 번만 저장해 좌우 회전이 매 프레임 누적되지 않게 한다.
            guardForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            isGuarding = true;
        }

        guardElapsed += Time.deltaTime;
        if (guardElapsed >= guardDuration)
        {
            StopGuard();
            perceptionLink.RequestDiscardObservation(investigatedObservation);
            return NodeStatus.Success;
        }

        // 기존 경계 시간 안에서 정면 → 오른쪽 → 왼쪽 → 정면 순서로 살핀다.
        float lookAngle = Mathf.Sin(guardElapsed / guardDuration * Mathf.PI * 2f) * guardLookAngle;
        Vector3 lookDirection = Quaternion.AngleAxis(lookAngle, Vector3.up) * guardForward;
        motor.TryFaceTowards(transform.position + lookDirection);

        return NodeStatus.Running;
    }

    private void StopGuard()
    {
        guardElapsed = 0f;
        isGuarding = false;
        motor.SetAutomaticRotation(true);
    }

    private void SetCombatMovementMode(CombatMovement mode)
    {
        if (combatMovement == mode)
            return;
        combatMovement = mode;
        hasCombatDestination = false;
        nextCombatPositionTime = 0f;
        motor.Stop();
    }

    private bool TryFindCombatDestination(Vector3 targetPosition, Vector3 awayDirection)
    {
        if (navigationAgent == null || !motor.IsReady)
            return false;

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = navigationAgent.agentTypeID,
            areaMask = navigationAgent.areaMask
        };
        bool retreating = combatMovement == CombatMovement.Retreat;
        // Agent의 정지 여유 때문에 목표 앞에서 멈춰도 거리 종료 조건에 도달하도록 보정한다.
        float desiredDistance = retreating
            ? retreatStopDistance + investigateStopDistance + 0.05f
            : Mathf.Max(0.1f, approachStopDistance - investigateStopDistance - 0.05f);
        float currentDistanceSqr = Vector3.ProjectOnPlane(transform.position - targetPosition, Vector3.up).sqrMagnitude;

        // 뒤가 막혔다면 대각선 뒤·측면도 검사한다. 탐색은 일정 간격으로 제한한다.
        int candidateCount = retreating ? 5 : 1;
        for (int i = 0; i < candidateCount; i++)
        {
            float angle = i == 0 ? 0f : ((i + 1) / 2) * 45f * (i % 2 == 1 ? 1f : -1f);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * awayDirection;
            Vector3 candidate = targetPosition + direction * desiredDistance;
            candidate.y = transform.position.y;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                combatPositionSampleRadius, filter))
                continue;

            float candidateDistanceSqr = Vector3.ProjectOnPlane(hit.position - targetPosition, Vector3.up).sqrMagnitude;
            if (retreating && candidateDistanceSqr <= currentDistanceSqr)
                continue;
            if (!retreating && candidateDistanceSqr > approachStopDistance * approachStopDistance)
                continue;
            if (!NavMesh.CalculatePath(navigationAgent.nextPosition, hit.position, filter, combatPath)
                || combatPath.status != NavMeshPathStatus.PathComplete)
                continue;

            if (retreating)
            {
                int count = combatPath.GetCornersNonAlloc(combatPathCorners);
                if (count >= combatPathCorners.Length)
                    continue;
                bool approachesTarget = false;
                for (int j = 1; j < count; j++)
                {
                    Vector3 segmentStart = combatPathCorners[j - 1];
                    Vector3 segment = Vector3.ProjectOnPlane(combatPathCorners[j] - segmentStart, Vector3.up);
                    Vector3 toTarget = Vector3.ProjectOnPlane(targetPosition - segmentStart, Vector3.up);
                    float t = segment.sqrMagnitude > 0.0001f
                        ? Mathf.Clamp01(Vector3.Dot(toTarget, segment) / segment.sqrMagnitude) : 0f;
                    float nearestDistanceSqr = (toTarget - segment * t).sqrMagnitude;
                    if (nearestDistanceSqr < currentDistanceSqr - 0.1f)
                    {
                        approachesTarget = true;
                        break;
                    }
                }
                if (approachesTarget)
                    continue;
            }

            combatDestination = hit.position;
            return true;
        }
        return false;
    }

    private void StopCombatMovement()
    {
        combatMovement = CombatMovement.Hold;
        hasCombatDestination = false;
        nextCombatPositionTime = 0f;
        // 이동만 종료한다. 보관 중인 분대 명령은 지우지 않는다.
        motor.Stop();
    }

    private void OnDisable()
    {
        rearPositionPlanner?.Release(this);
        tree?.Abort();
        IsAiming = false;
        if (motor != null)
        {
            motor.SetCombatMovement(false);
            motor.SetAutomaticRotation(true);
        }

        if (healthComponent != null)
            healthComponent.OnDamageTaken -= HandleDamageTaken;
    }

    private void Update()
    {
        if (healthComponent.IsDead)
        {
            rearPositionPlanner?.Release(this);
            AssignedRearCover = null;
            IsAiming = false;
            tree?.Abort();
            motor.SetCombatMovement(false);
            return;
        }
            
        perception.Tick(Time.deltaTime);
        attackController.Tick();
        tree.Tick();

        IsAiming = CanAttack();

        bool faceShared = ShouldFaceSharedObservation();

        motor.SetCombatMovement(IsAiming);
        // 경계 중에는 NavMeshAgent 대신 Guard가 회전을 담당한다.
        motor.SetAutomaticRotation(!IsAiming && !isGuarding && !faceShared);

        if (IsAiming)
            Attack();
        else if (faceShared)
            motor.TryFaceTowards(perceptionLink.LatestSharedObservation.Position);
    }

    private NodeStatus Idle()
    {
        motor.Stop();
        return NodeStatus.Running;
    }

    private NodeStatus Investigate()
    {
        SquadObservation observation = perceptionLink.LatestSharedObservation;
        Vector3 targetPosition = observation.Position;

        if (!motor.TryMoveTo(targetPosition, investigateStopDistance))
            return NodeStatus.Failure;

        if (motor.HasReachedDestination)
        {
            investigatedObservation = observation;
            return NodeStatus.Success;
        }
            
        return NodeStatus.Running;
    }

    private NodeStatus CommandMoveTo()
    {
        if (!motor.TryMoveTo(CurrentCommand.Destination, investigateStopDistance))
            return NodeStatus.Failure;

        if (motor.HasReachedDestination)
        {
            SquadCommand completedCommand = CurrentCommand;
            ClearCommand();
            // 도착으로 끝난 명령만 알린다. 취소는 완료로 취급하지 않는다.
            OnCommandCompleted?.Invoke(this, completedCommand);
            return NodeStatus.Success;
        }
            

        return NodeStatus.Running;
    }

    private NodeStatus CommandHoldPosition()
    {
        motor.Stop();

        return NodeStatus.Running;
    }

    private NodeStatus Recover()
    {
        motor.Stop();
        recoveryElapsed += Time.deltaTime;

        if (recoveryElapsed < aedKitDefinition.useDuration)
            return NodeStatus.Running;

        float missingHealth = healthComponent.MaxHealth - healthComponent.CurrentHealth;
        float restored = 0f;

        restored = aedKit.ConsumeForHealing(missingHealth);

        if (restored > 0f)
        {
            healthComponent.Restore(restored);
        }

        if (healthComponent.CurrentHealth >= healthComponent.MaxHealth || aedKit.IsDepleted)
        {
            StopRecovery();
            return NodeStatus.Success;
        }
        else if (healthComponent.CurrentHealth < healthComponent.MaxHealth && !aedKit.IsDepleted)
        {
            recoveryElapsed = 0f;
            return NodeStatus.Running;
        }

        return NodeStatus.Failure;
    }

    private void StopAction()
    {
        motor.Stop();
    }

    private bool CanAttack()
    {
        // 타겟이 존재하는지 확인
        if (perception.Target == null)
            return false;

        // 타겟을 직접 보고 있는지 확인
        if (!perception.IsConfirmed)
            return false;

        float distanceToTarget = Vector3.Distance(transform.position, perception.Target.position);

        // 공격 범위 내에 있는지 확인
        if (distanceToTarget * distanceToTarget >= attackDistance * attackDistance)
            return false;

        return true;
    }

    private void Attack()
    {
        Vector3 targetPoint = aimPointProvider.GetAimPosition(aimedHitPart);
        if (motor.TryFaceTowards(targetPoint))
        {
            if (attackController.TryAttack(targetPoint) && !attackController.IsBurstActive)
            {
                SelectNextAimPart();
            }
        }
    }

    private void SelectNextAimPart()
    {
        aimedHitPart = (UnityEngine.Random.value < headAimChance) ? HitPart.Head : HitPart.Body;
    }

    public void ReceiveCommand(SquadCommand command)
    {
        rearPositionPlanner?.Release(this);
        AssignedRearCover = null;
        // 이동 중에 명령을 받아 다른 곳으로 이동할 때 이전 이동을 멈추고 새로운 명령을 수행하도록 설정한다.
        motor.Stop();
        currentCommand = command;
        HasCommand = true;
        isSupportMoving = false;
        hasSupportDestination = false;
        nextSupportRefreshTime = 0f;
    }

    public void ClearCommand()
    {
        rearPositionPlanner?.Release(this);
        AssignedRearCover = null;
        // 지휘 해제 직후 이전 지원 목적지로 계속 이동하지 않도록 멈춘다.
        if (motor != null)
            motor.Stop();
        currentCommand = default;
        HasCommand = false;
        isSupportMoving = false;
        hasSupportDestination = false;
        nextSupportRefreshTime = 0f;
    }
    private void StopSupportMovement()
    {
        rearPositionPlanner?.Release(this);
        AssignedRearCover = null;
        isSupportMoving = false;
        hasSupportDestination = false;
        nextSupportRefreshTime = 0f;
        motor.Stop();
    }

    private bool ShouldFaceSharedObservation()
    {
        // 이미 시선이 목표를 향하고 있거나 경계 중이면 시선 전환을 하지 않는다.
        if (IsAiming || isGuarding)
            return false;

        // 명령이나 시야 공유 보고가 없으면 시선 전환을 하지 않는다.
        if (!perceptionLink.HasSharedObservation || !HasCommand)
            return false;

        if (CurrentCommand.Type == SquadCommandType.HoldPosition)
        {
            return true;
        }

        if (CurrentCommand.Type == SquadCommandType.Support && (!isSupportMoving || !hasSupportDestination))
        {
            return true;
        }

        return false;

    }

    public void SetDefensiveOnly(bool value)
    {
        defensiveOnly = value;
    }

    public void RequestRecovery()
    {
        if (!IsAlive || recoveryRequested)
            return;

        recoveryElapsed = 0f;
        recoveryRequested = true;
    }

    private bool CanRecover()
    {
        if (healthComponent == null)
            return false;

        if (!recoveryRequested || !IsAlive || dataRoot == null || !dataRoot.isReady)
            return false;

        if (aedKit == null)
        {
            if (!dataRoot.MedicineCatalog.TryGet(3002, out aedKitDefinition))
            {
                Debug.LogWarning("AED kit not found in MedicineCatalog.");
                return false;
            }

            aedKit = new MedicineInventoryItem(aedKitDefinition);
        }

        return !aedKit.IsDepleted && healthComponent.CurrentHealth < healthComponent.MaxHealth && !perception.IsConfirmed;
    }

    private void StopRecovery()
    {
        recoveryRequested = false;
        recoveryElapsed = 0f;
    }

    private void HandleDamageTaken(DamageInfo damageInfo)
    {
        StopRecovery();
    }
}
