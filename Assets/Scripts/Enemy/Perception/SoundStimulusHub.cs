using UnityEngine;
using System;

public static class SoundStimulusHub
{
    public static event Action<SoundStimulus> OnStimulusEmitted;

    public static void Emit(Vector3 position, float radius, GameObject source)
    {
        if (radius <= 0f)
        {
            Debug.LogWarning("SoundStimulusHub: 비정상적인 소리 전달 거리를 넘겨받았습니다.");
            return;
        }

        SoundStimulus stimulus = new SoundStimulus(position, radius, source);

        OnStimulusEmitted?.Invoke(stimulus);
    }

    // Enter Play Mode 설정이나 Domain Reload 여부와 관계없이
    // 이전 실행에서 남은 정적 이벤트 구독 정보를 제거한다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        OnStimulusEmitted = null;
    }
}
