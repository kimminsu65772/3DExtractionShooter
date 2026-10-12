using UnityEngine;

// 같은 분대가 공유하는 최신 관측 기록을 보관한다.
public class SquadBlackboard : MonoBehaviour
{
    // 사용할 수 있는 관측 기록이 있는지 제공한다.
    public bool HasValidObservation { get; private set; }

    // 위치와 실제 관측 시각을 하나의 기록으로 제공한다.
    // 기록을 읽기 전에 HasValidObservation을 확인한다.
    public SquadObservation LatestObservation { get; private set; }

    // 대원이 관측한 기록을 전달받는다. 갱신 판단과 저장은 이후 구현한다.
    public void ReportObservation(SquadObservation observation)
    {
    }
}
