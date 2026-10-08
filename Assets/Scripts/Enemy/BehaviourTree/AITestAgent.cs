using UnityEngine;
using UnityEngine.AI;
using ExtractionShooter.AI;

[RequireComponent(typeof(EnemyMotor), typeof(NavMeshAgent))]
public class AITestAgent : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField, Min(0.02f)] private float decisionInterval = 0.1f;
    [SerializeField, Min(0.1f)] private float sightRange = 12f;
    [SerializeField, Range(0, 360)] private float sightAngle = 120f;
    [SerializeField, Min(0.1f)] private float attackRange = 4f;
    [SerializeField, Min(0.1f)] private float attackExitRange = 5f;
    [SerializeField, Min(0f)] private float investigationDuration = 3f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private EnemyAttackController realAttack;

    private EnemyMotor motor;
    private NavMeshAgent agent;
    private ReactiveSelector tree;
    private bool visible, hasClue;
    private Vector3 lastSeen;
    private float nextDecision, distanceSqr, waitUntil = -1f, nextShot, minSightDot;
    private int patrolIndex;
    private LineRenderer shotLine;
    private float hideShotAt;
    public string ActiveAction { get; private set; } = "대기";
    public int SimulatedShots { get; private set; }
    public int DecisionCount { get; private set; }
    public int VisionRayCount { get; private set; }
    public Vector3 LastSeen => lastSeen;
    public bool HasClue => hasClue;

    public void Configure(Transform target, Transform[] points, LayerMask obstacles, LineRenderer tracer = null)
    {
        this.target = target;
        patrolPoints = points;
        obstacleMask = obstacles;
        shotLine = tracer;
    }

    private void Awake()
    {
        motor = GetComponent<EnemyMotor>();
        agent = GetComponent<NavMeshAgent>();
        CacheSettings();
        // 판단 경로와 조건을 한 번만 구성해 프레임마다 객체를 만들지 않는다.
        tree = new ReactiveSelector(
            new EligibleAction(CanAttack, Attack, StopAction),
            new EligibleAction(() => visible, Chase, StopAction),
            new EligibleAction(() => hasClue, Investigate, StopAction),
            new EligibleAction(() => true, Patrol, StopAction));
    }

    private void OnEnable() { nextDecision = Time.time; }
    private void OnDisable() { tree?.Abort(); }
    private void OnValidate() { CacheSettings(); }
    private void CacheSettings()
    {
        attackExitRange = Mathf.Max(attackRange, attackExitRange);
        // 시야각 변환은 설정이 바뀔 때만 수행해 감지 루프의 반복 계산을 줄인다.
        minSightDot = Mathf.Cos(sightAngle * 0.5f * Mathf.Deg2Rad);
    }

    private void Update()
    {
        realAttack?.Tick();
        if (shotLine != null && Time.time >= hideShotAt) shotLine.enabled = false;
        // 회전은 매 프레임 수행해야 deltaTime 기반 모터가 판단 주기만큼 느려지지 않는다.
        if (ActiveAction == "공격" && visible && target != null)
        {
            bool facing = motor.TryFaceTowards(target.position);
            if (facing)
            {
                if (realAttack != null) realAttack.TryAttack(target.position);
                else if (Time.time >= nextShot)
                {
                    SimulatedShots++;
                    nextShot = Time.time + 0.5f;
                    if (shotLine != null)
                    {
                        shotLine.SetPosition(0, transform.position + Vector3.up);
                        shotLine.SetPosition(1, target.position);
                        shotLine.enabled = true;
                        hideShotAt = Time.time + 0.12f;
                    }
                }
            }
        }
        if (Time.time < nextDecision) return;
        nextDecision = Time.time + Mathf.Max(0.02f, decisionInterval);
        DecisionCount++;
        Sense();
        tree.Tick();
    }

    private void Sense()
    {
        visible = false;
        if (target == null) return;
        Vector3 offset = target.position - transform.position;
        offset.y = 0;
        distanceSqr = offset.sqrMagnitude;
        if (distanceSqr > sightRange * sightRange) return;
        if (distanceSqr > 0.0001f && Vector3.Dot(transform.forward, offset.normalized) < minSightDot) return;
        Vector3 eye = transform.position + Vector3.up * 0.5f;
        Vector3 sight = target.position + Vector3.up * 0.5f - eye;
        VisionRayCount++;
        if (Physics.Raycast(eye, sight.normalized, sight.magnitude, obstacleMask, QueryTriggerInteraction.Ignore)) return;
        visible = true;
        hasClue = true;
        lastSeen = target.position;
        waitUntil = -1f;
    }

    private bool CanAttack()
    {
        // 진입과 이탈 거리를 분리해 경계 근처에서 추적과 공격이 반복 전환되지 않게 한다.
        float range = ActiveAction == "공격" ? attackExitRange : attackRange;
        return visible && distanceSqr <= range * range;
    }

    private NodeStatus Attack()
    {
        ActiveAction = "공격";
        motor.Stop();
        // 발사 실행은 Update에서 회전 완료를 확인한 뒤 수행한다.
        return NodeStatus.Running;
    }

    private NodeStatus Chase()
    {
        ActiveAction = "추적";
        return motor.TryMoveTo(lastSeen, attackRange * 0.8f) ? NodeStatus.Running : NodeStatus.Failure;
    }

    private NodeStatus Investigate()
    {
        ActiveAction = "조사";
        if (waitUntil < 0f)
        {
            if (!motor.TryMoveTo(lastSeen, 0.3f)) { hasClue = false; return NodeStatus.Failure; }
            // 목적지 요청 성공과 실제 경로 성공은 다르므로 계산이 끝난 뒤 검사한다.
            if (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete)
            { hasClue = false; return NodeStatus.Failure; }
            if (!motor.HasReachedDestination) return NodeStatus.Running;
            motor.Stop();
            waitUntil = Time.time + investigationDuration;
        }
        if (Time.time < waitUntil) return NodeStatus.Running;
        hasClue = false;
        waitUntil = -1f;
        return NodeStatus.Success;
    }

    private NodeStatus Patrol()
    {
        ActiveAction = "순찰";
        if (patrolPoints == null || patrolPoints.Length == 0) { motor.Stop(); return NodeStatus.Running; }
        Transform point = patrolPoints[patrolIndex];
        if (point == null) { patrolIndex = (patrolIndex + 1) % patrolPoints.Length; return NodeStatus.Running; }
        if (!motor.TryMoveTo(point.position, 0.3f)) return NodeStatus.Failure;
        if (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete)
        { patrolIndex = (patrolIndex + 1) % patrolPoints.Length; return NodeStatus.Failure; }
        if (motor.HasReachedDestination) patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        return NodeStatus.Running;
    }

    private void StopAction()
    {
        motor.Stop();
        waitUntil = -1f;
        realAttack?.CancelReload();
        if (shotLine != null) shotLine.enabled = false;
    }
}
