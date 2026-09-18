using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    // 씬에 자동 부착되지 않는 orchestration 뼈대. 구현 후 composition root에서 생성/호출한다.
    public sealed class CombatDataBootstrap
    {
        public PipelineState State => throw new NotImplementedException("TODO S09");
        public Task InitializeAsync(string csvAddress, CancellationToken token)
        {
            // TODO S09: 단일 초기화 보장; 의존성은 생성자/명시 Init으로 주입. 전역 Find 사용 금지.
            // TODO: CSV TextAsset Addressables lease→읽기→생성 binding→검증→ID→전투 pool plan.
            // TODO: AudioClip leases도 별도로 확보. 풀/클립 준비 후에만 카탈로그 공개 및 Ready.
            // TODO: 데이터 문자열을 소유해 파싱 끝나면 TextAsset lease 해제 가능.
            // TODO: 실패/취소 시 역순 rollback. 예외를 UI/호출자에 전달하고 재시도 가능 상태로.
            throw new NotImplementedException();
        }
        public Task ShutdownAsync()
        {
            // TODO S09: 발사 차단→활성 효과/탄환 정리→풀 파괴→clip leases 반환→store 종료→catalog clear.
            // TODO: 초기화 도중 종료/연속 종료도 안전. 호출자는 반드시 await.
            throw new NotImplementedException();
        }
    }
}
