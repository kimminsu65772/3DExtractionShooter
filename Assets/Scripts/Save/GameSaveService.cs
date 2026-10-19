using Cysharp.Threading.Tasks;
public class GameSaveService
{
    private readonly SaveDataLoader loader;
    private readonly SaveScheduler scheduler;

    public bool HasSaveData => loader.Exists();

    public GameSaveService(string saveFilePath)
    {
        loader = new SaveDataLoader(saveFilePath);
        SaveDataWriter writer = new SaveDataWriter(saveFilePath);
        scheduler = new SaveScheduler(writer);
    }

    public UniTask SaveAsync(GameSaveData data)
    {
        return scheduler.RequestSaveAsync(data);
    }

    public GameSaveData Load()
    {
        return loader.Load<GameSaveData>();
    }

    public UniTask FlushAsync()
    {
        return scheduler.FlushAsync();
    }

    public UniTask RetryAsync()
    {
        return scheduler.RetryAsync();
    }
}
