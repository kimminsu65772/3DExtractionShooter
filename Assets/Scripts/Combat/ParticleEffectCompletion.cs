using System.Collections;
using UnityEngine;

// 풀링하지 않는 단발 효과를 자식 파티클의 실제 재생 완료 뒤 제거한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class ParticleEffectCompletion : MonoBehaviour
{
    private IEnumerator Start()
    {
        ParticleSystem effect = GetComponent<ParticleSystem>();
        effect.Play(true);

        // 루트의 짧은 연기가 끝나도 더 오래 남는 자식 혈흔과 파편은 기다린다.
        while (effect.IsAlive(true))
            yield return null;

        Destroy(gameObject);
    }
}
