using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class RuntimeAssetRegistry
    {
        public int Register(string address, AssetKind kind)
        {
            // TODO S04: (정확한 주소,종류)→연속 정수. 0은 선택 리소스 없음.
            // TODO: string.GetHashCode를 유일/영구 ID로 쓰지 않는다. 중복은 기존 ID 반환.
            throw new NotImplementedException();
        }
        public bool TryResolve(int id, out string address, out AssetKind kind)
        {
            // TODO S04: 세션 내 역방향 조회. Freeze 후 신규 등록 금지.
            throw new NotImplementedException();
        }
        public void Freeze()
        {
            // TODO S04: 세션 동안 ID 매핑을 고정.
            throw new NotImplementedException();
        }
    }
}
