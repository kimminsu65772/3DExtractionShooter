using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMotor : MonoBehaviour
{
    [SerializeField] private NavMeshAgent _agent;

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
}
