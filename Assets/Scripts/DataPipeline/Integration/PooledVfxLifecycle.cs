using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class PooledVfxLifecycle : MonoBehaviour, IPoolLifecycle
    {
        public void OnRent(PoolLease lease, IPoolReturner owner)
        {
            // TODO S08: lease 저장; 재생 전 Particle Stop/Clear, 새 위치/부착 대상 설정.
            throw new NotImplementedException();
        }
        public void Play()
        {
            // TODO S08: 활성화 후 재생. 모든 하위 ParticleSystem 종료 확인 후 현재 generation으로 반환.
            // TODO: looping 효과에는 명시 Stop/timeout 정책, 늦은 callback에는 세대 검증.
            throw new NotImplementedException();
        }
        public void OnReturn()
        {
            // TODO S08: 코루틴/콜백 취소, 하위 파티클 정지/제거, 부모 복구.
            throw new NotImplementedException();
        }
    }
}
