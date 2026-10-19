using UnityEngine;
using UnityEngine.AI;

// 피격 반응 정책과 회피 실행기를 분리한다. 이 컴포넌트를 붙인 적만 자동으로 회피한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyDodgeController), typeof(HealthComponent), typeof(NavMeshAgent))]
public class EnemyHitDodge : MonoBehaviour
{
    [SerializeField] private EnemyDodgeController dodge; // [USEC 리뷰] 실제 이동·무적·스태미너 소비를 맡는 기존 회피 실행기다.
    [SerializeField] private HealthComponent health; // [USEC 리뷰] 이미 피해가 적용된 뒤 발생하는 피격 이벤트를 구독한다.
    [SerializeField] private NavMeshAgent agent; // [USEC 리뷰] 회피 방향별로 갈 수 있는 NavMesh 구간을 검사한다.
    [SerializeField] private Vector2 cooldownRange = new(3f, 5f); // [USEC 리뷰] USEC의 자동 피격 반응 간격이다. 실행기의 공통 쿨타임과 별개다.

    private float nextReactionTime; // [USEC 리뷰] 다음 피격에서 자동 회피를 허용할 절대 게임 시각을 저장한다.
    public float NextReactionTime => nextReactionTime; // [USEC 리뷰] 외부에서 검사할 수는 있지만 자동 회피 예약을 임의로 바꾸지는 못하게 한다.

    private void Awake() // [USEC 리뷰] 프레임마다 찾지 않도록 필요한 실행기와 상태 컴포넌트를 한 번 확보한다.
    {
        if (dodge == null) dodge = GetComponent<EnemyDodgeController>(); // [USEC 리뷰] 같은 적 루트의 회피 실행기를 연결한다.
        if (health == null) health = GetComponent<HealthComponent>(); // [USEC 리뷰] 다른 적의 피격이 아니라 자신의 피격만 받는다.
        if (agent == null) agent = GetComponent<NavMeshAgent>(); // [USEC 리뷰] 이 적과 같은 Agent 타입·이동 영역으로 후보를 검사한다.
    }

    private void OnEnable() // [USEC 리뷰] 자동 회피 정책이 켜져 있는 동안만 피격을 듣는다.
    {
        if (health != null) health.OnDamageTaken += HandleDamageTaken; // [USEC 리뷰] 실제 피격 때만 판단하므로 Update에서 계속 피격 여부를 찾지 않는다.
    }

    private void OnDisable() // [USEC 리뷰] 비활성화된 적이 이벤트를 계속 받지 않도록 구독을 해제한다.
    {
        if (health != null) health.OnDamageTaken -= HandleDamageTaken; // [USEC 리뷰] 활성화 때 등록한 동일 핸들러를 제거한다.
    }

    private void HandleDamageTaken(DamageInfo damage) // [USEC 리뷰] 피격 원인과 현재 실행 가능 여부로 이번 회피를 결정한다.
    {
        // 치명타나 쿨타임 중 피격은 회피로 바꾸지 않는다. 첫 피해 자체도 되돌리지 않는다.
        if (!health.IsAlive || damage.attacker == null || damage.attacker == gameObject // [USEC 리뷰] 사망했거나 공격자가 없거나 자기 자신이면 반응하지 않는다.
            || dodge == null || !dodge.CanDodge || Time.time < nextReactionTime) return; // [USEC 리뷰] 실행기 조건 또는 자동 반응 쿨타임을 통과하지 못하면 이번 피격만 건너뛴다.

        Vector3 away = Vector3.ProjectOnPlane(transform.position - damage.attacker.transform.position, Vector3.up); // [USEC 리뷰] 공격자에서 나를 향하는 수평 벡터이므로 공격자와 반대로 도망가는 방향이다.
        if (away.sqrMagnitude < 0.0001f) return; // [USEC 리뷰] 같은 수평 위치에서는 공격자 반대 방향을 정의할 수 없어 요청하지 않는다.
        away.Normalize(); // [USEC 리뷰] 방향 길이를 1로 맞춰 실행기가 회피 거리만큼 이동하게 한다.
        if (!dodge.TryDodge(SelectDirection(away))) return; // [USEC 리뷰] 방향을 고른 뒤 실제 회피를 요청한다. 스태미너 부족 등으로 실패하면 쿨타임을 새로 예약하지 않는다.
        nextReactionTime = Time.time + dodge.Duration + Random.Range(cooldownRange.x, cooldownRange.y); // [USEC 리뷰] 정상 회피 종료 예정 시각에 3~5초를 더한다. 별도 타이머를 매 프레임 감소시키지 않는다.
    }

