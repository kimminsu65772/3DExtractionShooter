using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

// UI-01: 버튼 요청을 받아 로비 외부에서는 확인 후 실행한다.
[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    private enum PendingAction { None, MainMenu, Quit }

    [Header("UI 참조")]
    [SerializeField] private RectTransform warningPanel;
    [SerializeField] private TMP_Text warningTitle;

    private Action closeMenu;
    private PendingAction pendingAction;
    private bool isExecuting;
    private float previousTimeScale;
    private bool ownsTimePause;

    private void Awake()
    {
        if (warningPanel != null) warningPanel.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        // 경고 팝업은 자식이므로 취소해도 메뉴가 닫힐 때까지 정지를 유지한다.
        if (ownsTimePause) return;
        previousTimeScale = Time.timeScale;
        ownsTimePause = true;
        Time.timeScale = 0f;
    }

    public void Initialize(Action close) => closeMenu = close;

    public void OnResumeClicked()
    {
        if (isExecuting || pendingAction != PendingAction.None || UIManager.IsLoading()) return;
        CloseMenu();
    }

    public void OnMainMenuClicked() => RequestAction(PendingAction.MainMenu);
    public void OnExitClicked() => RequestAction(PendingAction.Quit);

    public void OnCancelClicked()
    {
        pendingAction = PendingAction.None;
        if (warningPanel == null) return;
        UIManager.Instance?.UnregisterPopup(warningPanel.gameObject);
        warningPanel.gameObject.SetActive(false);
    }

    public void OnConfirmClicked()
    {
        if (isExecuting || UIManager.IsLoading() || pendingAction == PendingAction.None) return;
        PendingAction action = pendingAction;
        OnCancelClicked();
        ExecuteAction(action);
    }

    private void RequestAction(PendingAction action)
    {
        if (isExecuting || UIManager.IsLoading() || pendingAction != PendingAction.None) return;
        if (GameSceneManager.TryGetCurrentScene(out GameScene scene) && scene == GameScene.Lobby)
        {
            ExecuteAction(action);
            return;
        }

        // 확인 UI가 누락됐을 때는 확인 없이 탐험을 종료하지 않는다.
        if (warningPanel == null || warningTitle == null)
        {
            Debug.LogError("PauseMenuController에 경고 패널과 제목 참조가 필요합니다.", this);
            return;
        }
        pendingAction = action;
        warningTitle.text = action == PendingAction.MainMenu ? "타이틀로" : "게임 종료";
        warningPanel.gameObject.SetActive(true);
        UIManager.Instance?.RegisterPopup(warningPanel.gameObject, OnCancelClicked);
    }

    private void ExecuteAction(PendingAction action)
    {
        if (isExecuting || UIManager.IsLoading()) return;
        isExecuting = true;
        CloseMenu();
        if (action == PendingAction.MainMenu)
            ReturnToMainMenuAsync().Forget();
        else
        {
            Application.Quit();
            // 에디터에서는 Quit이 종료하지 않으므로 다음 메뉴 요청을 허용한다.
            isExecuting = false;
        }
    }

    private async UniTask ReturnToMainMenuAsync()
    {
        try
        {
            // GameStart에는 플레이어 부트스트랩이 없으므로 직접 씬 전환 경로를 사용한다.
            await GameSceneManager.LoadAsync(GameScene.GameStart);
        }
        catch (Exception exception)
        {
            Debug.LogError($"타이틀 씬 이동에 실패했습니다: {exception.Message}", this);
        }
        finally
        {
            isExecuting = false;
        }
    }

    private void CloseMenu()
    {
        OnCancelClicked();
        if (closeMenu != null) closeMenu();
        else gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        try { OnCancelClicked(); }
        finally { RestoreTimeScale(); }
    }

    private void OnDestroy() => RestoreTimeScale();

    private void RestoreTimeScale()
    {
        if (!ownsTimePause) return;
        ownsTimePause = false;
        // Resume·ESC·씬 전환·부모 제거 모두 실제 패널 비활성화 경로에서 복구한다.
        Time.timeScale = previousTimeScale;
    }
}
