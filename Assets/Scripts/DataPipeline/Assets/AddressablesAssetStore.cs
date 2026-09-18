using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class AddressablesAssetStore
    {
        public Task<AssetLease<T>> AcquireAsync<T>(string address, CancellationToken token)
            where T : UnityEngine.Object
        {
            // TODO S06: Addressables 패키지를 설치하고 LoadAssetAsync<T> 사용.
            // TODO: (address,typeof(T))별 pending 작업 병합, 핸들 보관, 독립 lease 반환.
            // TODO: Completed/Task 성공·실패 처리. 취소된 호출자만 이탈하고 공유 로드는 함부로 중단하지 않는다.
            // TODO: 관심자 0인 늦은 성공/실패도 정확히 한 번 Release. 메인 스레드에서 Unity API 호출.
            throw new NotImplementedException();
        }
        public Task ShutdownAsync(CancellationToken token)
        {
            // TODO S09: 신규 요청 거부, pending 정리, 모든 소유자 반환 확인 후 핸들 해제.
            // TODO: 사용자 취소가 cleanup 자체를 중단해 누수시키지 않도록 정책 분리.
            throw new NotImplementedException();
        }
    }
}
