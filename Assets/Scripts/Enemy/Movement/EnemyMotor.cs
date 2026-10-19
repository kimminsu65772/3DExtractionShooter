using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMotor : MonoBehaviour
{
    [SerializeField] private NavMeshAgent _agent;

    [Header("이동 사격")]
    [SerializeField, Range(0f, 1f)] private float combatMovementMultiplier = 0.5f;
    private float baseMovementSpeed;
    private bool hasBaseMovementSpeed;

    [Header("경로 갱신")]
    [SerializeField, Min(0.02f)] private float _repathInterval = 0.2f;
    [SerializeField, Min(0.01f)] private float destinationChangeThreshold = 0.25f;
    [SerializeField, Min(0.01f)] private float arrivalVelocityThreshold = 0.05f;

    [Header("회전 설정")]
    [SerializeField, Min(0f)] private float rotationSpeed = 360f;
    [SerializeField, Range(0f, 180f)] private float facingTolerance = 3f; // 회전 완료로 간주되는 각도 허용치

    private Vector3 requestedDestination;
    private float nextRepathTime;
    private bool hasRequestedDestination;

    public bool IsReady => _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh;

    // 실제 수평 속도를 제공해 경사면의 높이 변화가 달리기 판정에 섞이지 않게 한다.
    public float HorizontalSpeed => IsReady ? Vector3.ProjectOnPlane(_agent.velocity, Vector3.up).magnitude : 0f;

    public bool HasReachedDestination
    {
        get
        {
            // NavMeshAgent가 준비되지 않았거나 목적지가 요청되지 않았거나 경로가 아직 계산 중이면 도착 여부를 확인할 수 없음
            if (!IsReady || !hasRequestedDestination || _agent.pathPending)
                return false;

            if (_agent.pathStatus != NavMeshPathStatus.PathComplete)
                return false;

            if (_agent.remainingDistance > _agent.stoppingDistance)
                return false;

            float velocityThresholdSqr = arrivalVelocityThreshold * arrivalVelocityThreshold;

            // NavMeshAgent가 경로를 가지고 있지 않거나 속도가 임계값보다 작은 경우 도착한 것으로 간주
            return !_agent.hasPath || _agent.velocity.sqrMagnitude < velocityThresholdSqr;
        }
    }

    private void Awake()
    {
        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();
        CacheBaseMovementSpeed();
    }

    private void OnDisable()
    {
        // 재활성화할 때 감속된 값을 기본 속도로 잘못 기억하지 않도록 복원한다.
        SetCombatMovement(false);
    }

    private void CacheBaseMovementSpeed()
    {
        if (hasBaseMovementSpeed)
            return;

        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();
        if (_agent == null)
            return;

        baseMovementSpeed = _agent.speed;
        hasBaseMovementSpeed = true;
    }

    public void SetCombatMovement(bool isAiming) // [USEC 리뷰] 기존 공용 감속 기능을 USEC 공격 상태에서도 사용한다. 위치 이동 명령과 속도 설정은 별개다.
    {
        // 다른 컴포넌트가 먼저 호출해도 원래 속도는 한 번만 보관한다.
        CacheBaseMovementSpeed(); // [USEC 리뷰] 감속 전 기준 속도를 한 번만 확보한다.
        if (_agent == null || !hasBaseMovementSpeed) // [USEC 리뷰] 기준값이 없으면 현재 속도를 잘못 덮어쓰지 않는다.
            return; // [USEC 리뷰] Agent가 준비되지 않은 경우 이 속도 변경만 건너뛴다.

        // 현재 속도에 반복 곱하지 않고 항상 같은 기준에서 최종 속도를 계산한다.
        _agent.speed = baseMovementSpeed * (isAiming ? Mathf.Clamp01(combatMovementMultiplier) : 1f); // [USEC 리뷰] true면 기본 속도×전투 배율, false면 기본 속도다. 현재 속도에 반복 곱해 계속 느려지지 않는다.
    }

    public bool TryMoveTo(Vector3 destination, float stoppingDistance, bool forceRepath = false)
    {
        if (!IsReady)
            return false;
        
        _agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);

        if (!forceRepath && hasRequestedDestination)
        {
            float changeThresholdSqr = destinationChangeThreshold * destinationChangeThreshold;
            bool destinationChanged = (requestedDestination - destination).sqrMagnitude > changeThresholdSqr;

            // 변경된 경로를 계산한 결과가 이전 목적지와 거의 동일하면 경로를 다시 계산하지 않고 true 반환
            if (!destinationChanged)
                return true;

            // 이전 목적지와 다른 경로를 요청했지만 너무 자주 요청하여 아직 경로를 다시 계산할 시간이 되지 않은 경우 true 반환
            if (Time.time < nextRepathTime)
                return true;
        }

        // 공격하느라 navMeshAgent의 회전을 제어하지 못하게 막았을 수 있으므로, 이동을 시작할 때는 회전 제어를 다시 허용한다.
        _agent.updateRotation = true;

        // navMeshAgent에 내장된 경로 계산 기능을 사용한다.
        // 이 때, 실제 경로는 몇 프레임 후에 계산되므로, 경로 계산이 완료될 때까지 기다려야 한다.
        if (!_agent.SetDestination(destination))
            return false;

        requestedDestination = destination;
        hasRequestedDestination = true;
        nextRepathTime = Time.time + _repathInterval;
        _agent.isStopped = false;

        return true;
    }

    public void Stop()
    {
        if (!IsReady)
            return;

        _agent.isStopped = true;

        if (_agent.hasPath)
            _agent.ResetPath();

        hasRequestedDestination = false;
    }

    public bool TryFaceTowards(Vector3 targetPosition)
    {
        Vector3 horizontalOffset = Vector3.ProjectOnPlane(targetPosition - transform.position, Vector3.up);

        if (horizontalOffset.sqrMagnitude < 0.0001f)
            return true;

        Vector3 horizontalDirection = horizontalOffset.normalized;

        Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection, Vector3.up);

        if (_agent != null)
            _agent.updateRotation = false;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );

        // 현재 방향과 목표 방행의 차이가 허용 범위 안인지 반환한다.
        return Quaternion.Angle(transform.rotation, targetRotation) <= facingTolerance;
    }

    public void SetAutomaticRotation(bool enable)
    {
        if (_agent == null)
            return;

        _agent.updateRotation = enable;
    }

    // 회피 후 현재 위치에서 기존 목적지까지 다시 계산한다. 목적지 기억은 지우지 않는다.
    public void RefreshPath() // [USEC 리뷰] 회피 후 도착 목표는 유지하면서 현재 위치에서 경로만 갱신한다.
    {
        if (!IsReady || !hasRequestedDestination) return; // [USEC 리뷰] 이동 불가이거나 기억한 목적지가 없으면 다시 계산할 대상도 없다.
        nextRepathTime = 0f; // [USEC 리뷰] 뒤이어 새 목적지를 요청할 때 일반 갱신 간격에 막히지 않도록 한다.
        _agent.SetDestination(requestedDestination); // [USEC 리뷰] 같은 목적지라도 회피 후 바뀐 Agent 위치에서 경로를 새로 요청한다.
    }
}
