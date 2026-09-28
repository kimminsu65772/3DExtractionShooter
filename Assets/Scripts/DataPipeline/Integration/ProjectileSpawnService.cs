using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class ProjectileSpawnService
    {
        public bool TrySpawn(RuntimeWeapon weapon, AmmoDefinition ammo, Vector3 position, Vector3 direction,
            float aimDistance, GameObject attacker, LayerMask hitMask, DamageStatSnapshot damageStats, KeyedPoolService pools)
        {
            if (weapon == null || weapon.Definition == null)
                return false;

            if (pools == null || !pools.IsReady || weapon.ProjectileId <= 0)
                return false;

            if (direction.sqrMagnitude < 0.0001f ||
                aimDistance <= 0f ||
                float.IsNaN(aimDistance) ||
                float.IsInfinity(aimDistance))
            {
                return false;
            }

            if (!pools.TryRent(weapon.ProjectileId, out PoolLease lease))
                return false;

            try
            {
                GameObject instance = lease.Instance;

                if (!instance.TryGetComponent(
                    out PooledProjectileLifecycle lifecycle) ||
                    lifecycle.Projectile == null)
                {
                    throw new MissingComponentException(
                        $"투사체 프리팹에는 {nameof(PooledProjectileLifecycle)}와 " +
                        $"{nameof(Projectile)}이 필요합니다.");
                }

                Vector3 normalizedDirection = direction.normalized;

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

                // 설정이 끝난 뒤에만 활성화하여 Update가 불완전한 값을 읽지 않게 한다.
                pools.Activate(lease);
                return true;
            }
            catch (System.Exception exception)
            {
                // 초기화 또는 활성화 실패 시 대여한 인스턴스를 반드시 원래 풀로 돌려준다.
                pools.TryReturn(lease);
                Debug.LogException(exception, lease.Instance);
                return false;
            }
        }
    }
}
