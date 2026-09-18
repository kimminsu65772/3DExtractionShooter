using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class PoolPlanBuilder
    {
        public IReadOnlyList<PoolSpec> Build(IReadOnlyList<RuntimeWeapon> encounterWeapons,
            RuntimeAssetRegistry registry, DataReport report)
        {
            // TODO S05: 이번 전투의 플레이어/적 무기를 기준으로 필요한 프리팹을 집계.
            // TODO: 공유 projectileAddress는 하나의 PoolSpec. 동시 사용자 수를 고려한 수요 합산.
            // TODO: VFX 정책과 공통 AudioEmitter 풀 정책을 받아 합친다(AudioClip은 풀 대상 아님).
            // TODO: prewarm<=maxRetained, prewarm<=maxActive, 총 메모리 예산, 종류/정책 충돌 검증.
            // TODO: 필요한 정책 매개변수/설정 타입을 추가한다. 없으면 임의 기본값 대신 진단.
            throw new NotImplementedException();
        }
    }
}
