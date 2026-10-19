using UnityEngine;

public class MoveScene : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionLabel = "이동";
    [SerializeField] private GameScene targetScene;
    [Header("로비 복귀 대기")]
    [SerializeField, Min(0.1f)] private float interactionDuration = 3f;
    private SceneBootStrap bootstrap;

    public string InteractionLabel => interactionLabel;

    public bool CanInteract(PlayerInteractionController interactor)
    {
        // 상호작용 가능 여부를 판단한다. 현재 씬과 이동 대상 씬이 다를 때만 상호작용 가능하다.
        if (!isActiveAndEnabled || interactor == null || !GameSceneManager.TryGetCurrentScene(out var currentScene) || currentScene == targetScene)
            return false;
        // [DATA-01 변경] 로비 환경이 별도 additive 씬이어도 플레이어가 속한 씬의 담당자를 사용한다.
        if (bootstrap == null)
            foreach (GameObject root in interactor.gameObject.scene.GetRootGameObjects())
            {
                bootstrap = root.GetComponentInChildren<SceneBootStrap>(true);
                if (bootstrap != null) break;
            }
        return bootstrap != null && bootstrap.CanRequestSceneTransition;
    }

    public bool TryInteract(PlayerInteractionController interactor)
    {
        if (!CanInteract(interactor)) return false;

        // 출발은 기존 즉시 이동을 사용하고, 탐험에서 로비로 돌아갈 때만 대기한다.
        if (targetScene == GameScene.Lobby)
            return interactor.TryStartTimedInteraction(this, interactionDuration, CompleteTransition);

        return bootstrap.TryBeginSceneTransition(targetScene);
    }

    private void CompleteTransition()
    {
        // [DATA-01 변경] 3초 완료는 결과 확정 요청이다. 저장 성공 후 실제 이동은 부트스트랩이 수행한다.
        if (bootstrap != null) bootstrap.TryBeginSceneTransition(targetScene);
    }
}