    private Vector3 SelectDirection(Vector3 away) // [USEC 리뷰] 후방을 우선하되 막혔으면 좌우까지 이동 여유를 비교한다.
    {
        Vector3 direction = away; // [USEC 리뷰] 세 방향 모두 이동 여유가 없어도 방향은 남겨 제자리 회피를 허용한다.
        float distance = dodge.Distance; // [USEC 리뷰] 이번 피격의 세 후보가 공유할 곡선 기반 예상 거리를 한 번만 계산한다.
        float bestDistanceSqr = -1f; // [USEC 리뷰] 거리 제곱은 0 이상이므로 첫 후보가 반드시 비교에 들어가게 -1로 시작한다.
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }; // [USEC 리뷰] 이 캐릭터가 실제로 이용 가능한 NavMesh만 검사한다.
        // 후방이 열려 있으면 우선한다. 막혔으면 좌우까지 비교해 이동 여유가 큰 방향을 고른다.
        Vector3 side = Vector3.Cross(Vector3.up, away) * (Random.value < 0.5f ? -1f : 1f); // [USEC 리뷰] 후방에 수직인 측면 축을 만들고 좌우 검사 순서를 무작위로 바꾼다.
        for (int i = 0; i < 3; i++) // [USEC 리뷰] 후방·첫 측면·반대 측면의 세 후보만 검사한다.
        {
            Vector3 candidate = i == 0 ? away : i == 1 ? side : -side; // [USEC 리뷰] 첫 번째는 후방, 나머지는 서로 반대인 좌우 방향이다.
            Vector3 destination = agent.nextPosition + candidate * distance; // [USEC 리뷰] Agent의 내부 위치에서 곡선 기반 예상 거리의 끝점을 만든다.
            bool blocked = NavMesh.Raycast(agent.nextPosition, destination, out NavMeshHit hit, filter); // [USEC 리뷰] 물리 Raycast가 아니다. 이동 NavMesh의 경계에 부딪히는지 검사한다.
            if (i == 0 && !blocked) return away; // [USEC 리뷰] 후방으로 목표 거리만큼 갈 수 있으면 좌우 비교 없이 후방을 우선한다.
            float distanceSqr = blocked // [USEC 리뷰] 막힌 후보도 탈락시키지 않고 실제 여유 길이로 점수를 매긴다.
                ? Vector3.ProjectOnPlane(hit.position - agent.nextPosition, Vector3.up).sqrMagnitude // [USEC 리뷰] 막혔으면 경계까지의 수평 거리 제곱을 사용한다.
                : distance * distance; // [USEC 리뷰] 열려 있으면 예상 회피 거리 전체를 사용할 수 있는 것으로 평가한다.
            if (distanceSqr <= bestDistanceSqr) continue; // [USEC 리뷰] 지금까지 선택한 후보보다 여유가 작거나 같으면 기존 선택을 유지한다.
            bestDistanceSqr = distanceSqr; // [USEC 리뷰] 가장 큰 여유 거리의 점수를 기록한다. 비교만 하므로 제곱근은 필요 없다.
            direction = candidate; // [USEC 리뷰] 점수가 갱신된 후보의 방향도 함께 기록한다.
        }
        // 목표 거리 전체가 비어 있지 않아도 회피한다. 실제 이동량은 NavMeshAgent.Move가 제한한다.
        return direction; // [USEC 리뷰] 최종 방향을 반환하며 공간 부족 자체를 회피 실패로 처리하지 않는다.
    }

    private void OnValidate() // [USEC 리뷰] 인스펙터에서 쿨타임 범위를 편집할 때 잘못된 경계를 보정한다.
    {
        cooldownRange.x = Mathf.Max(0f, cooldownRange.x); // [USEC 리뷰] 음수 쿨타임을 허용하지 않는다.
        cooldownRange.y = Mathf.Max(cooldownRange.x, cooldownRange.y); // [USEC 리뷰] 상한을 하한 이상으로 맞춰 Random.Range의 순서를 보장한다.
    }
}
