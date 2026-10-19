using Unity.Hierarchy;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[RequireComponent(typeof(SquadRearPositionPlanner))]
public class SquadCommander : MonoBehaviour
{
    [Header("분대 구성")]
    [SerializeField] private SquadBlackboard blackboard;
    [SerializeField] private SquadMemberBrain leader;
    [SerializeField] private SquadMemberBrain[] assaultMembers;

    [Header("전술 판단")]
    [SerializeField, Min(0.02f)] private float decisionInterval = 0.2f;

    [Header("전진 목적지")]
    [SerializeField, Min(0.1f)] private float advanceDistance = 5f;
    [SerializeField, Min(0.1f)] private float destinationSampleRadius = 1f;

    [Header("엄호 대원 후방 간격")]
    [SerializeField, Min(0.1f)] private float supportDistance = 5f;
    [SerializeField, Min(0.1f)] private float rearLateralSpacing = 2.5f;
    [SerializeField, Min(0.1f)] private float leaderDistance = 8f;

    [Header("재정렬 간격")]
    [SerializeField, Min(0.1f)] private float regroupFrontDistance = 3f;
    [SerializeField, Min(0.1f)] private float regroupSideForwardDistance = 2f;
    [SerializeField, Min(0.1f)] private float regroupLateralSpacing = 1.5f;
    [SerializeField, Min(0.1f)] private float regroupRetryInterval = 0.5f;
    [SerializeField, Min(0.1f)] private float regroupCandidateRadius = 0.5f;

    [Header("재정렬 이후 복귀 시간")]
    [SerializeField, Min(0.1f)] private float regroupReturnDelay = 5f;

    [SerializeField, Min(0f)] private float holdDistance = 10f;
    [SerializeField, Min(0.1f)] private float contactReplanDistance = 4f;


    private readonly Dictionary<SquadMemberBrain, Vector3> slots = new (4);
    private readonly Dictionary<SquadMemberBrain, Vector3> regroupDesiredPositions = new(3);
    private readonly Dictionary<SquadMemberBrain, Vector3> originMemberPosition = new(4);
    private HealthComponent leaderHealth;
    private HealthComponent[] assaultHealth;
    private float nextDecisionTime;
    private NavMeshPath advancePath;
    private Vector3 issuedAdvanceDestination;
    private SquadRearPositionPlanner rearPositionPlanner;
    private readonly HashSet<SquadMemberBrain> commandedMembers = new HashSet<SquadMemberBrain>();
    private bool regroupOrdersIssued;
    private bool returnOrdersIssued;
    private readonly HashSet<SquadMemberBrain> pendingRegroupMembers = new(3);
    private bool IsRegroupComplete => regroupOrdersIssued && pendingRegroupMembers.Count == 0;
    private SquadTactic previousTactic;
    private int previousAliveCount = -1;
    private SquadMemberBrain formationFront;
    private bool formationMovePending;
    private bool isFormationApproaching;
    private float nextFormationStepTime;
    private Transform issuedRearFront;
    private Vector3 issuedRearDirection;
    private bool hasSingleAdvanceContact;
    private Vector3 singleAdvanceContactPosition;
    private float nextRegroupRetryTime;
    private float regroupCompletedTime = -1f;

    public SquadTactic CurrentTactic { get; private set; }
    public SquadMemberBrain Leader => leader;
    public int AliveCount { get; private set; }

    public SquadMemberBrain AdvanceMember { get; private set; }
    public SquadMemberBrain SupportMember { get; private set; }
    public SingleAdvancePhase AdvancePhase { get; private set; }

    private void Awake()
    {
        if (leader != null)
        {
            leaderHealth = leader.GetComponent<HealthComponent>();
            originMemberPosition[leader] = leader.transform.position;
        }

        if (assaultMembers == null)
            assaultMembers = new SquadMemberBrain[0];

        assaultHealth = new HealthComponent[assaultMembers.Length];

        for (int i = 0; i < assaultMembers.Length; i++)
        {
            if (assaultMembers[i] != null)
            {
                assaultHealth[i] = assaultMembers[i].GetComponent<HealthComponent>();

                originMemberPosition[assaultMembers[i]] = assaultMembers[i].transform.position;
            }
        }

        advancePath = new NavMeshPath();
        rearPositionPlanner = GetComponent<SquadRearPositionPlanner>();
        rearPositionPlanner.Configure(leader, assaultMembers);
    }

