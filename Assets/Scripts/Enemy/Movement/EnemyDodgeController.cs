using System;
using UnityEngine;
using UnityEngine.AI;

// 선택적으로 부착하는 회피 실행 기능이다. 회피할 시점과 다른 행동의 차단은 AI가 결정한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(HealthComponent))]
public class EnemyDodgeController : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private HealthComponent health;
    [SerializeField] private StaminaComponent stamina;
    [SerializeField] private EnemyAttackController attack;
    [SerializeField, Min(0.01f)] private float dodgeDistance = 3f;
    [SerializeField, Min(0.01f)] private float dodgeDuration = 0.55f;
    [SerializeField, Min(0f)] private float dodgeCooldown = 0.2f;
    [SerializeField, Min(0.01f)] private float dodgeStaminaCost = 20f;

    private Vector3 dodgeDirection;
    private float elapsed;
    private float nextDodgeTime;
    private bool previousStopped;
    private bool previousUpdateRotation;

    public bool IsDodging { get; private set; }
    public Vector3 DodgeDirection => dodgeDirection;
    public float Duration => dodgeDuration;
    public bool CanDodge => isActiveAndEnabled && !IsDodging && Time.time >= nextDodgeTime
        && health != null && health.IsAlive && agent != null && agent.isActiveAndEnabled
        && agent.isOnNavMesh && !agent.isOnOffMeshLink && agent.updatePosition
        && stamina != null && stamina.isActiveAndEnabled;

    // AI와 애니메이션이 회피 실행 결과를 구독할 수 있게 알림을 제공한다.
    public event Action OnDodgeStarted;
    public event Action OnDodgeEnded;

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (health == null) health = GetComponent<HealthComponent>();
        if (stamina == null) stamina = GetComponent<StaminaComponent>();
        if (attack == null) attack = GetComponent<EnemyAttackController>();
    }

    public bool TryDodge(Vector3 direction)
    {
        // 잘못된 요청은 스태미너 소비와 장전 취소 전에 거절한다.
        if (!CanDodge || !IsFinite(direction.x) || !IsFinite(direction.y) || !IsFinite(direction.z)) return false;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (direction.sqrMagnitude < 0.0001f || !stamina.TryUseStamina(dodgeStaminaCost)) return false;

        // 시작 시 방향을 고정하고 일반 이동 속도와 별개인 거리와 시간으로 이동한다.
        dodgeDirection = direction.normalized;
        elapsed = 0f;
        previousStopped = agent.isStopped;
        previousUpdateRotation = agent.updateRotation;
        agent.isStopped = true;
        agent.updateRotation = false;
        transform.rotation = Quaternion.LookRotation(dodgeDirection, Vector3.up);
        attack?.CancelReload();
        IsDodging = true;
        health.SetInvulnerable(true);
        OnDodgeStarted?.Invoke();
        return true;
    }

    private void Update()
    {
        if (!IsDodging) return;
        if (health == null || health.IsDead || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            CancelDodge();
            return;
        }
        // 마지막 프레임의 초과 이동을 막고 NavMesh의 이동 가능 영역을 따라 이동한다.
        float step = Mathf.Min(Time.deltaTime, Mathf.Max(0f, dodgeDuration - elapsed));
        agent.Move(dodgeDirection * (dodgeDistance / dodgeDuration) * step);
        elapsed += step;
        if (elapsed >= dodgeDuration) CancelDodge();
    }

    public void CancelDodge()
    {
        if (!IsDodging) return;
        IsDodging = false;
        elapsed = 0f;
        dodgeDirection = Vector3.zero;
        nextDodgeTime = Time.time + dodgeCooldown;
        health?.SetInvulnerable(false);
        // 다른 이동 제어를 훼손하지 않도록 회피 전 Agent 설정으로 복구한다.
        if (agent != null)
        {
            agent.updateRotation = previousUpdateRotation;
            if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = previousStopped;
        }
        OnDodgeEnded?.Invoke();
    }

    private void OnDisable() => CancelDodge();

    private void OnValidate()
    {
        dodgeDistance = IsFinite(dodgeDistance) ? Mathf.Max(0.01f, dodgeDistance) : 3f;
        dodgeDuration = IsFinite(dodgeDuration) ? Mathf.Max(0.01f, dodgeDuration) : 0.55f;
        dodgeCooldown = IsFinite(dodgeCooldown) ? Mathf.Max(0f, dodgeCooldown) : 0.2f;
        dodgeStaminaCost = IsFinite(dodgeStaminaCost) ? Mathf.Max(0.01f, dodgeStaminaCost) : 20f;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
