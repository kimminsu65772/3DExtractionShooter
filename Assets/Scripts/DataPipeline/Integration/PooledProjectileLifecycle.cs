using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    // 투사체가 현재 어느 풀에서 대여되었는지를 기억하고,
    // 사용이 끝났을 때 올바른 세대의 대여권을 소유 풀로 반환한다.
    public class PooledProjectileLifecycle : MonoBehaviour, IPoolLifecycle
    {
        [SerializeField] private Projectile projectile;

        private TrailRenderer[] trails;
        private PoolLease currentLease;
        private IPoolReturner owner;
        private bool hasLease;

        public Projectile Projectile => projectile;

        private void Awake()
        {
            EnsureCache();
        }

        private void EnsureCache()
        {
            if (projectile == null)
                projectile = GetComponent<Projectile>();

            trails ??= GetComponentsInChildren<TrailRenderer>(includeInactive: true);
        }

        public void OnRent(PoolLease lease, IPoolReturner owner)
        {
            // 비활성 부모 아래에서 생성된 프리팹은 Awake보다 OnRent가 먼저 올 수 있다.
            EnsureCache();

            Debug.Log(
                $"[Projectile Rent] " +
                $"Instance: {lease.Instance.GetInstanceID()}, " +
                $"Generation: {lease.Generation}"
            );

            if (owner == null)
                throw new System.ArgumentNullException(nameof(owner));

            if (projectile == null)
            {
                throw new MissingComponentException(
                    $"{nameof(PooledProjectileLifecycle)}에는 {nameof(Projectile)}이 필요합니다.");
            }

            currentLease = lease;
            this.owner = owner;
            hasLease = true;

            // 이전 발사에서 남은 궤적이 새 발사 위치까지 이어지지 않도록 지운다.
            for (int i = 0; i < trails.Length; i++)
            {
                TrailRenderer trail = trails[i];

                if (trail == null)
                    continue;

                trail.Clear();
                trail.emitting = true;
            }
        }

        public void OnReturn()
        {
            Debug.Log(
                $"[Projectile Return] " +
                $"Instance: {currentLease.Instance.GetInstanceID()}, " +
                $"Generation: {currentLease.Generation}"
            );

            // 다음 대여가 이전 공격의 무기, 공격자, 방향을 재사용하지 않도록 초기화한다.
            projectile?.ResetForPool();

            for (int i = 0; i < trails.Length; i++)
            {
                TrailRenderer trail = trails[i];

                if (trail == null)
                    continue;

                trail.emitting = false;
                trail.Clear();
            }

            hasLease = false;
            currentLease = default;
            owner = null;
        }



        public void ReturnToOwner()
        {
            if (!hasLease || owner == null)
                return;

            // TryReturn 안에서 OnReturn이 동기적으로 호출된다.
            // 먼저 플래그를 내리면 같은 프레임의 중복 반환 요청도 한 번만 처리된다.
            PoolLease lease = currentLease;
            IPoolReturner returner = owner;
            hasLease = false;

            if (!returner.TryReturn(lease))
            {
                // 이미 반환되었거나 이전 세대의 대여권이면 다시 시도하지 않는다.
                currentLease = default;
                owner = null;
            }
        }
    }
}
