using UnityEngine;
using System.Collections.Generic;

namespace ExtractionShooter.DataPipeline
{
    public class ProjectileSpawnService
    {
        // 한 번의 발사에 필요한 대여권을 모아 두고, 준비 실패 시 전부 반환한다.
        private readonly List<PoolLease> pendingLeases = new List<PoolLease>(8);

        public bool TrySpawn(RuntimeWeapon weapon, AmmoDefinition ammo, Vector3 position, Vector3 direction,
            float aimDistance, GameObject attacker, LayerMask hitMask, DamageStatSnapshot damageStats, KeyedPoolService pools)
        {
            return TrySpawnCore(weapon, ammo, position, direction, aimDistance, attacker, hitMask, damageStats, pools, 1, 0f);
        }

        // 산탄을 여러 개 생성하더라도 호출자는 성공한 한 번의 발사에 탄약을 한 발만 소비한다.
        public bool TrySpawnShot(RuntimeWeapon weapon, AmmoDefinition ammo, Vector3 position, Vector3 direction,
            float aimDistance, GameObject attacker, LayerMask hitMask, DamageStatSnapshot damageStats, KeyedPoolService pools)
        {
            if (weapon == null || weapon.Definition == null) return false;
            return TrySpawnCore(weapon, ammo, position, direction, aimDistance, attacker, hitMask, damageStats, pools, weapon.Definition.bulletsPerShot, weapon.Definition.singleShotAngle);
        }

        private bool TrySpawnCore(RuntimeWeapon weapon, AmmoDefinition ammo, Vector3 position, Vector3 direction,
            float aimDistance, GameObject attacker, LayerMask hitMask, DamageStatSnapshot damageStats, KeyedPoolService pools, int count, float pelletAngle)
        {
            if (weapon == null || weapon.Definition == null)
                return false;

            if (pools == null || !pools.IsReady || weapon.ProjectileId <= 0)
                return false;

            if (!IsFinite(direction) || !IsFinite(position) || direction.sqrMagnitude < 0.0001f ||
                aimDistance <= 0f ||
                float.IsNaN(aimDistance) ||
                float.IsInfinity(aimDistance))
            {
                return false;
            }

            if (count <= 0 || count > 128 || float.IsNaN(pelletAngle) || float.IsInfinity(pelletAngle) || pelletAngle < 0f || pelletAngle > 89f) return false;
            pendingLeases.Clear();

            try
            {
                for (int i = 0; i < count; i++)
                {
                    // 모두 준비하기 전에 하나라도 부족하면 먼저 빌린 탄환도 돌려준다.
                    if (!pools.TryRent(weapon.ProjectileId, out PoolLease lease))
                    {
                        ReturnPending(pools);
                        return false;
                    }
                    pendingLeases.Add(lease);
                    GameObject instance = lease.Instance;

                    if (!instance.TryGetComponent(
                        out PooledProjectileLifecycle lifecycle) ||
                        lifecycle.Projectile == null)
                    {
                        throw new MissingComponentException(
                            $"투사체 프리팹에는 {nameof(PooledProjectileLifecycle)}와 " +
                            $"{nameof(Projectile)}이 필요합니다.");
                    }

                    Vector3 normalizedDirection = WeaponSpreadUtility.ApplyFanSpread(direction, pelletAngle, i, count);

                    // OnRent 이후, 아직 비활성인 동안 이번 발사 상태를 모두 설정한다.
                    instance.transform.SetPositionAndRotation(
                        position,
                        Quaternion.LookRotation(normalizedDirection, Vector3.up));

                    lifecycle.Projectile.Initialize(
                        weapon.Definition,
                        ammo,
                        position,
                        normalizedDirection,
                        aimDistance,
                        attacker,
                        hitMask,
                        damageStats);
                }

                // 모든 탄환의 대여와 초기화가 성공한 뒤에만 함께 활성화한다.
                foreach (PoolLease lease in pendingLeases) pools.Activate(lease);
                pendingLeases.Clear();
                return true;
            }
            catch (System.Exception exception)
            {
                // 초기화 또는 활성화 실패 시 대여한 인스턴스를 반드시 원래 풀로 돌려준다.
                ReturnPending(pools);
                Debug.LogException(exception);
                return false;
            }
        }

        // 잘못된 좌표가 물리 이동과 풀 상태를 오염시키지 않게 막는다.
        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private void ReturnPending(KeyedPoolService pools)
        {
            foreach (PoolLease lease in pendingLeases) pools.TryReturn(lease);
            pendingLeases.Clear();
        }
    }
}
