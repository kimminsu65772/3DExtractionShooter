using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class KeyedPoolService : IPoolReturner
    {
        public Task InitializeAsync(IReadOnlyList<PoolSpec> plan, AddressablesAssetStore assets,
            Transform inactiveRoot, CancellationToken token)
        {
            // TODO S07: Dictionary<int,PrefabPool>. 전체 성공 전 외부 Ready 노출 금지.
            // TODO: 하나 실패하면 이미 만든 풀도 역순 정리.
            throw new NotImplementedException();
        }
        public bool TryRent(int poolId, out PoolLease lease)
        {
            // TODO S07: 정수 ID 조회. 미등록/미준비/고갈은 실패 원인 기록.
            throw new NotImplementedException();
        }
        public void Activate(PoolLease lease)
        {
            // TODO S07: 해당 owner pool로 위임.
            throw new NotImplementedException();
        }
        public bool TryReturn(PoolLease lease)
        {
            // TODO S07: 해당 owner pool로 위임. 도메인 밖 풀에 반환 금지.
            throw new NotImplementedException();
        }
        public Task ShutdownAsync()
        {
            // TODO S09: 모든 풀의 인스턴스 파괴/lease 반환 완료까지 기다린다.
            throw new NotImplementedException();
        }
    }
}
