using UnityEngine;
using System;

public class EnemyPerception : MonoBehaviour
{
    [Header("감지 대상")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform targetViewPoint; // 조준용 위치
    [SerializeField] private AimPointProvider targetAimPointProvider;
    [SerializeField] private LayerMask audibleLayer;

    [Header("시야의 기준점")]
    [SerializeField] private Transform viewOrigin;

    [Header("시야 범위")]
    [SerializeField, Min(0.1f)] private float confirmedDistance = 6f;
    [SerializeField, Min(0.1f)] private float suspiciousDistance = 12f;
    [SerializeField, Range(0f, 360f)] private float viewAngle = 100f;
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.15f;

    [Header("청각 범위")]
    [SerializeField, Min(0f)] private float hearingRangeMultiplier = 1f;

    [Header("시야 차폐 대상")]
    [SerializeField] private LayerMask viewBlockMask;

    private float refreshTimer;

    public Transform Target => playerRoot;
    public AimPointProvider TargetAimPointProvider => targetAimPointProvider;
    public EnemyPerceptionLevel CurrentLevel { get; private set; }
    public Vector3 LastKnownPosition { get; private set; }
    public bool HasLastKnownPosition { get; private set; }
    public bool IsConfirmed => CurrentLevel == EnemyPerceptionLevel.Confirmed;
    public float ConfirmedDistance => confirmedDistance;
    public float SuspiciousDistance => suspiciousDistance;
    public float ViewAngle => viewAngle;
    public Vector3 ViewOriginPosition => viewOrigin != null ? viewOrigin.position : transform.position;
    public Vector3 ViewForward => viewOrigin != null ? viewOrigin.forward : transform.forward;

    public event Action<SoundStimulus> OnSoundHeard;
    public event Action<Vector3, float> OnStimulusDetected;

    // 비활성 상태에서 주입해 Brain.Awake가 조준 대상을 바로 확보할 수 있게 한다.
    public void SetTarget(Transform target, Transform viewPoint, AimPointProvider aimPointProvider)
    {
        playerRoot = target;
        targetViewPoint = viewPoint;
        targetAimPointProvider = aimPointProvider;
        if (target == null)
        {
            // 감지 주기를 기다리지 않고 파괴된 대상을 향한 공격 조건을 해제한다.
            CurrentLevel = EnemyPerceptionLevel.None;
            HasLastKnownPosition = false;
        }
    }

    private void Awake()
    {
        if (playerRoot != null && targetAimPointProvider == null)
            targetAimPointProvider = playerRoot.GetComponent<AimPointProvider>();

        if (targetAimPointProvider == null)
            Debug.LogError("EnemyPerception: 플레이어의 AimProvider가 설정되지 않았습니다.");
    }

    private void OnEnable()
    {
        SoundStimulusHub.OnStimulusEmitted += HandleSoundStimulus;
    }

    private void OnDisable()
    {
        SoundStimulusHub.OnStimulusEmitted -= HandleSoundStimulus;
    }

    private void OnValidate()
    {
        confirmedDistance = Mathf.Max(0.1f, confirmedDistance);
        suspiciousDistance = Mathf.Max(confirmedDistance, suspiciousDistance);
        refreshInterval = Mathf.Max(0.02f, refreshInterval);
    }

    public void Tick(float deltaTime)
    {
        // 아직 다음 검사 시간이 되지 않았다면 기존 검사 결과를 유지하도록 한다.
        refreshTimer -= deltaTime;
        if (refreshTimer > 0f)
            return;

        refreshTimer = refreshInterval;

        RefreshVision();
    }

    public void RefreshVision()
    {
        CurrentLevel = EnemyPerceptionLevel.None;

        if (playerRoot == null)
            return;

        // 시야의 기준점이 정해져 있다면 그 위치를 기준으로 시야를 검사하고, 그렇지 않다면 적의 위치를 기준으로 시야를 검사한다.
        Vector3 origin = viewOrigin != null ? viewOrigin.position : transform.position;
        
        // 조준용 기준점을 잡는다.
        Vector3 targetPoint = targetViewPoint != null ? targetViewPoint.position : playerRoot.position;

        // 높이 차이는 시야각 계산에서 제거한다.
        Vector3 horizontalOffset = Vector3.ProjectOnPlane(targetPoint - origin, Vector3.up);
        float horizontalDistanceSqr = horizontalOffset.sqrMagnitude;
        float suspiciousDistanceSqr = suspiciousDistance * suspiciousDistance;

        if (horizontalDistanceSqr > suspiciousDistanceSqr)
            return;

        // 만약 플레이어와의 수평 위치가 거의 같다면 방향 비교를 생략한다.
        if (horizontalDistanceSqr >= 0.0001f)
        {
            Vector3 originForward = viewOrigin != null ? viewOrigin.forward : transform.forward;
            Vector3 horiziontalForward = Vector3.ProjectOnPlane(originForward, Vector3.up).normalized;

            Vector3 horizontalDirection = horizontalOffset.normalized;

            // 벡터 내적을 통해 두 벡터가 얼마나 같은 방향을 바라보는지 확인한다.
            float directionDot = Vector3.Dot(horiziontalForward, horizontalDirection);

            // 시야각의 절반을 라디안으로 변환하여 코사인 값을 구한다.
            // 이를 통해 시야각을 이루는 벡터의 최소 내적 값을 계산한다.
            float minDot = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);

            // 만약 두 벡터의 내적 값이 최소 내적 값보다 작다면, 시야각 밖에 있는 것으로 판단하여 감지하지 않는다.
            if (directionDot < minDot)
                return;
        }

        // 시야로 인지할 거리를 계산한다.
        Vector3 sightOffset = targetPoint - origin;
        float sightDistance = sightOffset.magnitude;

        if (sightDistance <= Mathf.Epsilon)
            return;

        // 시야의 방향을 계산한다.
        Vector3 sightDirection = sightOffset / sightDistance;

        // 시야 차폐 대상이 있는지 확인한다.
        if (Physics.Raycast(origin, sightDirection, out RaycastHit hitInfo, sightDistance, viewBlockMask, QueryTriggerInteraction.Ignore))
        {
            // 시야 차폐 대상이 있다면 감지하지 않는다.
            return;
        }

        // 플레이어를 직접 확인했으므로 이동에 사용할 발밑 위치를 기억한다.
        ReportStimulusPosition(playerRoot.position);

        float confirmedDistanceSqr = confirmedDistance * confirmedDistance;

        // 가까운 구간에서는 확정 발견, 먼 구간에서는 의심으로 구분한다.
        CurrentLevel = horizontalDistanceSqr <= confirmedDistanceSqr
            ? EnemyPerceptionLevel.Confirmed
            : EnemyPerceptionLevel.Suspicious;
    }

