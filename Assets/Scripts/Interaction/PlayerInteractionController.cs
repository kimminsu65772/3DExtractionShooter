using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerInteractionController : MonoBehaviour
{
    [Header("참조 설정")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private HealthComponent healthComponent;

    [Header("가까운 전방 감지 설정")]
    // 몸의 중심 가까이에서 거리와 벽 가림을 검사한다. 모델 본 대신 플레이어 루트를 기준으로 한다.
    [SerializeField] private Vector3 originOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.5f;
    // 전체 허용 각도를 나타낸다. 90도라면 앞쪽 좌우 45도까지만 후보로 인정한다.
    [SerializeField, Range(1f, 180f)] private float forwardAngle = 90f;
    // 처음에는 모든 레이어를 검사한다. 물체가 많아지면 상호작용 전용 레이어만 지정한다.
    [SerializeField] private LayerMask interactionMask = ~0;
    // 현재 프로젝트의 Ground와 Obstacle 레이어를 벽 가림 검사에 사용한다.
    [SerializeField] private LayerMask obstructionMask = (1 << 6) | (1 << 9);

    // 감지 배열을 재사용한다. 예외적으로 배열이 꽉 찰 때만 확장하고 다음 검사에서도 재사용한다.
    private Collider[] overlapBuffer = new Collider[32];
    private RaycastHit[] obstructionBuffer = new RaycastHit[16];
    private const int MaximumQueryCapacity = 512;
    private bool hasReportedSaturation;
    private IInteractable currentTarget;

    public IInteractable CurrentTarget => currentTarget;
    public Vector3 InteractionOrigin => transform.TransformPoint(originOffset);

    // 열린 창의 거리만 확인한다. 바라보는 방향을 바꿨다는 이유로 창을 닫지 않는다.
    public bool IsWithinInteractionDistance(Collider target)
    {
        if (target == null) return false;
        Vector3 origin = InteractionOrigin;
        return (target.ClosestPoint(origin) - origin).sqrMagnitude <= interactionDistance * interactionDistance;
    }

    // 안내 UI는 대상이 바뀔 때만 갱신하고, 동작 결과는 성공했을 때만 받도록 한다.
    public event Action<IInteractable> OnTargetChanged;
    public event Action<IInteractable> OnInteracted;

    private void Awake()
    {
        if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();
        if (healthComponent == null) healthComponent = GetComponent<HealthComponent>();
    }

    private void LateUpdate()
    {
        // 이동과 회전이 끝난 뒤 현재 위치로 감지한다. 이동과 회피 자체를 제한하지 않는다.
        RefreshTarget();
        if (inputHandler != null && inputHandler.ConsumeInteractInput()) TryInteract();
    }

    private void OnDisable()
    {
        // 플레이어나 컴포넌트가 꺼지면 이전 대상 안내를 남기지 않는다.
        SetTarget(null);
    }

    public void RefreshTarget()
    {
        if (!isActiveAndEnabled || (healthComponent != null && healthComponent.IsDead))
        {
            SetTarget(null);
            return;
        }

        Vector3 origin = InteractionOrigin;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        float minimumDot = Mathf.Cos(forwardAngle * 0.5f * Mathf.Deg2Rad);
        float nearestDistanceSquared = interactionDistance * interactionDistance;
        IInteractable nearest = null;
        int nearestInstanceId = int.MaxValue;
        int count = QueryNearbyColliders(origin);

        for (int i = 0; i < count; i++)
        {
            Collider candidateCollider = overlapBuffer[i];
            if (candidateCollider == null || !candidateCollider.enabled || candidateCollider.transform.IsChildOf(transform)) continue;

            // 자식의 Collider를 통해 부모의 상자 또는 바닥 아이템 컴포넌트를 찾는다.
            IInteractable candidate = candidateCollider.GetComponentInParent<IInteractable>();
            if (!IsAvailable(candidate, out Component component) || component.transform.IsChildOf(transform)) continue;

            // 중심점 대신 실제 표면까지의 거리로 비교해 큰 상자도 가까운 쪽에서 사용할 수 있게 한다.
            Vector3 closestPoint = candidateCollider.ClosestPoint(origin);
            Vector3 offset = closestPoint - origin;
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared > nearestDistanceSquared) continue;

            Vector3 flatDirection = offset;
            flatDirection.y = 0f;
            // 큰 Trigger 안에서는 가장 가까운 점이 감지 시작점과 같다. 이때는 Collider 중심으로 방향을 확인한다.
            if (flatDirection.sqrMagnitude < 0.0001f)
            {
                flatDirection = candidateCollider.bounds.center - origin;
                flatDirection.y = 0f;
            }
            // 방향을 구할 수 없는 완전 겹침은 전방 대상이라고 추측하지 않고 제외한다.
            if (flatDirection.sqrMagnitude < 0.0001f || Vector3.Dot(forward, flatDirection.normalized) < minimumDot) continue;
            // Trigger 안에서도 벽 검사를 생략하지 않도록 완전히 겹친 경우에는 중심까지 검사한다.
            Vector3 visibilityPoint = offset.sqrMagnitude < 0.0001f ? candidateCollider.bounds.center : closestPoint;
            if (!candidate.CanInteract(this) || IsObstructed(origin, visibilityPoint, component)) continue;

            // 동일 거리에서는 인스턴스 번호로 결정해 물리 검색 결과 순서가 바뀌어도 대상이 흔들리지 않게 한다.
            int instanceId = component.GetInstanceID();
            if (distanceSquared == nearestDistanceSquared && instanceId >= nearestInstanceId) continue;
            nearestDistanceSquared = distanceSquared;
            nearestInstanceId = instanceId;
            nearest = candidate;
        }

        SetTarget(nearest);
    }

    public bool TryInteract()
    {
        // 키 입력 직전에 다시 검사해 멀어진 대상이나 방금 파괴된 대상을 사용하지 않는다.
        RefreshTarget();
        IInteractable target = currentTarget;
        if (!IsAvailable(target, out _) || !target.CanInteract(this) || !target.TryInteract(this)) return false;
        OnInteracted?.Invoke(target);
        // 획득으로 물체가 사라지거나 사용 불가가 됐을 때 안내를 즉시 갱신한다.
        RefreshTarget();
        return true;
    }

    private int QueryNearbyColliders(Vector3 origin)
    {
        while (true)
        {
            // Trigger로 만든 감지 영역도 후보에 포함한다.
            int count = Physics.OverlapSphereNonAlloc(origin, interactionDistance, overlapBuffer, interactionMask, QueryTriggerInteraction.Collide);
            if (count < overlapBuffer.Length) return count;
            if (overlapBuffer.Length >= MaximumQueryCapacity)
            {
                ReportSaturation();
                return 0;
            }
            // 꽉 찬 결과는 가장 가까운 물체를 포함한다고 보장할 수 없으므로 더 큰 배열로 다시 검사한다.
            Array.Resize(ref overlapBuffer, Math.Min(overlapBuffer.Length * 2, MaximumQueryCapacity));
        }
    }

    private bool IsObstructed(Vector3 origin, Vector3 targetPoint, Component targetComponent)
    {
        Vector3 offset = targetPoint - origin;
        float distance = offset.magnitude;
        if (distance <= 0.001f) return false;

        while (true)
        {
            // 바닥 아이템의 감지 Trigger는 벽으로 취급하지 않고 실제 벽 Collider만 검사한다.
            int count = Physics.RaycastNonAlloc(origin, offset / distance, obstructionBuffer, distance, obstructionMask, QueryTriggerInteraction.Ignore);
            if (count == obstructionBuffer.Length)
            {
                if (obstructionBuffer.Length >= MaximumQueryCapacity)
                {
                    ReportSaturation();
                    return true;
                }
                Array.Resize(ref obstructionBuffer, Math.Min(obstructionBuffer.Length * 2, MaximumQueryCapacity));
                continue;
            }

            for (int i = 0; i < count; i++)
            {
                Collider blocker = obstructionBuffer[i].collider;
                if (blocker == null || blocker.transform.IsChildOf(transform)) continue;
                // 상자 자신의 몸체가 Obstacle 레이어에 있어도 자기 자신을 가림벽으로 판단하지 않는다.
                if (blocker.transform.IsChildOf(targetComponent.transform)) continue;
                return true;
            }
            return false;
        }
    }

    private static bool IsAvailable(IInteractable candidate, out Component component)
    {
        // 인터페이스 참조는 Unity의 파괴된 오브젝트 비교를 우회하므로 Component로 바꾸어 생존을 확인한다.
        component = candidate as Component;
        return component != null && component.gameObject.activeInHierarchy && (!(component is Behaviour behaviour) || behaviour.isActiveAndEnabled);
    }

    private void SetTarget(IInteractable target)
    {
        if (ReferenceEquals(currentTarget, target)) return;
        currentTarget = target;
        OnTargetChanged?.Invoke(target);
    }

    private void ReportSaturation()
    {
        // 밀집 구간에서 경고가 매 프레임 반복되지 않도록 한 번만 출력한다.
        if (hasReportedSaturation) return;
        hasReportedSaturation = true;
        Debug.LogWarning("상호작용 물리 검색이 512개 상한에 도달했다. 감지 레이어와 Collider 구성을 확인한다.", this);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = InteractionOrigin;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, interactionDistance);
        // 구는 거리 한계이고 두 선 사이는 실제로 허용하는 전방 방향임을 표시한다.
        Gizmos.DrawLine(origin, origin + Quaternion.AngleAxis(-forwardAngle * 0.5f, Vector3.up) * forward * interactionDistance);
        Gizmos.DrawLine(origin, origin + Quaternion.AngleAxis(forwardAngle * 0.5f, Vector3.up) * forward * interactionDistance);
    }
}