    private void Update()
    {
        if (Time.time < nextDecisionTime)
            return;

        nextDecisionTime = Time.time + decisionInterval;

        AliveCount = CountAliveMembers();
        if (hasSingleAdvanceContact && blackboard != null && blackboard.HasValidObservation &&
            Vector3.ProjectOnPlane(blackboard.LatestObservation.Position - singleAdvanceContactPosition, Vector3.up).sqrMagnitude >=
            contactReplanDistance * contactReplanDistance)
            ResetAdvanceRoles();

        CurrentTactic = EvaluateTactic();

        // 어떤 전술 이후에 현재 전술이 NoContact로 바뀌었거나,
        // 재정렬 명령이 발행된 상태에서 분대원 수가 바뀌었다면, 재정렬 명령을 다시 발행한다.
        bool regroupRequired = CurrentTactic == SquadTactic.NoContact && (previousTactic != SquadTactic.NoContact || regroupOrdersIssued && AliveCount != previousAliveCount);

        if (CurrentTactic != previousTactic || AliveCount != previousAliveCount)
        {
            ResetAdvanceRoles();
            previousTactic = CurrentTactic;
            previousAliveCount = AliveCount;
            bool defensive = CurrentTactic == SquadTactic.DisorganizedDefense;
            if (leader != null) leader.SetDefensiveOnly(defensive);
            for (int i = 0; i < assaultMembers.Length; i++)
                if (assaultMembers[i] != null) assaultMembers[i].SetDefensiveOnly(defensive);
        }

        if (CurrentTactic == SquadTactic.NoContact)
        {
            if (regroupRequired)
                IssueRegroupOrders();

            RetryUnassignedRegroupMembers();

            if (IsRegroupComplete)
            {
                if (regroupCompletedTime < 0f)
                    regroupCompletedTime = Time.time;

                if (Time.time - regroupCompletedTime >= regroupReturnDelay)
                {
                    IssueReturnOrders();
                }
            }

            return;
        }

        if (CurrentTactic == SquadTactic.DisorganizedDefense)
            return;

        if (CurrentTactic == SquadTactic.Investigate)
        {
            if (CountAliveMembers() >= 4)
            {
                if (AdvanceMember == null && !TrySelectAdvanceRoles())
                {
                    ResetAdvanceRoles();
                    return;
                }

                IssueCommand(AdvanceMember, new SquadCommand(SquadCommandType.Investigate, blackboard.LatestObservation.Position));
                IssueRearOrders(AdvanceMember);
                return;
            }

            // 분대원이 3명 이하라면, 가장 가까운 돌격대원을 전진 대원으로 선택한다.
            if (formationFront == null || !formationFront.IsAlive)
                formationFront = FindClosestAssault(blackboard.LatestObservation.Position, null);

            // 분대원이 모두 사망하여, 전진 대원을 선택할 수 없는 경우, 분대장은 현재 위치에서 대기하도록 한다.
            if (formationFront == null)
            {
                IssueCommand(leader, new SquadCommand(SquadCommandType.HoldPosition, leader.transform.position));
                return;
            }

            // 전진 대원이 선택되었다면, 전진 대원은 현재 관측 위치를 향해 이동하고, 나머지 대원들은 전진 대원의 후방에서 엄호하도록 한다.
            IssueCommand(formationFront, new SquadCommand(SquadCommandType.Investigate, blackboard.LatestObservation.Position));
            IssueRearOrders(formationFront);
            return;
        }

        if (formationFront == null || !formationFront.IsAlive)
            formationFront = FindClosestAssault(blackboard.LatestObservation.Position, null);
        if (formationFront == null)
        {
            IssueCommand(leader, new SquadCommand(SquadCommandType.HoldPosition, leader.transform.position));
            return;
        }

        IssueRearOrders(formationFront);

        if (CurrentTactic == SquadTactic.FormationAdvance)
        {
            // 현재 상태를 보고 플레이어에게 접근이 필요할지 판단한다.
            UpdateFormationApproach();

            // 전진 명령이 내려진 상태에서 접근 상태가 해제되었다면, 전진 명령을 취소한다.
            if (!isFormationApproaching)
            {
                if (formationMovePending)
                {
                    formationFront.OnCommandCompleted -= HandleFormationMoveCompleted;
                    formationMovePending = false;
                }

                // 도착한 상태라면 전진 명령을 취소하고, 현재 위치에서 대기하도록 한다.
                IssueCommand(formationFront, new SquadCommand(SquadCommandType.HoldPosition, formationFront.transform.position));

                return;
            }
        }

            if (CurrentTactic == SquadTactic.HoldPosition)
            IssueCommand(formationFront, new SquadCommand(SquadCommandType.HoldPosition, formationFront.transform.position));
        else if (!formationMovePending && Time.time >= nextFormationStepTime)
        {
            if (RearMembersReady() && TryGetAdvanceDestination(formationFront, out Vector3 destination))
            {
                issuedAdvanceDestination = destination;
                formationFront.OnCommandCompleted -= HandleFormationMoveCompleted;
                formationFront.OnCommandCompleted += HandleFormationMoveCompleted;
                IssueCommand(formationFront, new SquadCommand(SquadCommandType.MoveTo, destination));
                formationMovePending = true;
            }
            else
                IssueCommand(formationFront, new SquadCommand(SquadCommandType.HoldPosition, formationFront.transform.position));
        }
    }

