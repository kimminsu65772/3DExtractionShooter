using UnityEngine;

public class SquadPerceptionLink : MonoBehaviour
{
    [SerializeField] private EnemyPerception perception;
    [SerializeField] private SquadBlackboard blackboard;

    private bool hasSharedObservation;
    private SquadObservation latestSharedObservation;

    public bool HasSharedObservation => hasSharedObservation;
    public SquadObservation LatestSharedObservation => latestSharedObservation;

    private void Awake()
    {
        if (perception == null)
            Debug.LogError("SquadPerceptionLink: EnemyPerception가 설정되지 않았습니다.");
        if (blackboard == null)
            Debug.LogError("SquadPerceptionLink: SquadBlackboard가 설정되지 않았습니다.");
    }
    private void OnEnable()
    {
        if (perception == null || blackboard == null)
            return;
        perception.OnStimulusDetected += ReportObservation;
        perception.OnSoundHeard += ReportSoundDetection;
        blackboard.OnObservationUpdated += HandleObservationUpdated;
        blackboard.OnObservationExpired += HandleObservationExpired;

        if (blackboard.HasValidObservation)
        {
            hasSharedObservation = true;
            latestSharedObservation = blackboard.LatestObservation;
        }
        else
        {
            hasSharedObservation = false;
            latestSharedObservation = default;
        }
    }

    private void OnDisable()
    {
        if (perception == null || blackboard == null)
            return;
        perception.OnStimulusDetected -= ReportObservation;
        perception.OnSoundHeard -= ReportSoundDetection;
        blackboard.OnObservationUpdated -= HandleObservationUpdated;
        blackboard.OnObservationExpired -= HandleObservationExpired;

        HandleObservationExpired();
    }

    private void ReportObservation(Vector3 reportPosition, float reportTime)
    {
        SquadObservation observation = new SquadObservation(reportPosition, reportTime);
        blackboard.ReportObservation(observation);
    }

    private void ReportSoundDetection(SoundStimulus sound)
    {
        // 아군 사격 소리를 적의 위치로 보고하면 공유 기억과 전술이 서로 다른 위치 사이를 왕복한다.
        // 일반 적의 청각은 그대로 두고 분대 연결 계층에서 같은 분대의 소리만 제외한다.
        if (sound.Source != null)
        {
            SquadPerceptionLink sourceLink = sound.Source.GetComponentInParent<SquadPerceptionLink>();
            if (sourceLink != null && sourceLink.blackboard == blackboard)
                return;
        }
        SquadObservation observation = new SquadObservation(sound.Position, Time.time);
        blackboard.ReportObservation(observation);
    }
    public bool RequestDiscardObservation(SquadObservation investigatedObservation)
    {
        return blackboard.TryDiscardObservation(investigatedObservation);
    }

    private void HandleObservationUpdated(SquadObservation observation)
    {
        hasSharedObservation = true;
        latestSharedObservation = observation;
    }

    private void HandleObservationExpired()
    {
        hasSharedObservation = false;
        latestSharedObservation = default;
    }
}
