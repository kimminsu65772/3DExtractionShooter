using UnityEngine;
using UnityEngine.AI;

// 공격 상태의 이동만 담당한다. 부착하지 않은 일반 적은 기존 정지 사격을 유지한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMotor), typeof(NavMeshAgent))]
public class EnemyCombatMovement : MonoBehaviour
{
    [SerializeField] private EnemyMotor motor; // [USEC 리뷰] 목적지를 Agent에 전달하는 기존 이동 담당자다.
    [SerializeField] private NavMeshAgent agent; // [USEC 리뷰] 이 적의 Agent 타입·이동 영역·현재 NavMesh 위치를 읽는다.
    [SerializeField, Min(0.1f)] private float minimumDistance = 6f; // [USEC 리뷰] 이 거리보다 가까워지면 전투 이동 후보를 뒤쪽으로 잡는다.
    [SerializeField, Min(0.1f)] private float maximumDistance = 12f; // [USEC 리뷰] 이 거리보다 멀어지면 적정 거리로 접근하는 후보를 잡는다.
    [SerializeField, Min(0.1f)] private float strafeDistance = 3f; // [USEC 리뷰] 적정 거리 안에서는 이 길이만큼 측면 이동을 시도한다.
    [SerializeField] private Vector2 positionChangeInterval = new(1f, 3f); // [USEC 리뷰] x~y초마다 새 전투 위치를 골라 매 프레임 탐색하지 않는다.
    [SerializeField, Min(0.05f)] private float sampleRadius = 0.5f; // [USEC 리뷰] 후보를 가까운 NavMesh 점으로 보정할 수 있는 최대 반경이다.
    [SerializeField, Min(0.01f)] private float stoppingDistance = 0.2f; // [USEC 리뷰] 선정한 위치에 이 정도 접근하면 이동을 멈춘다.

    private float nextPositionTime; // [USEC 리뷰] 다음 후보 탐색을 허용할 게임 시각이다. 남은 시간이 아닌 절대 시각이다.
    private Vector3 destination; // [USEC 리뷰] 탐색 간격 사이에도 계속 사용할 선정된 목적지다.
    private bool hasDestination; // [USEC 리뷰] 기본 Vector3 값과 실제로 선정에 성공한 목적지를 구분한다.

    private void Awake() // [USEC 리뷰] 생성 시 한 번 참조를 확보해 Tick에서 GetComponent를 반복하지 않는다.
    {
        if (motor == null) motor = GetComponent<EnemyMotor>(); // [USEC 리뷰] 인스펙터에서 연결하지 않았으면 같은 루트의 Motor를 사용한다.
        if (agent == null) agent = GetComponent<NavMeshAgent>(); // [USEC 리뷰] 실제로 이 캐릭터를 이동시키는 Agent를 사용한다.
    }

    public void Tick(Vector3 targetPosition) // [USEC 리뷰] Attack 상태가 직접 호출한다. 독립 Update에서 FSM과 경쟁하지 않는다.
    {
        if (!isActiveAndEnabled || motor == null || !motor.IsReady) // [USEC 리뷰] 컴포넌트 또는 NavMesh 이동이 준비되지 않으면 아무 명령도 내리지 않는다.
            return; // [USEC 리뷰] 실패한 상태에서 Agent API를 호출하는 것을 막는다.

        if (Time.time >= nextPositionTime) // [USEC 리뷰] 예정된 탐색 시각이 지났을 때만 새 위치를 고른다.
        {
            nextPositionTime = Time.time + Random.Range(positionChangeInterval.x, positionChangeInterval.y); // [USEC 리뷰] 이번 시각에 무작위 간격을 더해 다음 탐색 시각을 예약한다.
            hasDestination = TrySelectDestination(targetPosition, out destination); // [USEC 리뷰] 현재 관측한 상대 위치로 이동 후보를 평가하고 성공 여부와 좌표를 함께 받는다.
            if (!hasDestination) motor.Stop(); // [USEC 리뷰] 적합한 후보가 없으면 오래된 목적지로 계속 가지 않고 정지 사격한다.
        }

        if (hasDestination) // [USEC 리뷰] 새 탐색 시각 전에는 저장한 목적지를 재사용한다.
            motor.TryMoveTo(destination, stoppingDistance); // [USEC 리뷰] 이동만 요청한다. 발사 여부는 같은 Attack.Tick의 뒤쪽 코드가 따로 판단한다.
    }

    public void ResetMovement() // [USEC 리뷰] 공격 진입·종료 및 회피 종료 시 전투 위치 선택의 기억을 초기화한다.
    {
        hasDestination = false; // [USEC 리뷰] 이전 교전에서 고른 목적지를 더 이상 유효한 것으로 취급하지 않는다.
        nextPositionTime = 0f; // [USEC 리뷰] 다음 Tick에서 기다리지 않고 현재 상황에 맞는 후보를 고르게 한다.
    }

