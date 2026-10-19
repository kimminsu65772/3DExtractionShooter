using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 전술은 지휘관이 정하고, 이 컴포넌트는 도달 가능한 후방 자리만 선정한다.
[DisallowMultipleComponent]
public class SquadRearPositionPlanner : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float coverSearchRadius = 3f;
    [SerializeField, Min(0.05f)] private float wallClearance = 0.2f;
    [SerializeField, Min(0.1f)] private float memberSpacing = 1.5f;
    [SerializeField, Min(0.1f)] private float leaderRearSpacing = 1f;
    [SerializeField, Min(0.1f)] private float sampleRadius = 0.8f;
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.5f;
    [SerializeField, Min(0f)] private float maxSlotDeviation = 1.5f;
    [SerializeField] private LayerMask coverLayerMask;

    [Header("지원 경로 우회 제한")]
    [SerializeField, Min(1f)] private float maxSupportDetourRatio = 2f;
    [SerializeField, Min(0f)] private float supportDetourAllowance = 1f;

#if UNITY_EDITOR
    [Header("대형 디버그 (Scene 뷰)")]
    [SerializeField] private bool showFormationGizmos = true;
#endif

    private sealed class Reservation
    {
        public bool HasPosition;
        public Vector3 Position;
        public DestructibleCover Cover;
        public float NextRefresh;
        public Vector3 LastDesired;
        public Vector3 LastThreat;
    }

    private readonly Dictionary<SquadMemberBrain, Reservation> reservations =
        new Dictionary<SquadMemberBrain, Reservation>(4);
    private readonly Vector3[] pathCorners = new Vector3[64];
    private SquadMemberBrain leader;
    private SquadMemberBrain[] assaults;
    private NavMeshPath path;
    private Collider[] coverBuffer = new Collider[10];
    private HashSet<DestructibleCover> coverSet = new();

    private void Awake()
    {
        path = new NavMeshPath();
    }

    public void Configure(SquadMemberBrain squadLeader, SquadMemberBrain[] assaultMembers)
    {
        leader = squadLeader;
        assaults = assaultMembers;
    }

    public void Release(SquadMemberBrain member)
    {
        if (member != null && reservations.TryGetValue(member, out Reservation reservation))
        {
            reservation.HasPosition = false;
            reservation.Cover = null;
            reservation.NextRefresh = 0f;
        }
    }

    public void ClearReservations()
    {
        reservations.Clear();
    }

    public bool TryGetPosition(SquadMemberBrain member, Transform front, Vector3 followOffset,
        Vector3 threatPosition, out Vector3 destination, out DestructibleCover selectedCover)
    {
        destination = default;
        selectedCover = null;
        NavMeshAgent agent = member.NavigationAgent;
        if (front == null || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        NavMeshAgent frontAgent = front.GetComponent<NavMeshAgent>();
        if (frontAgent == null || !frontAgent.isActiveAndEnabled || !frontAgent.isOnNavMesh)
            return false;

        Vector3 frontPosition = frontAgent.nextPosition;

        Vector3 rear = Flat(front.position - threatPosition);
        if (rear.sqrMagnitude < 0.0001f)
            return false;
        rear.Normalize();
        Vector3 desired = front.position + followOffset;
        float minimumDepth = Mathf.Max(0.5f, Vector3.Dot(followOffset, rear) - 1f);
        if (member == leader)
            minimumDepth = GetLeaderMinimumDepth(front, rear, minimumDepth);

        if (!reservations.TryGetValue(member, out Reservation reservation))
        {
            reservation = new Reservation();
            reservations.Add(member, reservation);
        }

        // 실패도 짧게 기억해 막힌 자리에서 동일한 탐색을 한 프레임에 두 번 반복하지 않는다.
        if (!reservation.HasPosition && Time.time < reservation.NextRefresh &&
            Flat(desired - reservation.LastDesired).sqrMagnitude < 0.25f * 0.25f &&
            Flat(threatPosition - reservation.LastThreat).sqrMagnitude < 1f)
            return false;

        bool valid = reservation.HasPosition &&
            IsSafePosition(member, reservation.Position, front.position, rear, minimumDepth, threatPosition, desired) &&
            IsFree(member, reservation.Position, reservation.Cover) &&
            (reservation.Cover == null || IsUsableCover(reservation.Cover) &&
                IsBehindCover(reservation.Cover.CoverCollider, reservation.Position, threatPosition)) &&
            Flat(reservation.Position - desired).sqrMagnitude <= coverSearchRadius * coverSearchRadius;

        if (valid && Time.time < reservation.NextRefresh)
        {
            destination = reservation.Position;
            selectedCover = reservation.Cover;
            return true;
        }

        reservation.NextRefresh = Time.time + refreshInterval;
        reservation.LastDesired = desired;
        reservation.LastThreat = threatPosition;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        bool found = false;
        float bestScore = float.MaxValue;
        Vector3 bestPosition = default;
        DestructibleCover bestCover = null;

        // 유효한 기존 엄폐 자리는 유지해 대원이 가까운 후보 사이를 왕복하지 않게 한다.
        if (valid && reservation.Cover != null 
            && HasCompletePath(agent, reservation.Position, filter, threatPosition, member.SupportThreatClearance, out _)
            && IsWithinFormationPathDistance(reservation.Position, frontPosition, followOffset, filter))
        {
            destination = reservation.Position;
            selectedCover = reservation.Cover;
            return true;
        }

        Vector3 side = Vector3.Cross(Vector3.up, rear);

        coverSet.Clear();
        int count = Physics.OverlapSphereNonAlloc(desired, coverSearchRadius, coverBuffer, coverLayerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            DestructibleCover cover = coverBuffer[i].GetComponentInParent<DestructibleCover>();
            if (!IsUsableCover(cover))
                continue;

            if (!coverSet.Add(cover))
                continue;

            Collider collider = cover.CoverCollider;
            Bounds bounds = collider.bounds;

            // Bounds.extents로 박스 전체 크기의 절반을 구한다. 그 후 후방 방향과 좌우 방향으로 측정한 절반 범위를 구한다.
            float rearExtent = Mathf.Abs(rear.x) * bounds.extents.x + Mathf.Abs(rear.z) * bounds.extents.z;
            float sideExtent = Mathf.Abs(side.x) * bounds.extents.x + Mathf.Abs(side.z) * bounds.extents.z;

            for (int j = 0; j < 3; j++)
            {
                // 중앙, 좌측, 우측 후보를 생성한다. 좌우 후보는 Bounds의 60%를 최대 1.5m로 제한한다.
                float lateral = j == 0 ? 0f : (j == 1 ? 1f : -1f) * Mathf.Min(sideExtent * 0.6f, 1.5f);
                Vector3 candidate = bounds.center + rear * (rearExtent + agent.radius + wallClearance) + side * lateral;
                candidate.y = desired.y;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, filter))
                    continue;
                if (Flat(hit.position - desired).sqrMagnitude > coverSearchRadius * coverSearchRadius ||
                    !IsSafePosition(member, hit.position, front.position, rear, minimumDepth, threatPosition, desired) ||
                    !IsFree(member, hit.position, cover) || !IsBehindCover(collider, hit.position, threatPosition))
                    continue;

                float score = Flat(hit.position - desired).sqrMagnitude + Flat(hit.position - member.transform.position).sqrMagnitude * 0.15f;
                if (score >= bestScore
                    || !HasCompletePath(agent, hit.position, filter, threatPosition, member.SupportThreatClearance, out _)
                    || !IsWithinFormationPathDistance(hit.position, frontPosition, followOffset, filter))
                    continue;
                found = true;
                bestScore = score;
                bestPosition = hit.position;
                bestCover = cover;
            }
        }

        // 엄폐 후보가 없을 때만 빈 대형 자리를 찾는다. 한 엄폐물에 여러 대원을 겹쳐 배정하지 않는다.
        if (!found)
        {
            for (int i = 0; i < 17; i++)
            {
                float radius = i == 0 ? 0f : (i <= 8 ? memberSpacing : memberSpacing * 2f);
                float angle = i == 0 ? 0f : ((i - 1) % 8) * 45f;
                Vector3 candidate = desired + Quaternion.AngleAxis(angle, Vector3.up) * rear * radius;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, filter) ||
                    !IsSafePosition(member, hit.position, front.position, rear, minimumDepth, threatPosition, desired) ||
                    !IsFree(member, hit.position, null))
                    continue;
                float score = Flat(hit.position - desired).sqrMagnitude;
                if (score >= bestScore 
                    || !HasCompletePath(agent, hit.position, filter, threatPosition, member.SupportThreatClearance, out _) 
                    || !IsWithinFormationPathDistance(hit.position, frontPosition, followOffset, filter))
                    continue;
                found = true;
                bestScore = score;
                bestPosition = hit.position;
            }
        }

        reservation.HasPosition = found;
        reservation.Position = bestPosition;
        reservation.Cover = bestCover;
        destination = bestPosition;
        selectedCover = bestCover;
        return found;
    }

    private float GetLeaderMinimumDepth(Transform front, Vector3 rear, float minimumDepth)
    {
        if (assaults == null)
            return minimumDepth;

        for (int i = 0; i < assaults.Length; i++)
        {
            SquadMemberBrain assault = assaults[i];
            if (assault == null || !assault.IsAlive || assault.transform == front)
                continue;

            if (!assault.HasCommand)
                continue;

            SquadCommand command = assault.CurrentCommand;
            if (command.Type != SquadCommandType.Support || command.FollowTarget != front)
                continue;

            float depth = Vector3.Dot(Flat(command.FollowOffset), rear);
            minimumDepth = Mathf.Max(minimumDepth, depth + leaderRearSpacing);
        }
        return minimumDepth;
    }

    private bool IsFree(SquadMemberBrain member, Vector3 position, DestructibleCover cover)
    {
        foreach (var pair in reservations)
        {
            if (pair.Key == member || pair.Key == null || !pair.Key.IsAlive || !pair.Key.isActiveAndEnabled || !pair.Value.HasPosition)
                continue;
            if (cover != null && pair.Value.Cover == cover)
                return false;
            float spacing = Mathf.Max(memberSpacing, member.NavigationAgent.radius + pair.Key.NavigationAgent.radius + 0.1f);
            if (Flat(position - pair.Value.Position).sqrMagnitude < spacing * spacing)
                return false;
        }
        return true;
    }

    private bool IsSafePosition(SquadMemberBrain member, Vector3 position, Vector3 front,
        Vector3 rear, float minimumDepth, Vector3 threat, Vector3 desired)
    {
        float supportThreatClearanceSqr = member.SupportThreatClearance * member.SupportThreatClearance;
        float depth = Vector3.Dot(Flat(position - front), rear);
        // 대원의 엄폐 선택이 분대장 자리보다 뒤로 넘어가면 지휘관 보호 대형이 역전된다.
        if (member != leader && leader != null && leader.IsAlive &&
            reservations.TryGetValue(leader, out Reservation leaderReservation) && leaderReservation.HasPosition &&
            depth > Vector3.Dot(Flat(leaderReservation.Position - front), rear) - leaderRearSpacing)
            return false;

        bool isOverDistance = Flat(position - desired).sqrMagnitude > maxSlotDeviation * maxSlotDeviation;

        if (isOverDistance)
            return false;

        return depth >= minimumDepth - 0.05f &&
            Flat(position - threat).sqrMagnitude >= supportThreatClearanceSqr &&
            Mathf.Abs(position.y - member.transform.position.y) <= 2f;
    }

    private static bool IsUsableCover(DestructibleCover cover)
    {
        return cover != null && cover.IsAvailable && cover.CoverCollider != null &&
            cover.CoverCollider.enabled && cover.CoverCollider.gameObject.activeInHierarchy && !cover.CoverCollider.isTrigger;
    }

    private static bool IsBehindCover(Collider collider, Vector3 position, Vector3 threat)
    {
        // Bounds만으로 판정하면 비어 있는 모서리도 엄폐로 오인한다. 실제 충돌체가 그 사이에 있는지 확인한다.
        Vector3 origin = position;
        origin.y = collider.bounds.center.y;
        Vector3 target = threat;
        target.y = origin.y;
        Vector3 offset = target - origin;
        return collider.Raycast(new Ray(origin, offset.normalized), out RaycastHit hit, offset.magnitude);
    }

    private bool TryGetPathLength(Vector3 start, Vector3 destination, NavMeshQueryFilter filter, out float pathLength)
    {
        pathLength = 0f;

        if (!NavMesh.CalculatePath(start, destination, filter, path) || path.status != NavMeshPathStatus.PathComplete)
            return false;

        int count = path.GetCornersNonAlloc(pathCorners);

        // 버퍼가 꽉 찼다면 경로 전체를 담았다고 확신할 수 없으므로 실패 처리한다.
        if (count >= pathCorners.Length)
            return false;

        for (int i = 1; i < count; i++)
        {
            pathLength += Vector3.Distance(pathCorners[i - 1], pathCorners[i]);
        }

        return true;
    }

    private bool HasCompletePath(NavMeshAgent agent, Vector3 destination, NavMeshQueryFilter filter,
        Vector3 threat, float minimumDistance, out float pathLength)
    {
        pathLength = 0f;

        if (!NavMesh.CalculatePath(agent.nextPosition, destination, filter, path) || path.status != NavMeshPathStatus.PathComplete)
            return false;
        int count = path.GetCornersNonAlloc(pathCorners);
        if (count >= pathCorners.Length)
            return false;

        float totalPathLength = 0f;

        float minimumSqr = Mathf.Min(minimumDistance * minimumDistance, Flat(agent.nextPosition - threat).sqrMagnitude);
        for (int i = 1; i < count; i++)
        {
            Vector3 segment = Flat(pathCorners[i] - pathCorners[i - 1]);
            Vector3 toThreat = Flat(threat - pathCorners[i - 1]);
            float t = segment.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(toThreat, segment) / segment.sqrMagnitude) : 0f;
            if ((toThreat - segment * t).sqrMagnitude < minimumSqr - 0.1f)
                return false;

            totalPathLength += Vector3.Distance(pathCorners[i - 1], pathCorners[i]);
        }

        // 장애물을 고려하지 않은 직선 거리
        float directDistance = Vector3.Distance(agent.nextPosition, destination);

        // 실제 경로 길이가 직선 거리보다 너무 길면 우회가 심한 것으로 판단한다.
        // 이를 위해 직선 거리에 우회 허용 비율과 추가 허용 거리를 곱해 최대 허용 경로 길이를 계산한다.
        float allowedPathLength = directDistance * maxSupportDetourRatio + supportDetourAllowance;

        // 경로 길이가 허용 범위를 초과하면 유효하지 않은 경로로 간주한다.
        if (totalPathLength > allowedPathLength)
            return false;

        pathLength = totalPathLength;
        return true;
    }

    private bool IsWithinFormationPathDistance(Vector3 candidate, Vector3 frontPosition, Vector3 followOffset, NavMeshQueryFilter filter)
    {
        // 후보 위치와 전진 대원 사이의 실제 경로 길이를 계산한다.
        if (!TryGetPathLength(candidate, frontPosition, filter, out float pathLength))
            return false;

        float allowedLength = followOffset.magnitude + maxSlotDeviation;

        return pathLength <= allowedLength;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 디버그 표시는 판단·경로 탐색을 실행하지 않고 현재 명령과 예약만 읽는다.
        if (!showFormationGizmos || !Application.isPlaying ||
            UnityEditor.SceneView.currentDrawingSceneView == null)
            return;

        Color previousColor = UnityEditor.Handles.color;
        DrawMemberPosition(leader);
        Vector3 firstSlot = default;
        Vector3 previousSlot = default;
        int slotCount = 0;
        if (assaults != null)
        {
            for (int i = 0; i < assaults.Length; i++)
            {
                SquadMemberBrain member = assaults[i];
                if (!CanDrawMember(member))
                    continue;

                DrawMemberPosition(member);
                Vector3 slot = GetFormationSlot(member) + Vector3.up * 0.15f;
                UnityEditor.Handles.color = Color.cyan;
                if (slotCount == 0)
                    firstSlot = slot;
                else
                    UnityEditor.Handles.DrawDottedLine(previousSlot, slot, 5f);
                previousSlot = slot;
                slotCount++;
            }
        }
        if (slotCount >= 3)
            UnityEditor.Handles.DrawDottedLine(previousSlot, firstSlot, 5f);
        if (CanDrawMember(leader))
        {
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(leader.transform.position + Vector3.up * 2.5f,
                "대형: 청록=슬롯/허용 범위  초록=선정 자리  흰색=실제 위치  빨강=자리 없음");
        }
        UnityEditor.Handles.color = previousColor;
    }

    private void DrawMemberPosition(SquadMemberBrain member)
    {
        if (!CanDrawMember(member))
            return;

        Vector3 actual = member.transform.position + Vector3.up * 0.15f;
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.DrawWireDisc(actual, Vector3.up, 0.2f);
        if (!member.HasCommand)
            return;

        SquadCommand command = member.CurrentCommand;
        if (command.Type == SquadCommandType.Support && command.FollowTarget != null)
        {
            Vector3 ideal = GetFormationSlot(member) + Vector3.up * 0.15f;
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.DrawWireDisc(ideal, Vector3.up, 0.25f);
            UnityEditor.Handles.DrawWireDisc(ideal, Vector3.up, maxSlotDeviation);
            UnityEditor.Handles.DrawDottedLine(
                command.FollowTarget.position + Vector3.up * 0.15f, ideal, 5f);
            if (reservations.TryGetValue(member, out Reservation reservation) && reservation.HasPosition)
            {
                Vector3 selected = reservation.Position + Vector3.up * 0.15f;
                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.DrawWireDisc(selected, Vector3.up, 0.3f);
                UnityEditor.Handles.DrawLine(ideal, selected);
                UnityEditor.Handles.DrawDottedLine(actual, selected, 3f);
                if (reservation.Cover != null && reservation.Cover.CoverCollider != null)
                    UnityEditor.Handles.DrawDottedLine(selected,
                        reservation.Cover.CoverCollider.bounds.center, 3f);
            }
            else
            {
                UnityEditor.Handles.color = Color.red;
                UnityEditor.Handles.DrawWireDisc(ideal, Vector3.up, 0.35f);
            }
        }
        else if (command.Type == SquadCommandType.MoveTo || command.Type == SquadCommandType.Investigate)
        {
            Vector3 destination = command.Destination + Vector3.up * 0.15f;
            UnityEditor.Handles.color = Color.green;
            UnityEditor.Handles.DrawWireDisc(destination, Vector3.up, 0.3f);
            UnityEditor.Handles.DrawDottedLine(actual, destination, 3f);
        }
    }

    private static Vector3 GetFormationSlot(SquadMemberBrain member)
    {
        SquadCommand command = member.CurrentCommand;
        return member.HasCommand && command.Type == SquadCommandType.Support && command.FollowTarget != null
            ? command.FollowTarget.position + command.FollowOffset
            : member.transform.position;
    }

    private static bool CanDrawMember(SquadMemberBrain member)
    {
        return member != null && member.isActiveAndEnabled && member.IsAlive;
    }
#endif

    private static Vector3 Flat(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }
}
