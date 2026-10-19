using UnityEngine;

// 원작의 끊긴 수명 스크립트를 대체한다. 풀에서 관리하는 효과에는 붙이지 않는다.
[DisallowMultipleComponent]
public sealed class ParticleEffectLifetime : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float lifetime = 4.2f;

    private void OnEnable()
    {
        // 원본 효과의 수명값을 사용해 파티클과 자식 조명을 함께 정리한다.
        Invoke(nameof(DestroyEffect), Mathf.Max(0.1f, lifetime));
    }

    private void OnDisable()
    {
        // 비활성화 후 다시 사용될 때 이전 삭제 예약이 남지 않게 한다.
        CancelInvoke(nameof(DestroyEffect));
    }

    private void DestroyEffect()
    {
        Destroy(gameObject);
    }
}
