using UnityEngine;

// 이동 담당자는 그대로 두고 플레이어와 적이 같은 원본 속도 곡선을 사용하게 한다.
public static class DodgeMotion
{
    public static AnimationCurve CreateOriginalSpeedCurve()
    {
        // 원본 Character.prefab의 시간·배율·접선을 보존한다. 시간 축은 회피 진행률이다.
        return new AnimationCurve(
            new Keyframe(-0.0012207031f, 0.6135613f, 4.2667236f, 4.2667236f),
            new Keyframe(0.19268288f, 1f, 0f, 0f),
            new Keyframe(0.7638108f, 0.7851256f, -0.91723293f, -0.91723293f),
            new Keyframe(1f, 0.5459905f, -0.8117536f, -0.8117536f));
    }

    public static float EvaluateSpeed(float speed, AnimationCurve curve, float progress)
    {
        float multiplier = curve != null && curve.length > 0 ? curve.Evaluate(Mathf.Clamp01(progress)) : 1f;
        return Mathf.Max(0f, speed) * Mathf.Max(0f, multiplier);
    }

    public static float EstimateDistance(float speed, float duration, AnimationCurve curve)
    {
        // 방향 탐색용 거리만 예측한다. 실제 이동을 이 거리에 맞춰 정규화하지 않는다.
        const int samples = 64;
        float totalSpeed = 0f;
        for (int i = 0; i < samples; i++)
            totalSpeed += EvaluateSpeed(speed, curve, (i + 0.5f) / samples);
        return totalSpeed * Mathf.Max(0f, duration) / samples;
    }
}
