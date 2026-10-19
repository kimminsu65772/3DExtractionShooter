using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 경로 해석과 전환만 담당한다. 저장 시점과 다음 목적지는 호출자가 결정한다.
public static class GameSceneManager
{
    // 전체 경로를 사용해 다른 폴더의 동명 씬과 혼동하지 않는다.
    private static readonly Dictionary<GameScene, string> scenePaths = new Dictionary<GameScene, string>
    {
        { GameScene.GameStart, "Assets/Scenes/GameStart.unity" },
        { GameScene.Lobby, "Assets/Scenes/Lobby/Base_SceneV2.unity" },
        { GameScene.Stage1, "Assets/Scenes/Level_GroundZero/Level_GroundZero_1.unity" },
        { GameScene.Loading, "Assets/Scenes/Loading.unity" }
    };

    public static bool IsLoading { get; private set; }

    public static string GetScenePath(GameScene scene) => scenePaths[scene];

    public static GameScene NextDestination { get; private set; }

    // 매핑에 없는 테스트 씬에서는 false를 반환한다.
    public static bool TryGetCurrentScene(out GameScene scene)
    {
        string activePath = SceneManager.GetActiveScene().path;

        if (activePath == "Assets/Scenes/Lobby/Base.unity")
            activePath = "Assets/Scenes/Lobby/Base_SceneV2.unity";

        foreach (KeyValuePair<GameScene, string> pair in scenePaths)
        {
            if (pair.Value == activePath)
            {
                scene = pair.Key;
                return true;
            }
        }

        scene = default;
        return false;
    }

    // true: 전환 완료. false: 이미 전환 중이거나 목적지가 현재 활성 씬이다.
    public static async UniTask<bool> LoadAsync(GameScene scene)
    {
        string path = GetScenePath(scene);
        if (IsLoading || SceneManager.GetActiveScene().path == path)
            return false;

        // 첫 비동기 대기 전에 잠가 같은 프레임의 연속 요청도 차단한다.
        IsLoading = true;
        try
        {
            await SceneManager.LoadSceneAsync(path, LoadSceneMode.Single).ToUniTask();
            return true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public static void MoveToNextDestination(GameScene scene)
    {
        if (IsLoading)
            return;

        // 목적지가 로딩 씬이면, 비정상적인 호출이므로 무시
        if (scene == GameScene.Loading)
            return;

        NextDestination = scene;
        SceneManager.LoadScene(GetScenePath(GameScene.Loading), LoadSceneMode.Single);
    }

    public static void ClearNextDestination()
    {
        NextDestination = default;
    }

}
