using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public class AddressablesAssetStore
    {
        private readonly Dictionary<AssetKey, AssetEntry> entries = new();

        private bool isShuttingDown;

        // IEqatable<T>: 두 개체가 내용상 같은 값을 갖는지 비교하는 방법을 타입이 직접 정의하도록 허용하는 인터페이스.
        // Assekey 구조체를 대상으로 한다는 뜻은, AssetKey 구조체가 두 개체가 같은지 비교하는 방법을 정의한다는 의미이다.
        // AssetKey 구조체는 주소와 타입을 바탕으로 동일성을 판단하므로, 두 가지가 모두 같으면 같은 키로 취급한다.
        private readonly struct AssetKey : IEquatable<AssetKey>
        {
            public readonly string Address;
            public readonly Type AssetType;
            public AssetKey(string address, Type assetType)
            {
                Address = address ?? throw new ArgumentNullException(nameof(address));
                AssetType = assetType ?? throw new ArgumentNullException(nameof(assetType));
            }
            // 주소의 경우 대소문자를 구분하는 Ordinal 비교를 사용하여 정확히 일치하는지 확인해야 한다.
            public bool Equals(AssetKey other)
            {
                return AssetType == other.AssetType && StringComparer.Ordinal.Equals(Address, other.Address);
            }
            // 같은 주소를 갖더라도 서로 다른 타입이면 다른 키로 취급해야 한다.
            public override bool Equals(object obj)
            {
                return obj is AssetKey other && Equals(other);
            }
            // 주소와 에셋 타입을 조합해 Dictionary 버킷을 찾는 데 사용할
            // 해시 코드를 생성한다. 해시가 같더라도 Equals에서 최종 동일성을 확인한다.
            public override int GetHashCode()
            {
                // unchecked 블록을 사용하여 오버플로우를 무시하고 해시 코드를 계산한다. 이는 해시 코드 계산에서 오버플로우가 발생하더라도
                // 예외를 발생시키지 않도록 하기 위함이다.
                unchecked
                {
                    // 위에서 진행한 Equals 비교와 동일하게, 주소의 해시 코드를 Ordinal 비교를 사용하여 생성한다.
                    int addressHash = StringComparer.Ordinal.GetHashCode(Address);

                    // XOR 연산을 사용하여 주소 해시와 타입 해시를 조합하여 최종 해시 코드를 생성한다. 397은 임의의 소수로, 해시 충돌을 줄이기 위해 사용된다.
                    return (addressHash * 397) ^ AssetType.GetHashCode();
                }
            }
        }

        private class AssetEntry
        {
            // 해제 완료를 나타내는 신호.
            private readonly TaskCompletionSource<bool> releasedSource = new TaskCompletionSource<bool>();

            // 에셋의 주소와 타입을 조합한 고유 키. 이 키를 사용하여 Addressables 시스템에서 리소스를 식별하고 관리한다.
            public AssetKey Key { get; }
            // 실제 Addressables API의 작업과 리소스 소유권을 나타내는 핸들. 이 핸들은 Addressables 시스템에서 리소스를 로드하고 관리하는 데 사용된다.
            public AsyncOperationHandle Handle { get; }

            public Task ReleasedTask => releasedSource.Task;

            // 로드 결과를 기다리는 AcquireAsync 호출자 수. 이 값은 동시에 여러 요청이 들어왔을 때, 로드가 완료될 때까지 기다리는 호출자 수를 추적하는 데 사용된다.
            public int WaiterCount;

            // 로드 후 실제 사용 중인 lease 수. 이 값은 에셋이 로드된 후 실제로 사용 중인 호출자 수를 추적하는 데 사용된다.
            public int LeaseCount;

            // 로딩이 진행 중이지만 모든 요청자가 취소되거나 이탈해
            // 결과를 받을 대상이 없어진 경우, 로딩 완료 즉시 핸들을 해제할지 나타낸다.
            public bool ReleaseWhenComplete;

            // 실제 Addressables.Release 호출이 수행되었는지 여부. 이 값이 true이면, 에셋이 이미 릴리스되었음을 나타낸다.
            public bool IsReleased;

            public AssetEntry(
                AssetKey key,
                AsyncOperationHandle handle)
            {
                Key = key;
                Handle = handle;
            }

            public void CompleteRelease()
            {
                releasedSource.TrySetResult(true);
            }
        }

        public async Task<AssetLease<T>> AcquireAsync<T>(string address, CancellationToken token)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException(
                    "Addressables 주소가 비어 있습니다.",
                    nameof(address));

            if (isShuttingDown)
                throw new ObjectDisposedException(
                    nameof(AddressablesAssetStore));

            // 취소 요청이 들어오면 즉시 예외를 발생시켜 호출자에게 알린다.
            token.ThrowIfCancellationRequested();

            AssetKey key = new AssetKey(address, typeof(T));
            AssetEntry entry = GetOrCreateEntry<T>(key);

            entry.ReleaseWhenComplete = false;
            entry.WaiterCount++;

            try
            {
                await WaitForLoadOrCancellationAsync(entry.Handle.Task, token);

                if (isShuttingDown)
                {
                    throw new ObjectDisposedException(
                        nameof(AddressablesAssetStore));
                }

                if (entry.Handle.Status != AsyncOperationStatus.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Addressables 로드 실패: {address}",
                        entry.Handle.OperationException);
                }

                if (!(entry.Handle.Result is T loadedAsset) || loadedAsset == null)
                {
                    throw new InvalidOperationException(
                        $"로드 결과가 요청한 타입과 일치하지 않습니다. " +
                        $"Address: {address}, Type: {typeof(T).Name}");
                }

                entry.LeaseCount++;

                return new AssetLease<T>(
                    loadedAsset,
                    () => ReleaseLease(entry));
            }
            finally
            {
                entry.WaiterCount--;

                if (entry.WaiterCount == 0 &&
                    entry.LeaseCount == 0)
                {
                    if (entry.Handle.IsDone)
                    {
                        ReleaseEntry(entry);
                    }
                    else
                    {
                        entry.ReleaseWhenComplete = true;
                    }
                }
            }
        }
        public async Task ShutdownAsync(CancellationToken token)
        {
            isShuttingDown = true;

            // 현재 엔트리 딕셔너리의 스냅샷을 만든다.
            List<AssetEntry> snapshot = new List<AssetEntry>(entries.Values);

            // 모든 엔트리에 대해 릴리스 작업을 수행할 수 있도록 Task 리스트를 생성한다.
            List<Task> releaseTasks = new List<Task>(snapshot.Count);

            // 리스트를 순회하며 각 엔트리의 ReleaseWhenComplete를 true로 설정하고, 릴리스 완료 Task를 수집한다.
            foreach (AssetEntry entry in snapshot)
            {
                entry.ReleaseWhenComplete = true;
                releaseTasks.Add(entry.ReleasedTask);

                if (entry.WaiterCount == 0 &&
                    entry.LeaseCount == 0 &&
                    entry.Handle.IsDone)
                {
                    ReleaseEntry(entry);
                }
            }

            // 모든 릴리스 작업이 완료될 때까지 기다린다. Task.WhenAll을 사용하여 모든 릴리스 Task가 완료될 때까지 비동기적으로 대기한다.
            Task allReleasedTask = Task.WhenAll(releaseTasks);

            // 모든 릴리스 작업이 완료되거나 취소 요청이 들어올 때까지 기다린다.
            // WaitForLoadOrCancellationAsync를 사용하여, 취소 토큰이 취소되면 즉시 예외를 발생시켜 호출자에게 알린다.
            await WaitForLoadOrCancellationAsync(
                allReleasedTask,
                token);
        }

        private AssetEntry GetOrCreateEntry<T>(AssetKey key) where T : UnityEngine.Object
        {
            if (entries.TryGetValue(key, out AssetEntry existingEntry))
            {
                return existingEntry;
            }

            AsyncOperationHandle handle = Addressables.LoadAssetAsync<T>(key.Address);

            AssetEntry newEntry = new AssetEntry(key, handle);

            entries.Add(key, newEntry);

            // 로드가 완료되면 TryReleaseAbandonedEntry를 호출하여, 모든 요청자가 이탈한 경우 핸들을 해제하도록 한다.
            // 이때 람다 매개변수로 전달되는 completedHandle은 실제로 사용하지 않으므로 언더스코어(_)로 무시한다.
            handle.Completed += _ =>
                TryReleaseAbandonedEntry(newEntry);

            return newEntry;
        }

        // 로드가 시작되었지만 해당 에셋에 대해 모든 요청이 취소되거나 이탈한 경우, 로딩 완료 즉시 핸들을 해제하는 메서드를 호출한다.
        private void TryReleaseAbandonedEntry(AssetEntry entry)
        {
            if (!entry.ReleaseWhenComplete)
                return;

            if (entry.WaiterCount > 0 ||
                entry.LeaseCount > 0)
            {
                return;
            }

            ReleaseEntry(entry);
        }

        // 핸들을 해제하고, entries 딕셔너리에서 제거하는 메서드. 이 메서드는 에셋이 더 이상 필요하지 않을 때 호출된다.
        private void ReleaseEntry(AssetEntry entry)
        {
            if (entry.IsReleased)
                return;

            entry.IsReleased = true;

            // ReferenceEquals를 사용하여 현재 entries 딕셔너리에 있는 entry와 동일한 객체인지 확인한다.
            // 만약 늦은 완료 콜백이 늦게 실행되는 사이에 같은 주소로 새로운 로드가 등록될 수 있으므로, 동일한 객체인지 확인하여 현재 entries 딕셔너리에서 제거한다.
            if (entries.TryGetValue(entry.Key, out AssetEntry currentEntry) && ReferenceEquals(currentEntry, entry))
            {
                entries.Remove(entry.Key);
            }

            try
            {
                if (entry.Handle.IsValid())
                {
                    Addressables.Release(entry.Handle);
                }
            }
            finally
            {
                entry.CompleteRelease();
            }
        }

        private static async Task WaitForLoadOrCancellationAsync(Task loadTask, CancellationToken token)
        {
            // 토큰이 취소 불가능한 경우에는 단순히 로드 작업만 기다린다.
            if (!token.CanBeCanceled)
            {
                await loadTask;
                return;
            }

            token.ThrowIfCancellationRequested();

            TaskCompletionSource<bool> cancellationSource = new TaskCompletionSource<bool>();

            // Task.WhenAny를 사용하여 로드 작업과 취소 토큰을 동시에 기다린다.
            // 이때, 취소 토큰이 취소되면 cancellationSource의 결과를 true로 설정하여 Task.WhenAny가 취소된 것으로 인식하도록 한다.
            using (token.Register(() => cancellationSource.TrySetResult(true)))
            {
                Task completedTask = await Task.WhenAny(loadTask, cancellationSource.Task);
                if (completedTask == cancellationSource.Task)
                {
                    // 취소 요청이 들어온 경우, 로드 작업이 완료되기 전에 예외를 발생시켜 호출자에게 알린다.
                    token.ThrowIfCancellationRequested();
                }
                // 로드 작업이 완료될 때까지 기다린다. 이때, 이미 완료된 경우 즉시 반환된다.
                await loadTask;
            }
        }

        private void ReleaseLease(AssetEntry entry)
        {
            // 중복 호출 방지
            if (entry.IsReleased)
                return;

            if (entry.LeaseCount <= 0)
            {
                throw new InvalidOperationException(
                    "반환할 AssetLease가 존재하지 않습니다.");
            }

            entry.LeaseCount--;

            // 아직 요청자나 사용자가 있으면 핸들을 해제하지 않는다.
            if (entry.WaiterCount > 0 || entry.LeaseCount > 0) return;

            if (entry.Handle.IsDone)
            {
                ReleaseEntry(entry);
            }
            else
            {
                entry.ReleaseWhenComplete = true;
            }
        }
    }
}