    private bool TrySelectDestination(Vector3 target, out Vector3 result) // [USEC 리뷰] 상대 좌표를 입력받고 이동 가능한 전투 좌표를 out으로 돌려준다.
    {
        result = default; // [USEC 리뷰] 실패해도 out 매개변수가 반드시 초기화되게 한다. 성공 여부는 bool로 구분한다.
        Vector3 away = Vector3.ProjectOnPlane(transform.position - target, Vector3.up); // [USEC 리뷰] 상대에서 나를 향하는 벡터의 높이 성분을 제거한다.
        float distance = away.magnitude; // [USEC 리뷰] 현재 상대와의 수평 거리를 구한다.
        if (distance < 0.001f) return false; // [USEC 리뷰] 거의 같은 위치에서는 뒤·옆 방향을 안정적으로 정의할 수 없어 위치 선정을 건너뛴다.
        away /= distance; // [USEC 리뷰] 길이를 1로 만들어 방향과 거리 계산을 분리한다.

        // 계속 접근하지 않고, 적정 거리에서는 옆으로 이동하며 사격한다.
        Vector3 offset = distance < minimumDistance || distance > maximumDistance // [USEC 리뷰] 현재 거리가 적정 구간 바깥인지 먼저 판단한다.
            ? target + away * ((minimumDistance + maximumDistance) * 0.5f) - transform.position // [USEC 리뷰] 구간 중앙 거리의 목표점에서 내 좌표를 빼 접근·후퇴 이동 벡터를 만든다.
            : Vector3.Cross(Vector3.up, away) * (Random.value < 0.5f ? -strafeDistance : strafeDistance); // [USEC 리뷰] 구간 안에서는 외적으로 구한 수평 측면 방향 중 한쪽을 무작위로 고른다.
        offset.y = 0f; // [USEC 리뷰] 다음 후보를 기울어진 3D 방향이 아닌 수평 방향으로 만든다.
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }; // [USEC 리뷰] 다른 크기의 Agent나 금지된 이동 영역에 속한 NavMesh를 잘못 사용하지 않는다.

        // 벽 너머의 가까운 점으로 긴 우회를 하지 않도록 직선으로 연결된 후보만 사용한다.
        for (int i = 0; i < 5; i++) // [USEC 리뷰] 기본 방향과 그 주변 네 방향만 평가해 탐색 비용을 제한한다.
        {
            float angle = i == 0 ? 0f : ((i + 1) / 2) * 45f * (i % 2 == 1 ? 1f : -1f); // [USEC 리뷰] 정수 나눗셈과 홀짝을 이용해 0, +45, -45, +90, -90도 순서를 만든다.
            Vector3 candidate = transform.position + Quaternion.AngleAxis(angle, Vector3.up) * offset; // [USEC 리뷰] 이동 벡터를 Y축 주위로 회전시켜 내 주변 후보 좌표로 바꾼다.
            candidate.y = transform.position.y; // [USEC 리뷰] NavMesh 보정의 시작 높이를 현재 캐릭터 높이로 맞춘다.
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, filter)) continue; // [USEC 리뷰] 후보 근처에 이 Agent가 사용할 NavMesh가 없으면 다음 후보로 넘어간다.
            if (Mathf.Abs(hit.position.y - transform.position.y) > sampleRadius) continue; // [USEC 리뷰] 가까운 다른 층으로 보정된 후보를 높이 차이로 거른다.
            float targetDistance = Vector3.ProjectOnPlane(hit.position - target, Vector3.up).magnitude; // [USEC 리뷰] 보정 전 후보가 아니라 최종 NavMesh 좌표에서 상대까지의 거리를 다시 계산한다.
            if (targetDistance < minimumDistance - sampleRadius || targetDistance > maximumDistance + sampleRadius) continue; // [USEC 리뷰] 보정 오차만 허용하며 적정 사격 거리에서 크게 벗어나는 후보를 거른다.
            if ((hit.position - transform.position).sqrMagnitude < stoppingDistance * stoppingDistance) continue; // [USEC 리뷰] 이미 정지 반경 안인 점을 새 이동 목적지로 고르지 않는다.
            if (NavMesh.Raycast(agent.nextPosition, hit.position, out _, filter)) continue; // [USEC 리뷰] NavMesh 경계로 직선 연결이 끊기는 후보는 긴 우회가 필요할 수 있으므로 거른다.
            result = hit.position; // [USEC 리뷰] 모든 조건을 통과한 첫 번째 좌표를 호출자에게 넘긴다.
            return true; // [USEC 리뷰] 전역 최적점을 찾는 것이 아니라 제한된 후보 중 첫 유효점을 선택하는 정책이다.
        }
        return false; // [USEC 리뷰] 모든 후보가 탈락했음을 알려 Tick이 정지 사격을 선택하게 한다.
    }

    private void OnValidate() // [USEC 리뷰] 인스펙터 편집 시 거리와 탐색 간격의 역전·음수를 보정한다.
    {
        maximumDistance = Mathf.Max(minimumDistance, maximumDistance); // [USEC 리뷰] 최대 거리가 최소 거리보다 작아지는 설정을 막는다.
        positionChangeInterval.x = Mathf.Max(0.1f, positionChangeInterval.x); // [USEC 리뷰] 최소 탐색 간격을 양수로 제한한다.
        positionChangeInterval.y = Mathf.Max(positionChangeInterval.x, positionChangeInterval.y); // [USEC 리뷰] 무작위 간격의 상한이 하한보다 작아지지 않게 한다.
    }
}
