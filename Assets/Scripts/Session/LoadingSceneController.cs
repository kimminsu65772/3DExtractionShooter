using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSceneController : MonoBehaviour
{
    [SerializeField] private GameScene destination = GameScene.Lobby;

    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private GameObject clickIndicator;
    [SerializeField] private Button continueButton;

    private UniTaskCompletionSource continueSignal;

    private GameObject eventSystem;
    private SceneBootStrap bootstrap;

    private void Awake()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(() =>
            {
                continueSignal?.TrySetResult();
            });
        }
    }

    private async void Start()
    {
        // 로비 씬으로 전환하기 이전에 로딩 씬을 기억해둔다.
        Scene loadingScene = gameObject.scene;

        // 플레이어의 입력을 막기 위해 로딩 UI를 활성화한다.
        ShowLoading();

        if (GameSceneManager.NextDestination != default)
        {
            if (GameSceneManager.NextDestination == GameScene.Lobby)
            {
                bool loadResult = await LoadLobbyScene(loadingScene);

                if (!loadResult)
                {
                    Debug.LogError("로비 씬의 초기화에 실패했습니다.");
                    return;
                }

                GameSceneManager.ClearNextDestination();
                return;
            }

            try
            {
                string nextScenePath = GameSceneManager.GetScenePath(GameSceneManager.NextDestination);

                UniTask nextSceneLoad = SceneManager.LoadSceneAsync(nextScenePath, LoadSceneMode.Additive).ToUniTask();

                await nextSceneLoad;

                Scene nextScene = SceneManager.GetSceneByPath(nextScenePath);

                SceneManager.SetActiveScene(nextScene);

                bootstrap = FindBootstrapInScene(nextScene);

                if (bootstrap == null)
                {
                    Debug.LogError("목적지 씬에 SceneBootStrap이 없습니다.");
                    return;
                }

                eventSystem = FindEventSystemInScene(nextScene);

                if (eventSystem == null)
                {
                    Debug.LogError("목적지 씬에 EventSystem이 없습니다.");
                    return;
                }

                bool initialized = await bootstrap.InitializeAsync();

                if (!initialized)
                {
                    Debug.LogError("목적지 씬의 초기화에 실패했습니다.");
                    return;
                }

                await SceneReadySequence(loadingScene);
            }

            catch (Exception ex)
            {
                Debug.LogError($"씬 전환 중 오류 발생: {ex.Message}", this);
                return;
            }

            GameSceneManager.ClearNextDestination();
            return;
        }

        bool result = await LoadLobbyScene(loadingScene);

        if (!result)
        {
            Debug.LogError("로비 씬의 초기화에 실패했습니다.");
            return;
        }
    }

    // 씬에서 SceneBootStrap을 찾아 반환하는 메서드
    private SceneBootStrap FindBootstrapInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            SceneBootStrap bootstrap =
                root.GetComponentInChildren<SceneBootStrap>(true);

            if (bootstrap != null)
                return bootstrap;
        }

        return null;
    }

    // BaseV2 씬에서 타겟 더미 스포너를 찾아 반환하는 메서드
    private TargetDummySpawner FindTargetDummySpawnerInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            TargetDummySpawner spawner =
                root.GetComponentInChildren<TargetDummySpawner>(true);
            if (spawner != null)
                return spawner;
        }
        return null;
    }

    private GameObject FindEventSystemInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject eventSystem = 
                root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true)?.gameObject;
            if (eventSystem != null)
                return eventSystem;
        }
        return null;
    }

    private async UniTask<bool> LoadLobbyScene(Scene loadingScene)
    {
        try
        {
            string scenePath = GameSceneManager.GetScenePath(destination);

            // additive 모드로 로딩 씬을 유지한 채 목적지 씬을 로드한다.
            UniTask baseLoad = SceneManager.LoadSceneAsync("Assets/Scenes/Lobby/Base.unity", LoadSceneMode.Additive).ToUniTask();

            UniTask environmentLoad = SceneManager.LoadSceneAsync(GameSceneManager.GetScenePath(GameScene.Lobby), LoadSceneMode.Additive).ToUniTask();

            await UniTask.WhenAll(baseLoad, environmentLoad);

            // 로드한 로비 씬에서 데이터 루트 오브젝트를 찾기 위해 목적지 씬을 path로 조회하여 가져온다.
            Scene baseScene = SceneManager.GetSceneByPath("Assets/Scenes/Lobby/Base.unity");

            Scene environmentScene = SceneManager.GetSceneByPath(GameSceneManager.GetScenePath(GameScene.Lobby));

            // 목적지 씬을 활성화하여, 그 씬의 오브젝트들이 활성화되도록 한다.
            SceneManager.SetActiveScene(baseScene);

            // 목적지 씬에서 SceneBootStrap을 찾아 초기화한다.
            bootstrap = FindBootstrapInScene(baseScene);

            if (bootstrap == null)
            {
                Debug.LogError("목적지 씬에 SceneBootStrap이 없습니다.");
                return false;
            }

            eventSystem = FindEventSystemInScene(baseScene);

            if (eventSystem == null)
            {
                Debug.LogError("목적지 씬에 EventSystem이 없습니다.");
                return false;
            }

            TargetDummySpawner spawner = FindTargetDummySpawnerInScene(environmentScene);

            if (spawner == null)
            {
                Debug.LogError("목적지 씬에 TargetDummySpawner가 없습니다.");
                return false;
            }

            bootstrap.SetTargetDummySpawner(spawner);

            // SceneBootStrap의 초기화 메서드를 호출하여 씬의 초기화를 진행하면서 로딩 씬을 유지한다.
            bool initialized = await bootstrap.InitializeAsync();

            if (!initialized)
            {
                Debug.LogError("목적지 씬의 초기화에 실패했습니다.");
                return false;
            }

            await SceneReadySequence(loadingScene);
        }

        catch (Exception ex)
        {
            Debug.LogError($"씬 전환 중 오류 발생: {ex.Message}", this);
            return false;
        }

        return true;
    }

    private void ShowLoading()
    {
        if (loadingIndicator != null)
            loadingIndicator.SetActive(true);
        if (clickIndicator != null)
            clickIndicator.SetActive(false);
        if (continueButton != null)
            continueButton.gameObject.SetActive(false);
    }

    private void ShowReady()
    {
        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);
        if (clickIndicator != null)
            clickIndicator.SetActive(true);
        if (continueButton != null)
            continueButton.gameObject.SetActive(true);
    }

    private async UniTask SceneReadySequence(Scene loadingScene)
    {
        continueSignal = new UniTaskCompletionSource();

        ShowReady();
        await continueSignal.Task;

        await SceneManager.UnloadSceneAsync(loadingScene).ToUniTask();


        bootstrap.SetPlayerInputBlocked(false);
        eventSystem.SetActive(true);
    }
}
