using UnityEngine;
using UnityEngine.UI;

public class LoadingCirclePulse : MonoBehaviour
{
    [SerializeField] private Image circleImage;
    [SerializeField] private float duration = 1f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1.5f);
    [SerializeField] private AnimationCurve alphaCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.1f, 1f),
        new Keyframe(0.3f, 1f),
        new Keyframe(0.8f, 0f),
        new Keyframe(1f, 0f));

    private float elapsedTime = 0f;

    private void OnEnable()
    {
        elapsedTime = 0f;
        transform.localScale = Vector3.one * scaleCurve.Evaluate(0f);
        ApplyAlpha(0f);
    }

    private void Update()
    {
        elapsedTime += Time.unscaledDeltaTime;

        // 경과 시간을 duration으로 나눈 나머지를 사용하여 반복적인 애니메이션을 재생하도록 한다.
        elapsedTime %= duration;

        // 변화 비율을 계산한다.
        float t = elapsedTime / duration;

        // AnimationCurve를 사용하여 스케일을 계산하고 적용한다.
        transform.localScale = Vector3.one * scaleCurve.Evaluate(t);
        ApplyAlpha(t);

    }

    private void ApplyAlpha(float t)
    {
        Color color = circleImage.color;
        color.a = alphaCurve.Evaluate(t);
        circleImage.color = color;
    }
}
