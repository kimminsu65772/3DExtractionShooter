using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class WeaponCatalog
    {
        public void Publish(IReadOnlyList<RuntimeWeapon> candidates, DataReport report)
        {
            // TODO S04: 중복 ID 검사→임시 사전 완성→오류 없을 때 한 번만 교체.
            // TODO: 외부에서 WeaponDefinition을 수정하지 않는 API/소유권 적용.
            throw new NotImplementedException();
        }
        public bool TryGet(int weaponId, out RuntimeWeapon weapon)
        {
            // TODO S04: Dictionary<int,RuntimeWeapon>. 실패를 기본 무기로 숨기지 않는다.
            throw new NotImplementedException();
        }
        public void Clear()
        {
            // TODO S09: 전투 사용 종료 후 카탈로그 참조 제거.
            throw new NotImplementedException();
        }
    }
}