    private void OnDisable()
    {
        ResetAdvanceRoles();
    }

    private void ResetAdvanceRoles()
    {
        if (AdvanceMember != null)
            AdvanceMember.OnCommandCompleted -= HandleAdvanceCompleted;
        if (formationFront != null)
            formationFront.OnCommandCompleted -= HandleFormationMoveCompleted;

        foreach (SquadMemberBrain member in commandedMembers)
            if (member != null) member.ClearCommand();

        commandedMembers.Clear();

        foreach (SquadMemberBrain member in pendingRegroupMembers)
            if (member != null) member.OnCommandCompleted -= HandleRegroupCompleted;
        pendingRegroupMembers.Clear();
        regroupDesiredPositions.Clear();

        regroupCompletedTime = -1f;

        regroupOrdersIssued = false;
        returnOrdersIssued = false;
        slots.Clear();

        if (rearPositionPlanner != null) rearPositionPlanner.ClearReservations();

        AdvanceMember = null;
        SupportMember = null;
        AdvancePhase = SingleAdvancePhase.NotStarted;
        formationFront = null;
        formationMovePending = false;
        isFormationApproaching = false; 
        nextFormationStepTime = 0f;
        issuedRearFront = null;
        hasSingleAdvanceContact = false;
    }

    private void HandleAdvanceCompleted(SquadMemberBrain member, SquadCommand completedCommand)
    {
        if (member != AdvanceMember)
            return;

        if (AdvancePhase != SingleAdvancePhase.Advancing || AdvanceMember == null ||
            completedCommand.Type != SquadCommandType.MoveTo ||
            (completedCommand.Destination - issuedAdvanceDestination).sqrMagnitude > 0.001f)
        {
            return;
        }

        AdvanceMember.OnCommandCompleted -= HandleAdvanceCompleted;
        AdvancePhase = SingleAdvancePhase.Completed;
        // 도착 후 같은 전진을 반복하거나 공유 위치를 향해 다시 접근하지 않는다.
        IssueCommand(AdvanceMember, new SquadCommand(
            SquadCommandType.HoldPosition, AdvanceMember.transform.position));
    }

    private int CountAliveMembers()
    {
        int count = leaderHealth != null && leaderHealth.IsAlive ? 1 : 0;

        for (int i = 0; i < assaultHealth.Length; i++)
        {
            if (assaultHealth[i] != null && assaultHealth[i].IsAlive)
                count++;
        }

        return count;
    }

    private SquadTactic EvaluateTactic()
    {
        // 분대장이 사망한 상태
        if (leader == null || leaderHealth == null || leaderHealth.IsDead)
            return SquadTactic.DisorganizedDefense;

        // 유효한 정보가 없는 상태
        if (blackboard == null || !blackboard.HasValidObservation)
            return SquadTactic.NoContact;

        // 공유 정보는 있지만 실제로 적을 발견하지는 못한 상태
        if (!HasAnyConfirmedTarget())
            return SquadTactic.Investigate;

        Vector3 offset = blackboard.LatestObservation.Position - leader.transform.position;

        offset.y = 0f; // 수직 방향은 무시

        // 적이 이미 가까운 거리에 있는 경우
        if (offset.sqrMagnitude <= holdDistance * holdDistance)
            return SquadTactic.HoldPosition;

        return SquadTactic.FormationAdvance;
    }

