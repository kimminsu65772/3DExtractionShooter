using UnityEngine;

// 하나의 관측에서 얻은 위치와 시각을 함께 전달한다.
public readonly struct SquadObservation
{
    // 관측한 대상의 월드 위치를 제공한다.
    public Vector3 Position { get; }

    // 보고 수신 시각이 아니라 실제 관측 시각을 제공한다.
    public float ObservedTime { get; }

    public SquadObservation(Vector3 position, float observedTime)
    {
        Position = position;
        ObservedTime = observedTime;
    }
}
