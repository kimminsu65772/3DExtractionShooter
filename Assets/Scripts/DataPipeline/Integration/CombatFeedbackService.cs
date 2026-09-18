using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class CombatFeedbackService
    {
        public void PlayMuzzleAndSound(RuntimeWeapon weapon, Vector3 position, Quaternion rotation)
        {
            // TODO S08: 주입받은 pool/clip catalog로 MuzzleVfxId 대여 + FireSoundId 클립의 emitter 대여.
            // TODO: 0 ID는 효과 없음. 효과 고갈은 피해 판정/탄환 발사 성공을 되돌리지 않는다.
            throw new NotImplementedException();
        }
        public void PlayHit(int effectId, Vector3 position, Vector3 normal)
        {
            // TODO S08: 충돌점 효과를 대여하고 법선 방향으로 배치. 탄환 반환 후에도 독립 수명 유지.
            throw new NotImplementedException();
        }
    }
}