    private SquadMemberBrain FindClosestAssault(Vector3 position, SquadMemberBrain excludedMember)
    {
        SquadMemberBrain closestMember = null;
        float closestDistanceSqr = float.MaxValue;
        
        for (int i = 0; i < assaultMembers.Length; i++)
        {
            SquadMemberBrain member = assaultMembers[i];
            HealthComponent health = assaultHealth[i];

            if (member == null || member == leader || member == excludedMember || health == null || health.IsDead)
                continue;

            Vector3 offset = member.transform.position - position;
            offset.y = 0f;

            float distancesqr = offset.sqrMagnitude;

            if (distancesqr < closestDistanceSqr)
            {
                closestDistanceSqr = distancesqr;
                closestMember = member;
            }
        }

        return closestMember;
    }

    private bool TrySelectAdvanceRoles()
    {
        // 유효한 정보가 없는 상태라면 돌격대원을 선택하지 않는다.
        if (blackboard == null || !blackboard.HasValidObservation)
            return false;

        Vector3 reportedPosition = blackboard.LatestObservation.Position;

        SquadMemberBrain advanceMember = FindClosestAssault(reportedPosition, null);

        if (advanceMember == null)
            return false;

        SquadMemberBrain supportMember = FindClosestAssault(advanceMember.transform.position, advanceMember);

        if (supportMember == null)
            return false;

        AdvanceMember = advanceMember;
        SupportMember = supportMember;

        return true;
    }

    private bool TryGetAdvanceDestination(SquadMemberBrain mover, out Vector3 destination)
    {
        destination = default;

        if (mover == null || blackboard == null || !blackboard.HasValidObservation)
            return false;

        NavMeshAgent agent = mover.NavigationAgent;

        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        Vector3 start = mover.transform.position;
        Vector3 reportedPosition = blackboard.LatestObservation.Position;

        Vector3 towardTarget = reportedPosition - start;
        towardTarget.y = 0f;

        float distance = Mathf.Max(towardTarget.magnitude, 0.01f);
        float minimumDistance = mover.CombatStopDistance;

        // 목표 위치가 있더라도 대원의 최소 전투 거리보다는 더 가까운 위치로 이동하지 않도록 한다.
        float availableDistance = Mathf.Max(distance - minimumDistance, 0.01f);
        float step = Mathf.Min(advanceDistance, availableDistance);

        if (step <= 0.25f)
            return false;

        Vector3 direction = towardTarget / distance;
        Vector3 candidate = start + direction * step;

        var filter = new NavMeshQueryFilter
        {
            areaMask = agent.areaMask,
            agentTypeID = agent.agentTypeID
        };

        Vector3 side = Vector3.Cross(Vector3.up, direction);
        for (int i = 0; i < 5; i++)
        {
            float lateral = i == 0 ? 0f : ((i + 1) / 2) * 1.5f * (i % 2 == 1 ? 1f : -1f);
            if (!NavMesh.SamplePosition(candidate + side * lateral, out NavMeshHit hit, destinationSampleRadius, filter))
                continue;
            Vector3 actualMovement = Vector3.ProjectOnPlane(hit.position - start, Vector3.up);
            if (Vector3.Dot(actualMovement, direction) < 0.25f ||
                Vector3.ProjectOnPlane(hit.position - reportedPosition, Vector3.up).sqrMagnitude < minimumDistance * minimumDistance)
                continue;
            if (!NavMesh.CalculatePath(agent.nextPosition, hit.position, filter, advancePath) ||
                advancePath.status != NavMeshPathStatus.PathComplete)
                continue;
            destination = hit.position;
            return true;
        }
        return false;
    }

    private void UpdateFormationApproach()
    {
        Vector3 offset = Vector3.ProjectOnPlane(blackboard.LatestObservation.Position - formationFront.transform.position, Vector3.up);

        float distanceSqr = offset.sqrMagnitude;

        // 이미 접근 중이라면, 일정 거리 안으로 들어올 때 접근 상태를 해제한다.
        if (isFormationApproaching)
        {
            float stopDistance = formationFront.CombatStopDistance + formationFront.NavigationAgent.stoppingDistance + 0.05f;

            if (distanceSqr <= stopDistance * stopDistance)
                isFormationApproaching = false;
        }
        // 거리가 충분히 먼 상태라면, 접근 상태를 활성화한다.
        else
        {
            float startDistance = formationFront.CombatStartDistance;

            if (distanceSqr >= startDistance * startDistance)
                isFormationApproaching = true;
        }
    }

