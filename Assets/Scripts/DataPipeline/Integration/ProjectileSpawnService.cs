using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class ProjectileSpawnService
    {
        public bool TrySpawn(RuntimeWeapon weapon, Vector3 position, Vector3 direction,
            float aimDistance, GameObject attacker, LayerMask hitMask, KeyedPoolService pools)
        {
            // TODO S08: ProjectileId 대여→캐시된 Projectile 초기화→lease 반환 콜백 연결→활성화.
            // TODO: 기존 Projectile.Initialize의 SetActive를 분리하고 RequestReturn을 owner 반환으로 교체.
            // TODO: 초기화 실패 시 빌린 lease 반환. 실제 성공일 때만 공격자가 쿨다운/반동을 확정.
            // TODO: Projectile.Update/피해 계산을 여기에 복제하지 않는다.
            throw new NotImplementedException();
        }
    }
}
