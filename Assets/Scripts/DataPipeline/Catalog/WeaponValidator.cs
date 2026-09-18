using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class WeaponValidator : IRowValidator<WeaponDefinition>
    {
        public void Validate(WeaponDefinition row, int record, DataReport report)
        {
            // TODO S03: ID>0, 유한 수치, fireRate/speed/radius>0, 0<optimal<=effective<=minDamageRange.
            // TODO: 배율/반동/프리웜 음수 거부. 조준 반동 0 처리 정책을 기존 코드와 합의한다.
            // TODO: projectileAddress 필수, VFX/사운드는 빈 값=없음 허용(선택한 정책).
            // TODO: 타입별 무기 규칙. 근접 무기를 원거리 필수 값으로 검증하지 않는다.
            throw new NotImplementedException();
        }
    }
}
