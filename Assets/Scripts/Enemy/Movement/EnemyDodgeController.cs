using System;
using UnityEngine;
using UnityEngine.AI;

// 선택적으로 부착하는 회피 실행 기능이다. 회피할 시점과 다른 행동의 차단은 AI가 결정한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(HealthComponent))]
public class EnemyDodgeController : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent; // [USEC 리뷰] 평소에는 경로를 따르고 회피 중에는 Move로 변위를 적용할 Agent다.
    [SerializeField] private HealthComponent health; // [USEC 리뷰] 생존 여부를 확인하고 회피 중 무적을 설정한다.
    [SerializeField] private StaminaComponent stamina; // [USEC 리뷰] 회피 시작에 필요한 자원을 실제로 소비한다.
    [SerializeField] private EnemyAttackController attack; // [USEC 리뷰] 회피 시작 시 진행 중인 재장전을 취소한다.
    [SerializeField, Min(0.01f)] private float dodgeSpeed = 10f; // [USEC 리뷰] 곡선 배율 1일 때의 회피 속도이며 일반 이동 속도와 분리한다.
    [SerializeField, Min(0.01f)] private float dodgeDuration = 0.5f; // [USEC 리뷰] 원본처럼 0.5초 동안 속도 곡선을 따라 이동한다.
    [SerializeField, Min(0f)] private float dodgeCooldown = 0.5f; // [USEC 리뷰] 원본의 종료 후 공통 제한이다. USEC의 자동 반응 3~5초는 별개다.
    [SerializeField] private AnimationCurve dodgeSpeedCurve = DodgeMotion.CreateOriginalSpeedCurve(); // [USEC 리뷰] 원본의 시작 가속·후반 감속을 플레이어와 같은 기본 곡선으로 설정한다.
    [SerializeField, Min(0.01f)] private float dodgeStaminaCost = 20f; // [USEC 리뷰] 시작에 한 번 지불하며 매 프레임 소비하지 않는다.

    private Vector3 dodgeDirection; // [USEC 리뷰] 회피 시작에 확정한 단위 방향이다. 도중에 공격자를 따라 휘지 않는다.
    private float elapsed; // [USEC 리뷰] 현재 회피에서 실제로 처리한 시간의 합이다.
    private float nextDodgeTime; // [USEC 리뷰] 공통 실행기에서 다음 회피를 허용할 절대 시각이다.
    private bool previousStopped; // [USEC 리뷰] 종료 후 복구할 기존 Agent의 경로 이동 정지 상태다.
    private bool previousUpdateRotation; // [USEC 리뷰] 종료 후 복구할 기존 Agent의 자동 회전 설정이다.

    public bool IsDodging { get; private set; } // [USEC 리뷰] Brain과 Animator가 읽는 실제 실행 상태이며 외부에서는 직접 바꾸지 못한다.
    public Vector3 DodgeDirection => dodgeDirection; // [USEC 리뷰] 회피 중 고정된 방향을 조회할 수 있게 한다.
    public float Duration => dodgeDuration; // [USEC 리뷰] Animator 재생 속도와 피격 반응 쿨타임 계산에 사용한다.
    public float Distance => DodgeMotion.EstimateDistance(dodgeSpeed, dodgeDuration, dodgeSpeedCurve); // [USEC 리뷰] 곡선의 예상 이동 거리로 후보를 검사한다. 고정 거리로 이동량을 강제하지 않는다.
    public bool CanDodge => isActiveAndEnabled && !IsDodging && Time.time >= nextDodgeTime // [USEC 리뷰] 실행기가 켜져 있고 회피 중이 아니며 공통 쿨타임이 지나야 한다.
        && health != null && health.IsAlive && agent != null && agent.isActiveAndEnabled // [USEC 리뷰] 살아 있는 적과 활성 Agent가 필요하다.
        && agent.isOnNavMesh && !agent.isOnOffMeshLink && agent.updatePosition // [USEC 리뷰] 일반 NavMesh 이동 중이며 Agent가 위치를 제어하는 경우에만 허용한다.
        && stamina != null && stamina.isActiveAndEnabled; // [USEC 리뷰] 자원 담당자가 있어야 한다. 잔량이 충분한지는 TryUseStamina가 최종 검사한다.

    // AI와 애니메이션이 회피 실행 결과를 구독할 수 있게 알림을 제공한다.
    public event Action OnDodgeStarted; // [USEC 리뷰] 모든 실행 상태를 설정한 다음 구르기 연출 시작을 알린다.
    public event Action OnDodgeEnded; // [USEC 리뷰] 정상 완료와 중도 취소 모두 여기로 모여 Brain과 Animator의 복귀를 알린다.

    private void Awake() // [USEC 리뷰] 같은 적의 실행에 필요한 컴포넌트를 한 번 연결한다.
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>(); // [USEC 리뷰] 자신의 Agent만 정지·이동·복구한다.
        if (health == null) health = GetComponent<HealthComponent>(); // [USEC 리뷰] 자신의 생존·무적 상태를 다룬다.
        if (stamina == null) stamina = GetComponent<StaminaComponent>(); // [USEC 리뷰] 자신의 스태미너에서 비용을 지불한다.
        if (attack == null) attack = GetComponent<EnemyAttackController>(); // [USEC 리뷰] 있으면 자신의 재장전만 취소한다.
    }

    public bool TryDodge(Vector3 direction) // [USEC 리뷰] 방향을 입력받아 시작 가능한지 검사하고 실제 시작 성공 여부를 반환한다.
    {
        // 잘못된 요청은 스태미너 소비와 장전 취소 전에 거절한다.
        if (!CanDodge || !IsFinite(direction.x) || !IsFinite(direction.y) || !IsFinite(direction.z)) return false; // [USEC 리뷰] 실행 불가나 NaN·무한대 방향은 비용을 내기 전에 거절한다.
        direction.y = 0f; // [USEC 리뷰] 회피 요청에서 위아래 방향을 제거한다.
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up); // [USEC 리뷰] 호출자가 영벡터를 보냈으면 현재 정면을 대체 방향으로 사용한다.
        if (direction.sqrMagnitude < 0.0001f || !stamina.TryUseStamina(dodgeStaminaCost)) return false; // [USEC 리뷰] 대체 방향도 없거나 비용 지불에 실패했으면 회피를 시작하지 않는다.

        // 시작 시 방향을 고정하고 일반 이동 속도와 별개인 회피 속도·곡선으로 이동한다.
        dodgeDirection = direction.normalized; // [USEC 리뷰] 입력 벡터의 크기가 회피 속도에 섞이지 않게 방향만 보관한다.
        elapsed = 0f; // [USEC 리뷰] 이번 회피의 경과 시간을 처음부터 센다.
        previousStopped = agent.isStopped; // [USEC 리뷰] 이미 대기 중이었는지 이동 중이었는지를 보관한다.
        previousUpdateRotation = agent.updateRotation; // [USEC 리뷰] 이동 방향 회전과 조준 방향 회전 중 무엇을 쓰고 있었는지 보관한다.
        agent.isStopped = true; // [USEC 리뷰] 기존 목적지를 향한 자동 이동만 잠시 멈춘다. ResetPath를 하지 않아 목적지는 유지한다.
        agent.updateRotation = false; // [USEC 리뷰] Agent가 회피 방향 회전을 덮어쓰지 못하게 한다.
        transform.rotation = Quaternion.LookRotation(dodgeDirection, Vector3.up); // [USEC 리뷰] 모델의 정면을 실제 구르는 방향에 맞춘다.
        attack?.CancelReload(); // [USEC 리뷰] 회피 도중 재장전이 완료되는 중첩 행동을 막는다.
        IsDodging = true; // [USEC 리뷰] Brain이 다음 Update부터 일반 이동·사격 처리를 건너뛰게 한다.
        health.SetInvulnerable(true); // [USEC 리뷰] 첫 피해가 처리된 이후부터 회피 중 추가 피해를 막는다.
        OnDodgeStarted?.Invoke(); // [USEC 리뷰] 시작 상태를 모두 갱신한 뒤 Animator 등의 구독자에게 알린다.
        return true; // [USEC 리뷰] 실제 회피 시작이 성공했으므로 호출자가 쿨타임을 예약해도 된다.
    }

    private void Update() // [USEC 리뷰] 자동 피격 판단이 아니라 시작된 회피의 시간과 위치만 진행한다.
    {
        if (!IsDodging) return; // [USEC 리뷰] 평소에는 회피 변위를 적용하지 않는다.
        if (health == null || health.IsDead || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) // [USEC 리뷰] 중도 사망·비활성화·NavMesh 이탈로 이동을 계속할 수 없는지 확인한다.
        {
            CancelDodge(); // [USEC 리뷰] 무적과 Agent 설정을 공통 종료 절차에서 복구한다.
            return; // [USEC 리뷰] 종료한 프레임에는 이동 코드를 실행하지 않는다.
        }
        // 마지막 프레임의 초과 이동을 막고 NavMesh의 이동 가능 영역을 따라 이동한다.
        float step = Mathf.Min(Time.deltaTime, Mathf.Max(0f, dodgeDuration - elapsed)); // [USEC 리뷰] 남은 시간보다 프레임 시간이 길면 남은 만큼만 처리해 목표 거리를 초과하지 않는다.
        float speed = DodgeMotion.EvaluateSpeed(dodgeSpeed, dodgeSpeedCurve, elapsed / dodgeDuration); // [USEC 리뷰] 현재 진행률의 곡선 배율을 읽어 가감속된 속도를 계산한다.
        agent.Move(dodgeDirection * speed * step); // [USEC 리뷰] 방향 × 현재 속도 × 처리 시간만큼 이동한다. 실제 이동은 NavMesh 경계로 제한된다.
        elapsed += step; // [USEC 리뷰] 이번 프레임에서 처리한 시간만 누적한다.
        if (elapsed >= dodgeDuration) CancelDodge(); // [USEC 리뷰] 목표 시간에 도달하면 중도 취소와 같은 복구 절차로 종료한다.
    }

    public void CancelDodge() // [USEC 리뷰] 정상 종료·사망·비활성화에서 공통으로 사용하는 정리 메서드다.
    {
        if (!IsDodging) return; // [USEC 리뷰] 중복 종료 호출이 상태와 이벤트를 두 번 바꾸지 않게 한다.
        IsDodging = false; // [USEC 리뷰] Brain의 일반 행동과 Animator 복귀가 가능하다는 상태를 먼저 공개한다.
        elapsed = 0f; // [USEC 리뷰] 다음 회피에 이전 경과 시간이 섞이지 않게 한다.
        dodgeDirection = Vector3.zero; // [USEC 리뷰] 진행 중이 아닌 회피 방향을 유효 정보처럼 노출하지 않는다.
        nextDodgeTime = Time.time + dodgeCooldown; // [USEC 리뷰] 종료 시각 기준으로 실행기 공통 쿨타임을 예약한다.
        health?.SetInvulnerable(false); // [USEC 리뷰] 회피가 끝났으므로 추가 피해를 다시 받게 한다.
        // 다른 이동 제어를 훼손하지 않도록 회피 전 Agent 설정으로 복구한다.
        if (agent != null) // [USEC 리뷰] Agent가 아직 존재할 때만 이전 설정을 복구한다.
        {
            agent.updateRotation = previousUpdateRotation; // [USEC 리뷰] 시작 전의 자동 회전 정책으로 돌린다.
            if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = previousStopped; // [USEC 리뷰] 이동 가능한 Agent에만 정지 상태를 복구해 잘못된 API 호출을 피한다.
        }
        OnDodgeEnded?.Invoke(); // [USEC 리뷰] 복구가 끝난 뒤 Brain은 재판단하고 Animator는 구르기를 해제한다.
    }

    private void OnDisable() => CancelDodge(); // [USEC 리뷰] 도중에 꺼져도 무적이나 이동 정지가 남지 않도록 정리한다.

    private void OnValidate() // [USEC 리뷰] 인스펙터 값 때문에 0 나눗셈이나 잘못된 수치가 들어오는 것을 막는다.
    {
        dodgeSpeed = IsFinite(dodgeSpeed) ? Mathf.Max(0.01f, dodgeSpeed) : 10f; // [USEC 리뷰] 유한한 양수 속도만 허용한다.
        dodgeDuration = IsFinite(dodgeDuration) ? Mathf.Max(0.01f, dodgeDuration) : 0.5f; // [USEC 리뷰] 진행률 계산의 분모가 0이 되지 않게 한다.
        dodgeCooldown = IsFinite(dodgeCooldown) ? Mathf.Max(0f, dodgeCooldown) : 0.5f; // [USEC 리뷰] 음수 또는 유한하지 않은 공통 쿨타임을 보정한다.
        if (dodgeSpeedCurve == null || dodgeSpeedCurve.length == 0) dodgeSpeedCurve = DodgeMotion.CreateOriginalSpeedCurve(); // [USEC 리뷰] 비어 있는 곡선은 원본 기본 곡선으로 복구한다.
        dodgeStaminaCost = IsFinite(dodgeStaminaCost) ? Mathf.Max(0.01f, dodgeStaminaCost) : 20f; // [USEC 리뷰] 자원 사용 API가 받을 수 있는 유한한 양수 비용을 보장한다.
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value); // [USEC 리뷰] NaN과 양·음의 무한대가 아닌 정상 실수인지 검사한다.
}
