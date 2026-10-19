using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// 씬과 UI를 모르는 저장 서비스다. 게임 전체에서 같은 인스턴스를 유지해 사용한다.
// Update, 주기적 저장, 종료·일시정지 저장은 없다.
// 요청과 완료 대기는 Unity 메인 스레드에서 호출한다.
public sealed class SaveScheduler
{
    private sealed class PendingSave
    {
        public string Json;
        public long Revision;
        public readonly UniTaskCompletionSource Completion = new UniTaskCompletionSource();
        public readonly UniTaskCompletionSource RunNow = new UniTaskCompletionSource();
    }

    private readonly SaveDataWriter writer;
    private readonly TimeSpan soonToSaveDelay;
    private readonly object stateLock = new object();
    private PendingSave pendingSave;
    private UniTask workerTask = UniTask.CompletedTask;
    private bool workerRunning;
    private bool isSaving;
    private long requestedRevision;
    private long savedRevision;
    private int flushWaiterCount;
    private string lastRequestedJson;
    private Exception lastSaveException;

    public bool IsSaving { get { lock (stateLock) return isSaving; } }
    public bool HasUnsavedChanges { get { lock (stateLock) return savedRevision < requestedRevision; } }
    public Exception LastSaveException { get { lock (stateLock) return lastSaveException; } }

    public SaveScheduler(SaveDataWriter writer, TimeSpan? soonToSaveDelay = null)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this.soonToSaveDelay = soonToSaveDelay ?? TimeSpan.FromSeconds(5);
        if (this.soonToSaveDelay.TotalMilliseconds < 0 || this.soonToSaveDelay.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(soonToSaveDelay));
    }

    // 인벤토리 등의 변경이 확정된 뒤 요청하고, 호출하는 쪽에서 반환 UniTask를 기다린다.
    public UniTask RequestSaveAsync(SaveData data, SavePolicy policy = SavePolicy.Immediate)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        ValidatePolicy(policy);
        if (data.SaveVersion != SaveDataVersion.CurrentVersion)
            throw new ArgumentException("현재 저장 형식이 아니다.", nameof(data));

        // 요청 시점에 스냅샷을 만든다. 예약 중 런타임이 바뀌어도 그 변경은 저장에 섞이지 않는다.
        JObject snapshot = JObject.FromObject(data);
        snapshot[nameof(SaveData.LastSavedAtUtc)] = DateTime.UtcNow.ToString("o");
        return Enqueue(snapshot.ToString(Formatting.Indented), policy);
    }

    // 새 스냅샷을 만들지 않고, 이미 승인한 저장 요청만 끝까지 기다린다.
    public async UniTask FlushAsync()
    {
        lock (stateLock) flushWaiterCount++;
        try
        {
            while (true)
            {
                UniTask task;
                lock (stateLock)
                {
                    pendingSave?.RunNow.TrySetResult();
                    task = workerTask;
                }

                await task;
                lock (stateLock)
                {
                    // 대기 중 추가 요청이 들어왔다면 그 요청까지 처리한다.
                    if (workerRunning) continue;
                    if (savedRevision < requestedRevision)
                        throw new InvalidOperationException("저장이 완료되지 않았다. 재시도가 필요하다.", lastSaveException);
                    return;
                }
            }
        }
        finally
        {
            lock (stateLock) flushWaiterCount--;
        }
    }

    public UniTask RetryAsync()
    {
        lock (stateLock)
        {
            if (lastRequestedJson == null)
                throw new InvalidOperationException("재시도할 저장 요청이 없다.");

            // 실패 후 현재 런타임을 다시 읽지 않는다. 마지막으로 승인한 데이터를 재시도한다.
            return Enqueue(lastRequestedJson, SavePolicy.Immediate);
        }
    }

    private UniTask Enqueue(string json, SavePolicy policy)
    {
        lock (stateLock)
        {
            lastRequestedJson = json;
            if (pendingSave == null) pendingSave = new PendingSave();
            pendingSave.Json = json;
            pendingSave.Revision = ++requestedRevision;
            UniTask completion = pendingSave.Completion.Task;

            // Flush를 기다리는 로딩 중에는 뒤이어 온 Soon도 즉시 처리한다.
            if (policy == SavePolicy.Immediate || flushWaiterCount > 0) pendingSave.RunNow.TrySetResult();
            if (!workerRunning)
            {
                workerRunning = true;
                UniTaskCompletionSource workerCompletion = new UniTaskCompletionSource();
                workerTask = workerCompletion.Task;
                ProcessPendingAsync(workerCompletion).Forget();
            }
            return completion;
        }
    }

    private async UniTask ProcessPendingAsync(UniTaskCompletionSource workerCompletion)
    {
        try
        {
            await DrainPendingAsync();
            workerCompletion.TrySetResult();
        }
        catch (Exception exception)
        {
            PendingSave failed;
            lock (stateLock)
            {
                failed = pendingSave;
                pendingSave = null;
                workerRunning = false;
                isSaving = false;
                lastSaveException = exception;
            }
            failed?.Completion.TrySetException(exception);
            workerCompletion.TrySetException(exception);
        }
    }

    private async UniTask DrainPendingAsync()
    {
        while (true)
        {
            PendingSave batch;
            lock (stateLock)
            {
                if (pendingSave == null)
                {
                    workerRunning = false;
                    return;
                }
                batch = pendingSave;
            }

            // Soon도 요청이 있을 때만 기다린다. Immediate나 Flush는 이 대기를 끝낸다.
            if (batch.RunNow.Task.Status == UniTaskStatus.Pending)
            {
                using (CancellationTokenSource delayCancellation = new CancellationTokenSource())
                {
                    // 로딩에서 timeScale을 0으로 바꿔도 예약 시간이 멈추지 않는다.
                    UniTask delay = UniTask.Delay(soonToSaveDelay, ignoreTimeScale: true, cancellationToken: delayCancellation.Token);
                    await UniTask.WhenAny(delay, batch.RunNow.Task);
                    batch.RunNow.TrySetResult();
                    delayCancellation.Cancel();
                }
            }

            string json;
            long revision;
            lock (stateLock)
            {
                json = batch.Json;
                revision = batch.Revision;
                pendingSave = null;
                isSaving = true;
            }

            try
            {
                await writer.SaveFileAsync(json);
                lock (stateLock)
                {
                    savedRevision = revision;
                    lastSaveException = null;
                    isSaving = false;
                }
                batch.Completion.TrySetResult();
            }
            catch (Exception exception)
            {
                lock (stateLock)
                {
                    lastSaveException = exception;
                    isSaving = false;
                }
                // 실패를 숨기지 않는다. 로딩 담당자가 예외를 받아 씬 전환을 멈출 수 있다.
                batch.Completion.TrySetException(exception);
            }
        }
    }

    private static void ValidatePolicy(SavePolicy policy)
    {
        if (policy != SavePolicy.Soon && policy != SavePolicy.Immediate)
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "지원하지 않는 저장 정책이다.");
    }
}