    private void IssueRearOrders(SquadMemberBrain front)
    {
        if (front == null || !blackboard.HasValidObservation)
            return;
        Vector3 rear = front.transform.position - blackboard.LatestObservation.Position;
        rear.y = 0f;
        if (rear.sqrMagnitude < 0.0001f)
            return;
        rear.Normalize();
        bool keepAxis = issuedRearFront == front.transform && Vector3.Dot(rear, issuedRearDirection) > 0.985f;
        if (keepAxis) rear = issuedRearDirection;
        Vector3 leaderOffset = rear * Mathf.Max(leaderDistance, supportDistance + 1f);
        // 지휘관의 후방 자리를 먼저 확보한다. 대원은 이 자리보다 앞에서 엄폐물을 선택한다.
        IssueCommand(leader, new SquadCommand(SquadCommandType.Support, front.transform, leaderOffset));
        if (leader != null && leader.IsAlive && leader.HasCommand && leader.CurrentCommand.Type == SquadCommandType.Support)
            rearPositionPlanner.TryGetPosition(leader, front.transform, leader.CurrentCommand.FollowOffset,
                blackboard.LatestObservation.Position, out _, out _);
        // 몸의 조준 회전은 대형을 돌리지 않는다. 위협 방향이 충분히 바뀔 때만 대형 축을 갱신한다.
        if (keepAxis)
            return;
        issuedRearFront = front.transform;
        issuedRearDirection = rear;
        Vector3 side = Vector3.Cross(Vector3.up, rear);
        int slot = 0;
        for (int i = 0; i < assaultMembers.Length; i++)
        {
            SquadMemberBrain member = assaultMembers[i];
            if (member == null || member == front || !member.IsAlive)
                continue;
            float lateral = slot++ % 2 == 0 ? -rearLateralSpacing : rearLateralSpacing;
            IssueCommand(member, new SquadCommand(SquadCommandType.Support, front.transform,
                rear * supportDistance + side * lateral));
        }
    }

