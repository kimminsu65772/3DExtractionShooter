using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class FOV : MonoBehaviour
{
    [Min(0.1f)]
    [SerializeField] float viewRadius;
    [Range(0.1f, 360)]
    [SerializeField] float viewAngle;
    [Min(0f)]
    [SerializeField] private float awarenessRadius;

    [Header("시야 원점")]
    [SerializeField] private Transform viewOrigin;

    [Header("인지 범위 전용 원형 메시")]
    [SerializeField, Min(3)] private int awarenessSegments;
    [SerializeField] private MeshFilter awarenessMeshFilter;

    private Mesh awarenessMesh;

    [SerializeField] LayerMask targetMask;
    [SerializeField] LayerMask visionBlockMask;

    public float ViewRadius => viewRadius;
    
    public float ViewAngle => viewAngle;

    public float AwarenessRadius => awarenessRadius;

    public LayerMask TargetMask => targetMask;
    public LayerMask VisionBlockMask => visionBlockMask;

    private List<VisibilityTarget> visibleTargets = new();
    public IReadOnlyList<VisibilityTarget> VisibleTargets => visibleTargets;

    private readonly HashSet<VisibilityTarget> scannedTargets = new();

    private readonly HashSet<VisibilityTarget> currentVisibleTargets = new();

    private readonly HashSet<VisibilityTarget> nextVisibleTargets = new();

    // 1도당 몇 개의 Ray를 쏠 것인지 결정하는 변수. 1도당 1개의 Ray를 쏘면 360개의 Ray를 쏘게 된다.
    [SerializeField, Min(0.01f)] private float meshResolution;

    [SerializeField, Min(0.02f)]
    private float targetRefreshInterval = 0.2f;

    [SerializeField, Min(1)]
    private int initialTargetBufferSize = 32;

    [SerializeField, Min(1)]
    private int maxTargetBufferSize = 128;


    // 시야각의 경계를 구할 때 얼마나 반복할지 여부
    [SerializeField] private int edgeResolveIterations;

    [SerializeField] private float edgeDstThreshold;

    [SerializeField] private MeshFilter viewMeshFilter;
    private Mesh viewMesh;

    private Coroutine targetSearchCoroutine;
    private WaitForSeconds targetSearchWait;

    private Collider[] targetBuffer;
    private bool hasWarnedTargetBufferFull;

    public Vector3 ViewOriginPosition =>
        viewOrigin != null
            ? viewOrigin.position
            : transform.position;

    // Raycast로 구한 시야 경계의 월드 좌표
    private readonly List<Vector3> viewPoints = new();

    // Mesh에 전달할 로컬 좌표
    private readonly List<Vector3> meshVertices = new();

    // 삼각형을 구성하는 정점 인덱스
    private readonly List<int> meshTriangles = new();

    private void OnValidate()
    {
        ClampSettings();
    }

    private void Awake()
    {
        ClampSettings();

        if (viewMeshFilter == null &&
            !TryGetComponent(out viewMeshFilter))
        {
            Debug.LogError(
                "FOV 메시를 출력할 MeshFilter가 설정되지 않았습니다.",
                this
            );

            enabled = false;
            return;
        }

        // 근거리 원형 Mesh를 받을 MeshFilter가 연결되었는지 확인한다.
        if (awarenessMeshFilter == null)
        {
            Debug.LogError(
                "근거리 원형 시야를 출력할 MeshFilter가 설정되지 않았습니다.",
                this
            );

            enabled = false;
            return;
        }

        viewMesh = new Mesh
        {
            name = "View Mesh"
        };

        // 매 프레임 정점이 변경되는 Mesh라는 사실을 Unity에 알려준다.
        viewMesh.MarkDynamic();

        // 생성한 Mesh를 시야각 MeshFilter에 연결한다.
        viewMeshFilter.sharedMesh = viewMesh;

        targetSearchWait = new WaitForSeconds(targetRefreshInterval);

        targetBuffer = new Collider[initialTargetBufferSize];

        awarenessMesh = new Mesh
        {
            name = "Awareness View Mesh"
        };

        awarenessMesh.MarkDynamic();

        awarenessMeshFilter.sharedMesh = awarenessMesh;
    }

    private void OnEnable()
    {
        if (viewMesh == null)
            return;

        targetSearchCoroutine = StartCoroutine(FindTargetsWithDelay());
    }

    private void OnDisable()
    {
        if (targetSearchCoroutine != null)
        {
            StopCoroutine(targetSearchCoroutine);
            targetSearchCoroutine = null;
        }

        HideAllVisibleTargets();
    }

    private void OnDestroy()
    {
        // 전방 부채꼴 Mesh가 생성된 상태인지 확인한다.
        if (viewMesh != null)
        {
            // 실행 중 직접 만든 전방 부채꼴 Mesh를 제거한다.
            Destroy(viewMesh);

            // 제거한 Mesh를 더 이상 참조하지 않도록 비운다.
            viewMesh = null;
        }

        if (awarenessMesh != null)
        {
            Destroy(awarenessMesh);
            awarenessMesh = null;
        }
    }

    private void LateUpdate()
    {
        DrawFOV();

        DrawAwarenessMesh();
    }

    // 일반적인 수학 좌표계에서 0도는 X축이다. (오른쪽)
    // 그래서 원래는 x축에 Cos, z축에 Sin을 적용해야 한다.
    // 그러나 유니티에서 0도는 Z축이다. (앞쪽)
    // 그래서 Unity 각도를 수학 각도로 대응하려면 90 - angle을 적용해야 한다.
    // x = cos(90 - angle), z = sin(90 - angle)
    // 그런데 cos(90 - angle) = sin(angle), sin(90 - angle) = cos(angle) 이므로
    // 그냥 x축에 sin, z축에 cos를 적용하면 각도에 맞는 방향 벡터를 얻을 수 있다.

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGolbal)
    {
        // 먼저 파라미터를 통해 들어온 angleInDegrees가 global 좌표계 기준인지 local 좌표계 기준인지 확인한다.
        // 만약 로컬 좌표계 기준이라면, transform.eulerAngles.y를 더해주어 global 좌표계 기준으로 변환한다.
        if (!angleIsGolbal)
        {
            angleInDegrees += transform.eulerAngles.y;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }

    private IEnumerator FindTargetsWithDelay()
    {
        // 모든 대상의 Awake가 끝난 후에 탐지를 시작해야 하므로, 한 프레임 대기한다.
        yield return null;

        while (true)
        {
            FindVisibleTargets();
            yield return targetSearchWait;
        }
    }

    private void FindVisibleTargets()
    {
        visibleTargets.Clear();
        scannedTargets.Clear();
        nextVisibleTargets.Clear();

        // OverlapSphere를 통해 viewRadius 반경 안에 있는 모든 targetMask 레이어의 Collider를 가져온다.
        int targetCount = CollectTargetsInViewRadius();

        Vector3 origin = ViewOriginPosition;

        Vector3 horizontalForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        float minimumDot = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);

        for (int i = 0; i < targetCount; i++)
        {
            Collider targetCollider = targetBuffer[i];

            if (targetCollider == null)
                continue;

            VisibilityTarget target = targetCollider.GetComponentInParent<VisibilityTarget>();

            if (target == null)
                continue;

            // 이미 스캔한 대상이라면 중복으로 처리하지 않는다.
            if (!scannedTargets.Add(target))
                continue;

            Vector3 offsetToTarget = target.ViewPoint - origin;

            Vector3 horizontalOffset = Vector3.ProjectOnPlane(offsetToTarget, Vector3.up);

            if (!IsInsideVisionShape(horizontalOffset, horizontalForward, minimumDot))
                continue;

            float dstToTarget = offsetToTarget.magnitude;

            if (dstToTarget <= Mathf.Epsilon)
                continue;

            Vector3 sightDir = offsetToTarget / dstToTarget;

            if (Physics.Raycast(origin, sightDir, dstToTarget, visionBlockMask, QueryTriggerInteraction.Ignore))
                continue;

            visibleTargets.Add(target);
            nextVisibleTargets.Add(target);
        }

        ApplyVisibilityChanges();
    }

    // 대상이 인지 범위 혹은 시야각 안에 있는가?
    private bool IsInsideVisionShape(Vector3 horizontalOffset, Vector3 horizontalForward, float minimumDot)
    {
        float horizontalDstSqr = horizontalOffset.sqrMagnitude;

        float awarenessRadiusSqr = awarenessRadius * awarenessRadius;

        // 대상이 인지 범위 안에 있는지 확인한다.
        if (horizontalDstSqr <= awarenessRadiusSqr)
            return true;

        float viewRadiusSqr = viewRadius * viewRadius;

        // 대상이 전방 시야 거리 안에 있는지 확인한다.
        if (horizontalDstSqr > viewRadiusSqr)
            return false;

        Vector3 horizontalDir = horizontalOffset.normalized;

        // 플레이어의 전방과 플레이어가 바라보는 대상 방향이 얼마나 일치하는지 확인한다. (시야각 안에 있는지 확인)
        float directionDot = Vector3.Dot(horizontalForward, horizontalDir);

        // 일치도가 최소 일치도보다 크거나 같다면 시야각 안에 있는 것으로 판단한다.
        return directionDot >= minimumDot;
    }

    private void ApplyVisibilityChanges()
    {
        foreach (VisibilityTarget target in currentVisibleTargets)
        {
            if (target == null)
                continue;

            if (!nextVisibleTargets.Contains(target))
                target.SetVisible(false);
        }

        foreach (VisibilityTarget target in nextVisibleTargets)
        {
            if (target == null)
                continue;
            if (!currentVisibleTargets.Contains(target))
                target.SetVisible(true);
        }

        currentVisibleTargets.Clear();
        currentVisibleTargets.UnionWith(nextVisibleTargets);
    }

    private void HideAllVisibleTargets()
    {
        foreach (VisibilityTarget target in currentVisibleTargets)
        {
            if (target != null)
            {
                target.SetVisible(false);
            }
        }

        currentVisibleTargets.Clear();
        nextVisibleTargets.Clear();
        scannedTargets.Clear();
        visibleTargets.Clear();
    }


    private void DrawFOV()
    {
        viewPoints.Clear();
        meshVertices.Clear();
        meshTriangles.Clear();

        int stepCount = Mathf.Max(1, Mathf.CeilToInt(viewAngle * meshResolution));
        float stepAngleSize = viewAngle / stepCount;

        // 시야각을 시각화하기 위해 Ray를 쏘아 시야각의 경계점을 구한다.
        ViewCastInfo oldViewCast = new ViewCastInfo();
        for (int i = 0; i <= stepCount; i++)
        {
            // 시야각의 시작점부터 끝점까지 stepAngleSize만큼 각도를 증가시키며 Ray를 쏜다.
            float angle = transform.eulerAngles.y - viewAngle / 2 + stepAngleSize * i;
            // 충돌 감지 메서드를 호출하여 충돌 지점의 정보를 가져온다.
            ViewCastInfo newViewCast = ViewCast(angle, viewRadius);

            if (i > 0)
            {
                bool hitStateChanged = oldViewCast.hit != newViewCast.hit;

                bool obstacleChanged = oldViewCast.hit && newViewCast.hit && oldViewCast.collider != newViewCast.collider;

                bool edgeDistanceExceeded = oldViewCast.hit && newViewCast.hit && Mathf.Abs(oldViewCast.dst - newViewCast.dst) > edgeDstThreshold;

                if (hitStateChanged || obstacleChanged || edgeDistanceExceeded)
                {
                    EdgeInfo edge = FindEdge(oldViewCast, newViewCast, viewRadius);

                    if (edge.HasPointA)
                        viewPoints.Add(edge.PointA);

                    if (edge.HasPointB)
                        viewPoints.Add(edge.PointB);
                }
            }

            // 충돌 지점의 좌표를 viewPoints 리스트에 추가한다.
            viewPoints.Add(newViewCast.point);
            oldViewCast = newViewCast;
        }

        Transform meshTransform = viewMeshFilter.transform;

        // 부채꼴 중심점을 MeshFilter의 로컬 좌표로 변환한다.
        Vector3 localOrigin = meshTransform.InverseTransformPoint(ViewOriginPosition);

        // 부채꼴 모양의 Mesh의 중심점
        meshVertices.Add(localOrigin);

        for (int i = 0; i < viewPoints.Count; i++)
        {
            //ViewCast가 구한 월드 좌표를 FOV 오브젝트의 local 좌표로 변환하여 meshVertices 리스트에 추가한다.
            Vector3 localPoint = meshTransform.InverseTransformPoint(viewPoints[i]);

            meshVertices.Add(localPoint);

            if (i >= viewPoints.Count - 1)
                continue;

            // 중심점은 항상 0번 인덱스이다.
            meshTriangles.Add(0);

            // 현재 시야의 경계점
            meshTriangles.Add(i + 1);

            // 다음 시야의 경계점
            meshTriangles.Add(i + 2);
        }

        viewMesh.Clear();

        viewMesh.SetVertices(meshVertices);

        // 마지막 true는 삼각형 변경 후 Bounds도 다시 계산하라는 뜻이다.
        viewMesh.SetTriangles(meshTriangles, 0, true);
    }

    private void DrawAwarenessMesh()
    {
        viewPoints.Clear();
        meshVertices.Clear();
        meshTriangles.Clear();

        // 360도를 awarenessSegments로 나누어 한 구역이 차지하는 각도를 구한다.
        float stepAngleSize = 360f / awarenessSegments;

        // 이전 Raycast 충돌 정보를 저장할 변수
        ViewCastInfo oldViewCast = default;

        // 0도부터 360도까지 awarenessSegments만큼 나누어 Ray를 쏘아 인지 범위의 경계점을 구한다.
        // 이때, 마지막 경계점이 처음 경계점과 이어져야 하므로, <= awarenessSegments로 360도에 해당하는 마지막 경계점까지 포함할 수 있도록 한다.
        for (int i = 0; i <= awarenessSegments; i++)
        {
            // 월드 기준 현재 경계점이 향할 각도
            float angle = stepAngleSize * i;

            // 현재 방향으로 awarenessRadius만큼 Ray를 쏘아 충돌 정보를 가져온다.
            ViewCastInfo newViewCast = ViewCast(angle, awarenessRadius);

            // 첫 번째 검사에는 비교할 이전 Raycast 충돌 정보가 없으므로, 2번째 검사부터 이전 Raycast 충돌 정보와 비교한다.
            if (i > 0)
            {
                bool hitStateChanged = oldViewCast.hit != newViewCast.hit;
                bool obstacleChanged = oldViewCast.hit && newViewCast.hit && oldViewCast.collider != newViewCast.collider;
                bool edgeDistanceExceeded = oldViewCast.hit && newViewCast.hit && Mathf.Abs(oldViewCast.dst - newViewCast.dst) > edgeDstThreshold;

                // 이전 Raycast 충돌 정보와 현재 Raycast 충돌 정보를 비교하여, hit 상태가 바뀌었거나, 장애물이 바뀌었거나, 거리가 threshold를 초과했다면 경계점을 찾아야 한다.
                if (hitStateChanged || obstacleChanged || edgeDistanceExceeded)
                {
                    EdgeInfo edge = FindEdge(oldViewCast, newViewCast, awarenessRadius);
                    if (edge.HasPointA)
                        viewPoints.Add(edge.PointA);
                    if (edge.HasPointB)
                        viewPoints.Add(edge.PointB);
                }
            }

            // 현재 Raycast로 찾은 원형 시야의 경계점을 viewPoints 리스트에 추가한다.
            viewPoints.Add(newViewCast.point);

            // raycast 충돌 정보를 이전 Raycast 충돌 정보로 저장한다.
            oldViewCast = newViewCast;
        }

        // 원형 메시가 연결된 MeshFilter의 Transform을 가져온다.
        Transform meshTransform = awarenessMeshFilter.transform;

        // ViewCast가 구한 좌표는 월드 좌표이므로, 원형 메시를 그리기 위해서는 월드 좌표를 원형 메시의 로컬 좌표로 변환해야 한다.
        // 월드에 있는 시야 중심점을 원형 메시의 로컬 좌표로 변환한다.
        Vector3 localOrigin = meshTransform.InverseTransformPoint(ViewOriginPosition);

        // 원형 메시의 중심점은 모든 삼각형의 첫 번째 정점이므로, meshVertices 리스트에 추가한다.
        meshVertices.Add(localOrigin);

        // 앞에서 구한 원형 경계점을 순회하여 원형 메시를 구성하기 위한 정점과 삼각형을 생성한다.
        for (int i = 0; i < viewPoints.Count; i++)
        {
            // ViewCast가 구한 월드 좌표를 원형 메시의 로컬 좌표로 변환하여 meshVertices 리스트에 추가한다.
            Vector3 localPoint = meshTransform.InverseTransformPoint(viewPoints[i]);
            meshVertices.Add(localPoint);
            // 마지막 경계점은 다음 경계점이 없으므로, 마지막 경계점까지 삼각형을 구성하지 않는다.
            if (i >= viewPoints.Count - 1)
                continue;
            // 삼각형을 구성하기 위해 정점 인덱스를 meshTriangles 리스트에 추가한다.
            // 중심점은 항상 0번 인덱스이다.
            meshTriangles.Add(0);
            // 현재 경계점
            meshTriangles.Add(i + 1);
            // 다음 경계점
            meshTriangles.Add(i + 2);
        }

        // 원형 메시를 초기화한다.
        awarenessMesh.Clear();

        // 이번 프레임에 구한 원형 메시의 정점 정보를 원형 메시에 설정한다.
        awarenessMesh.SetVertices(meshVertices);

        // 정점들을 어떤 순서로 연결할지 전달한다.
        awarenessMesh.SetTriangles(meshTriangles, 0, true);
    }

    private ViewCastInfo ViewCast(float globalAngle, float maxDistance)
    {
        Vector3 origin = ViewOriginPosition;
        Vector3 dir = DirFromAngle(globalAngle, true);
        RaycastHit hit;

        // 만약 Raycast가 장애물에 맞았다면, hit.point를 반환하고, 아니라면 maxDistance만큼 떨어진 지점을 반환한다.
        if (Physics.Raycast(origin, dir, out hit, maxDistance, visionBlockMask, QueryTriggerInteraction.Ignore))
        {
            return new ViewCastInfo(true, hit.point, hit.distance, globalAngle, hit.collider);
        }
        else
        {
            Vector3 endPoint = origin + dir * maxDistance;
            return new ViewCastInfo(false, endPoint, maxDistance, globalAngle, null);
        }
    }

    private int CollectTargetsInViewRadius()
    {
        while (true)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(ViewOriginPosition, viewRadius, targetBuffer, targetMask, QueryTriggerInteraction.Collide);

            // 만약 결과 수가 배열 크기보다 작다면 모든 결과가 저장된 것으로 간주하고 hitCount를 반환한다.

            if (hitCount < targetBuffer.Length)
            {
                hasWarnedTargetBufferFull = false;
                return hitCount;
            }

            // 최대 크기에 도달했다면 더 이상 배열을 확장하지 않는다.
            if (targetBuffer.Length >= maxTargetBufferSize)
            {
                if (!hasWarnedTargetBufferFull)
                {
                    Debug.LogWarning(
                        $"FOV 탐지 결과가 최대 버퍼 크기 " +
                        $"{maxTargetBufferSize}개에 도달했습니다. " +
                        $"일부 대상이 누락될 수 있습니다.",
                        this
                    );
                    hasWarnedTargetBufferFull = true;
                }
                return hitCount;
            }

            int expandedSize = Mathf.Min(targetBuffer.Length * 2, maxTargetBufferSize);

            System.Array.Resize(ref targetBuffer, expandedSize);
        }
    }

    // 두 충돌 정보(장애물에 부딪힌 각도와 해당 장애물에 부딪히지 않은 각도)를 기반으로 장애물이 끝나는 경계 각도를 찾는 메서드
    private EdgeInfo FindEdge(ViewCastInfo minViewCast, ViewCastInfo maxViewCast, float maxDistance)
    {
        float minAngle = minViewCast.angle;
        float maxAngle = maxViewCast.angle;

        Vector3 minPoint = Vector3.zero;
        Vector3 maxPoint = Vector3.zero;

        bool hasMinPoint = false;
        bool hasMaxPoint = false;

        // 두 각도의 중앙값을 기준으로 hit 정보를 확인한다.
        // 중앙값을 각도로 하여 충돌 정보를 얻은 후
        // 만약 장애물이 걸리는 게 있다면 minAngle을 조정하고
        // 걸리는 게 없다면 MaxAngle을 조정한다.
        // 이 과정을 반복함으로써 장애물의 경계와 충돌하는 각도와 지점을 찾아낸다.
        for (int i = 0; i < edgeResolveIterations; i++)
        {
            float angle = (minAngle + maxAngle) / 2;
            ViewCastInfo newViewCast = ViewCast(angle, maxDistance);

            bool sameHitState = newViewCast.hit == minViewCast.hit;

            bool sameObstacle = !newViewCast.hit || newViewCast.collider == minViewCast.collider;

            bool edgeDistanceExceeded = Mathf.Abs(minViewCast.dst - newViewCast.dst) > edgeDstThreshold;

            if (sameHitState && sameObstacle && !edgeDistanceExceeded)
            {
                minAngle = angle;
                minPoint = newViewCast.point;
                hasMinPoint = true;
            }
            else
            {
                maxAngle = angle;
                maxPoint = newViewCast.point;
                hasMaxPoint = true;
            }
        }

        return new EdgeInfo(hasMinPoint, minPoint, hasMaxPoint,  maxPoint);
    }

    private void ClampSettings()
    {
        viewRadius =
            Mathf.Max(0.1f, viewRadius);

        viewAngle =
            Mathf.Clamp(viewAngle, 0.1f, 360f);

        meshResolution =
            Mathf.Max(0.01f, meshResolution);

        targetRefreshInterval =
            Mathf.Max(0.02f, targetRefreshInterval);

        initialTargetBufferSize =
            Mathf.Max(1, initialTargetBufferSize);

        maxTargetBufferSize =
            Mathf.Max(
                initialTargetBufferSize,
                maxTargetBufferSize
            );

        edgeResolveIterations =
            Mathf.Max(0, edgeResolveIterations);

        edgeDstThreshold =
            Mathf.Max(0f, edgeDstThreshold);

        awarenessRadius = Mathf.Clamp(awarenessRadius, 0f, viewRadius);

        // 도형을 만들기 위해서는 최소 3개의 점이 필요하다.
        awarenessSegments =
            Mathf.Max(3, awarenessSegments);
    }

    public struct ViewCastInfo
    {
        public bool hit;
        public Vector3 point;
        public float dst;
        public float angle;
        public Collider collider;

        public ViewCastInfo(bool _hit, Vector3 _point, float _dst, float _angle, Collider _collider)
        {
            hit = _hit;
            point = _point;
            dst = _dst;
            angle = _angle;
            collider = _collider;
        }
    }

    public readonly struct EdgeInfo
    {
        public bool HasPointA { get; }
        public Vector3 PointA { get; }

        public bool HasPointB { get; }
        public Vector3 PointB { get; }

        public EdgeInfo(
            bool hasPointA,
            Vector3 pointA,
            bool hasPointB,
            Vector3 pointB)
        {
            HasPointA = hasPointA;
            PointA = pointA;

            HasPointB = hasPointB;
            PointB = pointB;
        }
    }
}
