using System;
using System.IO;
using Cysharp.Threading.Tasks;

public sealed class SaveDataWriter
{
    private readonly string saveFilePath;
    private readonly object queueLock = new object();
    private UniTask writeTail = UniTask.CompletedTask;

    public SaveDataWriter(string saveFilePath)
    {
        if (string.IsNullOrWhiteSpace(saveFilePath))
            throw new ArgumentException("저장 파일 경로가 비어 있다.", nameof(saveFilePath));

        this.saveFilePath = Path.GetFullPath(saveFilePath);
    }

    public UniTask SaveFileAsync(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("저장할 JSON이 비어 있다.", nameof(json));

        lock (queueLock)
        {
            // 단순 lock은 동시 쓰기만 막는다. 작업을 이어 붙여 요청 순서도 보장한다.
            UniTask previousWrite = writeTail;
            // 일반 UniTask의 중복 await 대신 여러 대기를 지원하는 완료 소스를 공유한다.
            UniTaskCompletionSource completion = new UniTaskCompletionSource();
            writeTail = completion.Task;
            WriteAfterAsync(previousWrite, json, completion).Forget();
            return completion.Task;
        }
    }

    private async UniTask WriteAfterAsync(UniTask previousWrite, string json, UniTaskCompletionSource completion)
    {
        try
        {
            await previousWrite;
        }
        catch (Exception)
        {
            // 이전 실패는 그 요청으로 전달된다. 새 요청이나 재시도까지 막지 않는다.
        }

        // 런타임 객체를 작업 스레드에서 읽지 않는다. 이미 복사된 문자열만 쓴다.
        try
        {
            // 파일 쓰기만 작업 스레드에서 수행하고, 완료와 실패는 메인 스레드로 돌아와 알린다.
            await UniTask.RunOnThreadPool(() => SaveToFile(json), configureAwait: true);
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private void SaveToFile(string json)
    {
        string directoryPath = Path.GetDirectoryName(saveFilePath);
        if (!string.IsNullOrEmpty(directoryPath))
            Directory.CreateDirectory(directoryPath);

        string tempFilePath = saveFilePath + ".tmp";
        string backupFilePath = saveFilePath + ".bak";
        File.WriteAllText(tempFilePath, json);

        // 새 파일을 완성한 뒤 기존 저장을 교체한다. 실패했다고 기존 파일을 먼저 지우지 않는다.
        if (File.Exists(saveFilePath))
            File.Replace(tempFilePath, saveFilePath, backupFilePath);
        else
            File.Move(tempFilePath, saveFilePath);
    }
}
