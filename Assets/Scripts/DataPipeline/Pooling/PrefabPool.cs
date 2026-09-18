using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class PrefabPool : IPoolReturner
    {
        public Task InitializeAsync(PoolSpec spec, AddressablesAssetStore assets, Transform inactiveRoot,
            CancellationToken token)
        {
            // TODO S07: 프리팹 lease를 한 번 취득. 비활성 부모 아래 생성하여 OnEnable 조기 실행 방지.
            // TODO: ObjectPool<GameObject> 생성 콜백은 동기 Instantiate. Addressables를 콜백 안에서 기다리지 않는다.
            // TODO: prewarm 개를 동시에 빌려 별도 목록에 보관한 후 모두 반환(같은 1개 재사용 주의).
            // TODO: 프리웜 프레임 예산, 컴포넌트 캐싱, 실패 시 전체 파괴 후 lease Dispose.
            throw new NotImplementedException();
        }
        public bool TryRent(out PoolLease lease)
        {
            // TODO S07: inactive 상태로 반환. hard MaxActive와 overflow 정책 검사.
            // TODO: instance→현재 세대/대여 상태 추적, OnRent 호출. 준비 실패 시 롤백.
            throw new NotImplementedException();
        }
        public void Activate(PoolLease lease)
        {
            // TODO S07: owner/instance/generation 검증 후 SetActive(true).
            // TODO: payload/위치/이벤트 초기화는 호출자가 이 메서드 전에 완료.
            throw new NotImplementedException();
        }
        public bool TryReturn(PoolLease lease)
        {
            // TODO S07: 다른 풀, 이중 반환, 오래된 generation 거부.
            // TODO: 재진입 방지 상태 먼저 전환→OnReturn→비활성→대기 풀. 예외 시 격리 파괴.
            throw new NotImplementedException();
        }
        public Task ShutdownAsync()
        {
            // TODO S09: 대여 차단→활성/비활성 전부 파괴→Destroy 처리 완료 프레임→prefab lease Dispose.
            throw new NotImplementedException();
        }
    }
}
