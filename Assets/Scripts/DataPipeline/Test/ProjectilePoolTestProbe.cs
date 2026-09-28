using System;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    // 테스트 전용 투사체 인스턴스의 활성화/비활성화를 Runner에 알려준다.
    // 실제 풀링 로직에는 관여하지 않는다.
    public sealed class ProjectilePoolTestProbe : MonoBehaviour
    {
        public static event Action<int> Activated;
        public static event Action<int> Returned;

        private bool wasActivated;

        private void OnEnable()
        {
            wasActivated = true;
            Activated?.Invoke(gameObject.GetInstanceID());
        }

        private void OnDisable()
        {
            // 프리웜을 위해 처음 생성될 때 발생할 수 있는 비활성화는 반환으로 세지 않는다.
            if (!wasActivated)
                return;

            wasActivated = false;
            Returned?.Invoke(gameObject.GetInstanceID());
        }
    }
}
