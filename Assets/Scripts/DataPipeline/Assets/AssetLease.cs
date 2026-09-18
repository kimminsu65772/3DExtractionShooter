using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class AssetLease<T> : IDisposable where T : UnityEngine.Object
    {
        public T Asset => throw new NotImplementedException("TODO S06: 유효한 동안만 접근");
        public void Dispose()
        {
            // TODO S06: store의 소유 수를 정확히 한 번 감소. Unity 메인 스레드 계약.
            // TODO: Dispose 후 Asset 접근 거부. 각 Acquire마다 독립 lease.
            throw new NotImplementedException();
        }
    }
}