    private void IssueRegroupOrders()
    {
        if (regroupOrdersIssued || leader == null || !leader.IsAlive)
            return;

        regroupOrdersIssued = true;
        pendingRegroupMembers.Clear();
        regroupDesiredPositions.Clear();

        IssueCommand(leader, new SquadCommand(SquadCommandType.HoldPosition, leader.transform.position));

        Vector3 origin = leader.transform.position;
        Vector3 forward = Vector3.ProjectOnPlane(leader.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        slots.Clear();
        slots.Add(leader, origin);

        int slot = 0;
        for (int i = 0; i < assaultMembers.Length; i++)
        {
            SquadMemberBrain member = assaultMembers[i];
            if (member == null || !member.IsAlive)
                continue;

            pendingRegroupMembers.Add(member);

            member.OnCommandCompleted -= HandleRegroupCompleted;
            member.OnCommandCompleted += HandleRegroupCompleted;

            int slotIndex = slot++;

            float forwardOffset = slotIndex == 0
                ? regroupFrontDistance
                : regroupSideForwardDistance;

            float lateral = slotIndex == 0 ? 0f
                : slotIndex == 1 ? -regroupLateralSpacing
                : regroupLateralSpacing;

            Vector3 regroupPosition = origin + forward * forwardOffset + right * lateral;
            regroupDesiredPositions[member] = regroupPosition;

            TryAssignRegroupSlot(member, regroupPosition);
        }

        nextRegroupRetryTime = Time.time + regroupRetryInterval;
    }

    private void IssueCommand(SquadMemberBrain member, SquadCommand command)
    {
        if (member == null || !member.IsAlive)
            return;
        if (member.HasCommand && member.CurrentCommand.Type == command.Type &&
            member.CurrentCommand.FollowTarget == command.FollowTarget &&
            (member.CurrentCommand.FollowOffset - command.FollowOffset).sqrMagnitude < 0.01f &&
            (command.Type == SquadCommandType.HoldPosition ||
             (member.CurrentCommand.Destination - command.Destination).sqrMagnitude < 0.01f))
            return;
        member.ReceiveCommand(command);
        commandedMembers.Add(member);
    }

    private bool RearMembersReady()
    {
        foreach (SquadMemberBrain member in commandedMembers)
        {
            if (member == null || !member.IsAlive || member == formationFront)
                continue;
            if (!member.HasRearPosition ||
                Vector3.ProjectOnPlane(member.RearPosition - member.transform.position, Vector3.up).sqrMagnitude > 1f)
                return false;
        }
        return true;
    }

    private bool HasAnyConfirmedTarget()
    {
        if (leader != null && leader.IsAlive && leader.HasConfirmedTarget)
            return true;

        foreach (SquadMemberBrain member in assaultMembers)
        {
            if (member != null && member.IsAlive && member.HasConfirmedTarget)
                return true;
        }

        return false;
    }

    private void HandleFormationMoveCompleted(SquadMemberBrain member, SquadCommand completedCommand)
    {
        if (member != formationFront)
            return;

        if (!formationMovePending || formationFront == null || completedCommand.Type != SquadCommandType.MoveTo ||
            (completedCommand.Destination - issuedAdvanceDestination).sqrMagnitude > 0.001f)
            return;
        formationFront.OnCommandCompleted -= HandleFormationMoveCompleted;
        formationMovePending = false;
        nextFormationStepTime = Time.time + decisionInterval;
        IssueCommand(formationFront, new SquadCommand(SquadCommandType.HoldPosition, formationFront.transform.position));
    }

    private void HandleRegroupCompleted(SquadMemberBrain member, SquadCommand completedCommand)
    {
        if (!regroupOrdersIssued)
            return;
        if (!pendingRegroupMembers.Contains(member))
            return;
        if (completedCommand.Type != SquadCommandType.Regroup ||
            (completedCommand.Destination - slots[member]).sqrMagnitude > 0.001f)
            return;

        pendingRegroupMembers.Remove(member);
        member.OnCommandCompleted -= HandleRegroupCompleted;
    }

    private bool TryAssignRegroupSlot(SquadMemberBrain member, Vector3 desired)
    {
        // desired와 가까운 대체 후보를 검사한다.
        if (member == null) return false;

        NavMeshAgent agent = member.NavigationAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            areaMask = agent.areaMask,
            agentTypeID = agent.agentTypeID
        };

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 1f, filter))
        {
            if (!NavMesh.CalculatePath(member.NavigationAgent.nextPosition, hit.position, filter, advancePath) ||
                advancePath.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            bool tooClose = false;

            foreach (var pair in slots)
            {
                if (pair.Key == member || !pair.Key.IsAlive || !pair.Key.isActiveAndEnabled)
                    continue;

                if (Vector3.ProjectOnPlane(pair.Value - hit.position, Vector3.up).sqrMagnitude < regroupLateralSpacing * regroupLateralSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                return false;

            slots.Add(member, hit.position);
            IssueCommand(member, new SquadCommand(SquadCommandType.Regroup, hit.position));
            return true;
        }
        
        return false;
    }
    
    private void RetryUnassignedRegroupMembers()
    {
        if (!regroupOrdersIssued || IsRegroupComplete)
            return;

        if (Time.time < nextRegroupRetryTime)
            return;

        nextRegroupRetryTime = Time.time + regroupRetryInterval;

        foreach (SquadMemberBrain member in pendingRegroupMembers)
        {
            if (member == null || !member.IsAlive)
                continue;

            // 이미 슬롯이 할당된 대원은 재할당하지 않는다.
            if (slots.TryGetValue(member, out Vector3 desiredPosition))
                continue;

            // 슬롯이 할당되지 않은 대원은 원하는 위치를 기준으로 상하좌우로 추가 후보도 탐색한다.
            for (int i = 0; i < 5; i++)
            {
                Vector3 candidatePosition = regroupDesiredPositions[member];

                if (i == 1) candidatePosition += Vector3.right * regroupCandidateRadius;
                else if (i == 2) candidatePosition += Vector3.left * regroupCandidateRadius;
                else if (i == 3) candidatePosition += Vector3.forward * regroupCandidateRadius;
                else if (i == 4) candidatePosition += Vector3.back * regroupCandidateRadius;

                if (TryAssignRegroupSlot(member, candidatePosition))
                    break;
            }
        }
    }

    private void IssueReturnOrders()
    {
        if (returnOrdersIssued)
            return;

        returnOrdersIssued = true;

        foreach (SquadMemberBrain member in originMemberPosition.Keys)
        {
            if (member == null || !member.IsAlive || !member.isActiveAndEnabled)
                continue;
            IssueCommand(member, new SquadCommand(SquadCommandType.ReturnToBase, originMemberPosition[member]));
            member.OnCommandCompleted += HandleReturnToBase;
        }
    }

    private void HandleReturnToBase(SquadMemberBrain member, SquadCommand completedCommand)
    {
        if (completedCommand.Type != SquadCommandType.ReturnToBase)
            return;
        if (!originMemberPosition.TryGetValue(member, out Vector3 origin) ||
            (completedCommand.Destination - origin).sqrMagnitude > 0.001f)
            return;

        // 멤버의 회복 처리를 진행
        member.RequestRecovery();

        member.OnCommandCompleted -= HandleReturnToBase;
    }
}
