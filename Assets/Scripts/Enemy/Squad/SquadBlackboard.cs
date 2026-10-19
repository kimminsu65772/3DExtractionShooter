using System;
using UnityEngine;

// 같은 분대가 공유하는 최신 관측 기록을 보관한다.
public class SquadBlackboard : MonoBehaviour
{
    [Header("정보 유효 시간 설정")]
    [SerializeField] private float memoryDuration = 10f;

    // 사용할 수 있는 관측 기록이 있는지 제공한다.
    public bool HasValidObservation { get; private set; }

    // 위치와 실제 관측 시각을 하나의 기록으로 제공한다.
    public SquadObservation LatestObservation { get; private set; }

    public event Action<SquadObservation> OnObservationUpdated;
    public event Action OnObservationExpired;

    private void Update()
    {
        if (HasValidObservation && Time.time - LatestObservation.ObservedTime >= memoryDuration)
        {
            HasValidObservation = false;
            OnObservationExpired?.Invoke();
        }
    }

    public void ReportObservation(SquadObservation observation)
    {
        // 이미 최신 관측 기록이 있고, 새로 들어온 관측 기록이 이전 시각이면 무시한다.
        if (HasValidObservation && observation.ObservedTime <= LatestObservation.ObservedTime)
        {
            return;
        }

        // 새로 들어온 관측 기록이 유효 시간보다 오래되었으면 무시한다.
        if (Time.time - observation.ObservedTime >= memoryDuration)
        {
            return;
        }

        LatestObservation = observation;
        HasValidObservation = true;
        OnObservationUpdated?.Invoke(observation);
    }

    public bool TryDiscardObservation(SquadObservation observation)
    {
        if (HasValidObservation && LatestObservation.Equals(observation))
        {
            HasValidObservation = false;
            OnObservationExpired?.Invoke();
            return true;
        }
        return false;
    }
}
