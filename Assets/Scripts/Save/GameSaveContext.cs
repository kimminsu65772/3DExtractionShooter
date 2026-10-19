using System.IO;
using UnityEngine;

public static class GameSaveContext
{
    public static GameSaveService Service { get; private set; }

    // 런타임 시작 시점에 이른 초기화 작업을 수행하기 위해 RuntimeInitializeOnLoadMethod 특성 중 SubsystemRegistration을 사용한다.
    // 이 특성은 런타임 초기화 시점에 호출되며, 정적 필드나 싱글톤 인스턴스를 초기화하는 데 적합하다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Service = null;
    }

    // 씬이 로드되기 전에 GameSaveService를 초기화한다. 이를 통해 씬에서 세이브 데이터를 참조하기 전에 Service가 준비되도록 보장한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Service != null)
            return;
        string saveDirectory = Path.Combine(Application.persistentDataPath, "save.json");
        Service = new GameSaveService(saveDirectory);
    }
}