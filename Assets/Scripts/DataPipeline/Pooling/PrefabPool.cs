using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace ExtractionShooter.DataPipeline
{
    public class PrefabPool : IPoolReturner
    {
        private enum InstanceStatus
        {
            Inactive,
            Rented,
            Active,
            Returning,
            Destroyed
        }

        private class InstanceState
        {
            public GameObject Instance;
            public IPoolLifecycle[] Lifecycles;
            public uint Generation;
            public InstanceStatus Status;
        }

        private readonly Dictionary<GameObject, InstanceState> _instanceStates = new();

        private PoolSpec _spec;
        private Transform _inactiveRoot;
        private AssetLease<GameObject> _prefabLease;
        private ObjectPool<GameObject> _objectPool;

        private bool isReady;
        private bool isShuttingDown;
        private int activeCount;
        private Task shutdownTask;

        // Addresables에서 원본 프래핍을 빌려 해당 프리팹으로 ObjectPool<GameObject>를 생성하고, inactiveRoot 아래에 비활성 상태로 미리 생성한다.
        public async Task InitializeAsync(PoolSpec spec, AddressablesAssetStore assets, Transform inactiveRoot,
            CancellationToken token)
        {
            if (isReady)
                throw new InvalidOperationException("이미 초기화된 풀입니다.");

            if (spec == null)
                throw new ArgumentNullException(nameof(spec));

            if (assets == null)
                throw new ArgumentNullException(nameof(assets));

            if (inactiveRoot == null)
                throw new ArgumentNullException(nameof(inactiveRoot));

            // 프리팹 생성 직후 OnEnable이 호출되지 않도록
            // 인스턴스를 생성할 부모는 비활성 상태여야 한다.
            if (inactiveRoot.gameObject.activeInHierarchy)
            {
                throw new ArgumentException(
                    "inactiveRoot는 비활성 상태여야 합니다.",
                    nameof(inactiveRoot));
            }

            token.ThrowIfCancellationRequested();

            _spec = spec;
            _inactiveRoot = inactiveRoot;

            try
            {
                // 풀의 수명 동안 원본 프리팹을 사용할 권한을 빌린다.
                _prefabLease = await assets.AcquireAsync<GameObject>(spec.Address, token);

                token.ThrowIfCancellationRequested();

                // ObjectPool<GameObject>를 생성한다.
                _objectPool = new ObjectPool<GameObject>(
                    createFunc: CreateInstance,
                    actionOnGet: null,
                    actionOnRelease: MoveToInactiveRoot,
                    actionOnDestroy: DestroyInstance,
                    collectionCheck: true,
                    defaultCapacity: Mathf.Max(1, spec.PrewarmCount),
                    maxSize: spec.MaxRetained);

                List<GameObject> prewarmedInstances = new List<GameObject>(spec.PrewarmCount);

                for (int i = 0; i < spec.PrewarmCount; i++)
                {
                    token.ThrowIfCancellationRequested();
                    GameObject instance = _objectPool.Get();
                    prewarmedInstances.Add(instance);
                }

                foreach (GameObject instance in prewarmedInstances)
                {
                    _objectPool.Release(instance);
                }

                isReady = true;
            }
            catch
            {
                CleanInitializationFailure();
                throw;
            }
        }

        private GameObject CreateInstance()
        {
            GameObject instance = UnityEngine.Object.Instantiate(_prefabLease.Asset, _inactiveRoot);

            instance.SetActive(false);

            IPoolLifecycle[] lifecycles = instance.GetComponentsInChildren<IPoolLifecycle>(includeInactive: true);

            InstanceState state = new InstanceState
            {
                Instance = instance,
                Lifecycles = lifecycles,
                Generation = 0,
                Status = InstanceStatus.Inactive
            };

            _instanceStates.Add(instance, state);

            return instance;
        }

        private void MoveToInactiveRoot(GameObject instance)
        {
            if (instance == null)
                return;

            instance.SetActive(false);
            instance.transform.SetParent(_inactiveRoot, false);
        }

        private void DestroyInstance(GameObject instance)
        {
            if (instance == null)
                return;

            if (_instanceStates.TryGetValue(instance, out InstanceState state))
            {
                state.Status = InstanceStatus.Destroyed;
                _instanceStates.Remove(instance);
            }

            UnityEngine.Object.Destroy(instance);
        }

        private void CleanInitializationFailure()
        {
            isReady = false;

            _objectPool?.Clear();
            _objectPool = null;

            List<GameObject> remainingInstances = new List<GameObject>(_instanceStates.Keys);

            foreach (GameObject instance in remainingInstances)
            {
                if (instance != null)
                    DestroyInstance(instance);
            }

            _instanceStates.Clear();

            _prefabLease?.Dispose();
            _prefabLease = null;

            _spec = null;
            _inactiveRoot = null;
            activeCount = 0;
        }

        public bool TryRent(out PoolLease lease)
        {
            lease = default;

            // 초기화되지 않았거나 종료 중이면 대여를 거부한다.
            if (!isReady || isShuttingDown || _objectPool == null)
                return false;

            if (activeCount >= _spec.MaxActive)
                return false;

            // 현재 대기중인 인스턴스가 없고, 오버플로우 정책이 Reject(오브젝트가 모자를 때 추가 생성을 거부)이면 대여를 거부한다.
            if (_objectPool.CountInactive == 0 && _spec.Overflow == PoolOverflowPolicy.Reject)
                return false;

            GameObject instance = null;
            InstanceState state = null;
            int rentedLifecycleCount = 0;

            try
            {
                instance = _objectPool.Get();

                if (instance == null)
                    throw new InvalidOperationException("ObjectPool에서 인스턴스를 가져오지 못했습니다.");

                if (!_instanceStates.TryGetValue(instance, out state))
                    throw new InvalidOperationException("인스턴스 상태를 찾을 수 없습니다.");

                if (state.Status != InstanceStatus.Inactive)
                    throw new InvalidOperationException($"비활성 상태가 아닌 인스턴스가 대여되었습니다. 현재 상태: {state.Status}");

                state.Generation = GetNextGeneration(state.Generation);
                state.Status = InstanceStatus.Rented;
                activeCount++;

                lease = new PoolLease
                (
                    _spec.PoolId,
                    instance,
                    state.Generation
                );

                for (int i = 0; i < state.Lifecycles.Length; i++)
                {
                    IPoolLifecycle lifecycle = state.Lifecycles[i];

                    if (lifecycle == null)
                        continue;

                    // OnRent 호출 중 예외가 발생하면 해당 컴포넌트를까지 정리 대상에 포함할 수 있도록 호출 전에 미리 카운트를 증가시킨다.
                    rentedLifecycleCount = i + 1;
                    lifecycle.OnRent(lease, this);
                }

                return true;
            }
            catch (Exception e)
            {
                RollbackFailedRent(state, rentedLifecycleCount);

                lease = default;
                Debug.LogException(e, instance);
            }

            return false;
        }

        private static uint GetNextGeneration(uint currentGeneration)
        {
            // uint 최대값 다음은 0이 되므로,
            // 0을 건너뛰고 다시 1부터 시작한다.
            return currentGeneration == uint.MaxValue
                ? 1u
                : currentGeneration + 1u;
        }

        private void RollbackFailedRent(InstanceState state, int rentedLifecycleCount)
        {
            if (state == null)
                return;

            bool canReuse = true;

            // 초기화된 역순으로 OnReturn을 호출하여, OnRent에서 설정한 상태를 되돌린다.
            for (int i = rentedLifecycleCount - 1; i >= 0; i--)
            {
                IPoolLifecycle lifecycle = state.Lifecycles[i];

                if (lifecycle == null)
                    continue;

                try
                {
                    lifecycle.OnReturn();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, state.Instance);
                    canReuse = false;
                }
            }

            activeCount = Math.Max(0, activeCount - 1);

            if (canReuse)
            {
                state.Status = InstanceStatus.Inactive;
                _objectPool.Release(state.Instance);
            }
            else
            {
                DestroyInstance(state.Instance);
            }
        }

        public void Activate(PoolLease lease)
        {
            if (!isReady || isShuttingDown)
            {
                throw new InvalidOperationException(
                    "준비되지 않았거나 종료 중인 풀에서는 인스턴스를 활성화할 수 없습니다.");
            }

            if (!TryGetMatchingState(lease, out InstanceState state))
            {
                throw new InvalidOperationException(
                    "현재 풀에서 발급한 유효한 대여권이 아닙니다.");
            }

            // 활성화는 TryRent 직후 한 번만 할 수 있다.
            if (state.Status != InstanceStatus.Rented)
            {
                throw new InvalidOperationException(
                    $"대여 상태가 아닌 인스턴스는 활성화할 수 없습니다. 현재 상태: {state.Status}");
            }

            GameObject instance = state.Instance;

            // inactiveRoot가 비활성 상태이므로 그 아래에서는
            // SetActive(true)를 호출해도 activeInHierarchy가 true가 되지 않는다.
            // 호출자가 별도 활성 부모를 설정하지 않았다면 씬 루트로 옮긴다.
            if (instance.transform.IsChildOf(_inactiveRoot))
            {
                instance.transform.SetParent(null, true);
            }

            // OnEnable에서 반환 요청 등이 발생해도 올바른 상태를 볼 수 있도록
            // GameObject를 활성화하기 전에 상태부터 Active로 전환한다.
            state.Status = InstanceStatus.Active;
            instance.SetActive(true);
        }

        private bool TryGetMatchingState(PoolLease lease, out InstanceState state)
        {
            state = null;

            // 다른 풀에서 발급한 대여권은 사용하지 않는다.
            if (_spec == null || lease.PoolId != _spec.PoolId)
                return false;

            if (lease.Instance == null)
                return false;

            if (!_instanceStates.TryGetValue(lease.Instance, out state))
                return false;

            if (state.Generation != lease.Generation)
            {
                state = null;
                return false;
            }

            return true;
        }

        public bool TryReturn(PoolLease lease)
        {
            // 다른 풀의 대여권, 이미 파괴된 인스턴스,
            // 이전 세대의 대여권은 반환할 수 없다.
            if (!TryGetMatchingState(lease, out InstanceState state))
                return false;

            // Rented는 활성화 전 취소, Active는 사용 완료에 의한 반환이다.
            // 그 외의 상태는 이중 반환이거나 이미 반환 중인 상태다.
            if (state.Status != InstanceStatus.Rented &&
                state.Status != InstanceStatus.Active)
            {
                return false;
            }

            // OnReturn 내부에서 다시 반환을 요청하더라도 거부되도록
            // 콜백을 호출하기 전에 상태부터 Returning으로 변경한다.
            state.Status = InstanceStatus.Returning;

            bool canReuse = true;

            // OnRent의 반대 순서로 정리한다.
            for (int i = state.Lifecycles.Length - 1; i >= 0; i--)
            {
                IPoolLifecycle lifecycle = state.Lifecycles[i];

                if (lifecycle == null)
                    continue;

                try
                {
                    lifecycle.OnReturn();
                }
                catch (Exception exception)
                {
                    // 상태 초기화에 실패한 객체를 다시 빌려주면
                    // 이전 투사체·이벤트·타이머 정보가 남을 수 있으므로 재사용하지 않는다.
                    canReuse = false;
                    Debug.LogException(exception, state.Instance);
                }
            }

            // TryRent에서 증가시킨 대여 개수를 한 번만 감소시킨다.
            activeCount = Math.Max(0, activeCount - 1);

            // 종료 중에는 반환된 인스턴스를 다시 보관하지 않는다.
            if (!canReuse || isShuttingDown || _objectPool == null)
            {
                DestroyInstance(state.Instance);
                return true;
            }

            try
            {
                state.Status = InstanceStatus.Inactive;

                // actionOnRelease로 등록한 MoveToInactiveRoot가 호출되어
                // 비활성화 및 inactiveRoot 아래로 이동한다.
                _objectPool.Release(state.Instance);
            }
            catch (Exception exception)
            {
                // ObjectPool 반환 과정이 실패한 인스턴스도 격리하여 파괴한다.
                Debug.LogException(exception, state.Instance);
                DestroyInstance(state.Instance);
            }

            // 대여권이 유효했고 반환 요청을 처리했으므로 true다.
            return true;
        }
        public Task ShutdownAsync()
        {
            return shutdownTask ??= ShutdownCoreAsync();
        }

        private async Task ShutdownCoreAsync()
        {
            // 새로운 대여와 활성화를 먼저 차단한다.
            isShuttingDown = true;
            isReady = false;

            // ObjectPool 안에 보관 중인 비활성 인스턴스를 제거한다.
            // Clear가 actionOnDestroy로 등록한 DestroyInstance를 호출한다.
            if (_objectPool != null)
            {
                try
                {
                    _objectPool.Clear();
                }
                catch (Exception exception)
                {
                    // 일부 인스턴스 정리에 실패해도 나머지 종료 작업은 계속한다.
                    Debug.LogException(exception);
                }

                _objectPool = null;
            }

            // Clear 이후 Dictionary에 남은 객체는 주로 현재 대여 중인 인스턴스다.
            List<InstanceState> remainingStates =
                new List<InstanceState>(_instanceStates.Values);

            foreach (InstanceState state in remainingStates)
            {
                if (state == null)
                    continue;

                // 사용 중인 객체라면 반환과 같은 생명주기 정리를 수행한다.
                if (state.Status == InstanceStatus.Rented ||
                    state.Status == InstanceStatus.Active)
                {
                    // OnReturn 도중 재진입하는 반환 요청을 막는다.
                    state.Status = InstanceStatus.Returning;

                    for (int i = state.Lifecycles.Length - 1; i >= 0; i--)
                    {
                        IPoolLifecycle lifecycle = state.Lifecycles[i];

                        // IPoolLifecycle가 UnityEngine.Object를 상속받은 컴포넌트라면 이미 파괴되어 null이 될 수 있으므로, null 체크 후 OnReturn을 호출한다.
                        if (!IsLifecycleAlive(lifecycle))
                            continue;

                        try
                        {
                            lifecycle.OnReturn();
                        }
                        catch (Exception exception)
                        {
                            // 종료 중에는 재사용하지 않으므로 오류를 기록하고
                            // 나머지 컴포넌트와 인스턴스 정리를 계속한다.
                            Debug.LogException(exception, state.Instance);
                        }
                    }
                }

                if (state.Instance != null)
                {
                    state.Instance.SetActive(false);
                    DestroyInstance(state.Instance);
                }
            }

            _instanceStates.Clear();
            activeCount = 0;

            // Unity의 Destroy는 즉시 파괴하지 않고 프레임 마지막에 처리된다.
            // 따라서 원본 Addressables lease를 반환하기 전에 한 번 실행 흐름을 양보하여, 모든 인스턴스가 파괴되도록 한다.
            // 그 후 원본 프리팹 대여권을 반환한다.
            await Awaitable.NextFrameAsync();

            _prefabLease?.Dispose();
            _prefabLease = null;

            _spec = null;
            _inactiveRoot = null;
        }

        private static bool IsLifecycleAlive(IPoolLifecycle lifecycle)
        {
            if (lifecycle == null)
                return false;

            // 단순히 인터페이스로 null 체크만 해서는, UnityEngine.Object를 상속받은 컴포넌트가 이미 파괴되어도 null이 아닌 것으로 판단될 수 있다.
            if (lifecycle is UnityEngine.Object unityObject)
                return unityObject != null;

            return true;
        }
    }
}
