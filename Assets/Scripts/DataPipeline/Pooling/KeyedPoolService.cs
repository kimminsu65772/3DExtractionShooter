using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public class KeyedPoolService : IPoolReturner
    {
        private readonly Dictionary<int, PrefabPool> poolsById = new();

        // 생성된 순서를 보존한다.
        // 초기화 실패 또는 종료 시 반대 순서로 풀을 정리할 때 사용한다.
        private readonly List<PrefabPool> initializationOrder = new();

        private bool isReady;
        private bool isShuttingDown;
        private Task shutdownTask;

        public bool IsReady => isReady;

        public async Task InitializeAsync(IReadOnlyList<PoolSpec> plan, AddressablesAssetStore assets,
            Transform inactiveRoot, CancellationToken token)
        {
            if (isReady)
                throw new InvalidOperationException("이미 초기화된 풀 서비스입니다.");

            if (isShuttingDown)
                throw new InvalidOperationException("종료 중인 풀 서비스는 초기화할 수 없습니다.");

            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            if (assets == null)
                throw new ArgumentNullException(nameof(assets));

            if (inactiveRoot == null)
                throw new ArgumentNullException(nameof(inactiveRoot));

            token.ThrowIfCancellationRequested();

            try
            {
                for (int i = 0; i < plan.Count; i++)
                {
                    token.ThrowIfCancellationRequested();

                    PoolSpec spec = plan[i];

                    ValidatePoolSpec(spec);

                    // 같은 아이디가 중복되면 어떤 풀을 사용해야 하는지 알 수 없으므로
                    // 초기화를 실패로 처리한다.
                    if (poolsById.ContainsKey(spec.PoolId))
                        throw new InvalidOperationException($"풀 ID {spec.PoolId}가 중복되었습니다.");

                    PrefabPool pool = new PrefabPool();

                    await pool.InitializeAsync(spec, assets, inactiveRoot, token);

                    poolsById.Add(spec.PoolId, pool);
                    initializationOrder.Add(pool);
                }

                isReady = true;
            }
            catch
            {
                await RollbackInitializationAsync();
                throw;
            }
        }

        private static void ValidatePoolSpec(PoolSpec spec)
        {
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));

            if (spec.PoolId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(spec.PoolId),
                    "PoolId는 0보다 커야 합니다.");

            if (string.IsNullOrWhiteSpace(spec.Address))
                throw new ArgumentException(
                    "프리팹 Address가 비어 있습니다.",
                    nameof(spec.Address));

            // AudioClip 자체는 GameObject가 아니므로 PrefabPool의 대상이 아니다.
            // 소리를 재생하는 AudioEmitter GameObject만 풀링한다.
            if (spec.Kind == AssetKind.AudioClip)
            {
                throw new ArgumentException(
                    "AudioClip은 PrefabPool의 대상이 아닙니다.",
                    nameof(spec.Kind));
            }

            if (spec.PrewarmCount < 0)
                throw new ArgumentOutOfRangeException(nameof(spec.PrewarmCount));

            if (spec.MaxActive <= 0)
                throw new ArgumentOutOfRangeException(nameof(spec.MaxActive));

            if (spec.MaxRetained <= 0)
                throw new ArgumentOutOfRangeException(nameof(spec.MaxRetained));

            if (spec.PrewarmCount > spec.MaxActive)
            {
                throw new ArgumentException(
                    "PrewarmCount는 MaxActive를 초과할 수 없습니다.");
            }

            if (spec.PrewarmCount > spec.MaxRetained)
            {
                throw new ArgumentException(
                    "PrewarmCount는 MaxRetained를 초과할 수 없습니다.");
            }
        }

        private async Task RollbackInitializationAsync()
        {
            isReady = false;

            // 나중에 생성된 풀이 먼저 정리되도록 역순으로 종료한다.
            for (int i = initializationOrder.Count - 1; i >= 0; i--)
            {
                PrefabPool pool = initializationOrder[i];

                if (pool == null)
                    continue;

                try
                {
                    await pool.ShutdownAsync();
                }
                catch (Exception exception)
                {
                    // 하나의 풀 정리가 실패해도 나머지 풀은 계속 정리한다.
                    Debug.LogException(exception);
                }
            }

            initializationOrder.Clear();
            poolsById.Clear();
        }

        public bool TryRent(int poolId, out PoolLease lease)
        {
            lease = default;

            // 전체 풀이 준비되지 않았거나 종료 중이면 새로운 대여를 허용하지 않는다.
            if (!isReady || isShuttingDown)
                return false;

            if (poolId <= 0)
                return false;

            // 정수 ID에 해당하는 프리팹 전용 풀을 찾는다.
            if (!poolsById.TryGetValue(poolId, out PrefabPool pool))
            {
                Debug.LogWarning(
                    $"등록되지 않은 PoolId로 대여를 요청했습니다. PoolId: {poolId}");
                return false;
            }

            // 실제 대여 가능 여부와 overflow 정책은 해당 PrefabPool이 판단한다.
            return pool.TryRent(out lease);
        }

        public void Activate(PoolLease lease)
        {
            // 대여권에 기록된 PoolId로 인스턴스의 소유 풀을 찾는다.
            if (!poolsById.TryGetValue(lease.PoolId, out PrefabPool pool))
            {
                throw new InvalidOperationException(
                    $"대여권의 소유 풀을 찾을 수 없습니다. PoolId: {lease.PoolId}");
            }

            // 인스턴스와 Generation의 최종 검증은 소유 PrefabPool이 수행한다.
            pool.Activate(lease);
        }

        public bool TryReturn(PoolLease lease)
        {
            // 이 서비스가 관리하지 않는 풀의 대여권은 반환받지 않는다.
            if (!poolsById.TryGetValue(lease.PoolId, out PrefabPool pool))
                return false;

            // 종료 중에도 아직 등록된 풀이라면 사용 중인 인스턴스를 반환할 수 있다.
            return pool.TryReturn(lease);
        }

        public Task ShutdownAsync()
        {
            // 중복 종료 요청은 최초 종료 작업을 함께 기다리도록 한다.
            return shutdownTask ??= ShutdownCoreAsync();
        }

        private async Task ShutdownCoreAsync()
        {
            // 새로운 대여를 먼저 막은 뒤 이미 만들어진 풀들을 정리한다.
            isShuttingDown = true;
            isReady = false;

            // 나중에 초기화된 풀부터 종료하여 생성 순서의 반대로 정리한다.
            for (int i = initializationOrder.Count - 1; i >= 0; i--)
            {
                PrefabPool pool = initializationOrder[i];

                if (pool == null)
                    continue;

                try
                {
                    await pool.ShutdownAsync();
                }
                catch (Exception exception)
                {
                    // 한 풀의 종료가 실패해도 나머지 풀은 계속 정리한다.
                    Debug.LogException(exception);
                }
            }

            initializationOrder.Clear();
            poolsById.Clear();
        }
    }
}
