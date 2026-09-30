using UnityEngine;

// AI가 들을 수 있는 게임 규칙상의 소리 정보를 나타낸다.
public readonly struct SoundStimulus
{
    // 소리가 발생한 월드 위치
    public Vector3 Position { get; }

    // 이 소리가 전달되는 최대 거리
    public float Radius { get; }

    // 소리를 발생시킨 오브젝트
    public GameObject Source { get; }

    public SoundStimulus(Vector3 position, float radius, GameObject source)
    {
        Position = position;
        Radius = Mathf.Max(0f, radius);
        Source = source;
    }
}