    // 시야, 피격, 소리 등을 통해 알아낸 위치를 마지막 단서로 기억한다.
    public void ReportStimulusPosition(Vector3 position)
    {
        LastKnownPosition = position;
        HasLastKnownPosition = true;
        OnStimulusDetected?.Invoke(LastKnownPosition, Time.time);
    }


    // 마지막 단서에 대한 조사가 완료되면 기존 단서를 지워서 더 이상 조사하지 않도록 한다.
    public void ClearLastKnownPosition()
    {
        HasLastKnownPosition = false;
    }

    private void HandleSoundStimulus(SoundStimulus stimulus)
    {
        // 사운드 소스가 비었거나, 자신이 발생시킨 사운드라면 무시한다.
        if (stimulus.Source == null || stimulus.Source == gameObject)
            return;

        // 사운드 소스가 자신이 속한 계층 구조에 있다면 무시한다.
        if (stimulus.Source != null && stimulus.Source.transform.IsChildOf(transform))
            return;

        // 사운드 소스가 자신이 감지할 수 있는 계층 구조에 속하지 않았다면 무시한다.
        if ((audibleLayer.value & (1 << stimulus.SourceLayer)) == 0)
            return;

        Vector3 listenerPosition = viewOrigin != null ? viewOrigin.position : transform.position;

        float hearingRadius = stimulus.Radius * hearingRangeMultiplier;

        if (hearingRadius <= 0f)
            return;

        float hearingRadiusSqr = hearingRadius * hearingRadius;

        float distanceSqr = (stimulus.Position - listenerPosition).sqrMagnitude;

        if (distanceSqr > hearingRadiusSqr)
            return;

        OnSoundHeard?.Invoke(stimulus);
    }
}
