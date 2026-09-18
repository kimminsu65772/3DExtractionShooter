using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    // TODO S08: 투사체 프리팹에 연결하고 Projectile이 이 컴포넌트로 반환을 요청하도록 연결한다.
    public sealed class PooledProjectileLifecycle : MonoBehaviour, IPoolLifecycle
    {
        public void OnRent(PoolLease lease, IPoolReturner owner)
        {
            // TODO S08: 세대와 owner 저장. 컴포넌트/Trail 캐시는 최초 생성 때만 확보.
            throw new NotImplementedException();
        }
        public void OnReturn()
        {
            // TODO S08: 공격자/무기/거리/방향, 이벤트, TrailRenderer.Clear 등 이전 사용 상태 제거.
            throw new NotImplementedException();
        }
        public void ReturnToOwner()
        {
            // TODO S08: 현재 lease 반환. OnDisable에서 재귀 반환하지 않는다.
            throw new NotImplementedException();
        }
    }
}
