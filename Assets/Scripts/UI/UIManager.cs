using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-300)]
public sealed class UIManager : MonoBehaviour
{
    private sealed class PopupEntry
    {
        public GameObject Root;
        public Action Close;
    }

    public static UIManager Instance { get; private set; }
    [SerializeField] private EscapeMenuController escapeMenu;
    private readonly Stack<PopupEntry> popups = new Stack<PopupEntry>();
    private readonly Stack<PopupEntry> retained = new Stack<PopupEntry>();

    public bool HasOpenPopups
    {
        get
        {
            PruneClosedPopups();
            return popups.Count > 0;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>("UI/EscapeMenuHost");
        if (prefab != null) Instantiate(prefab);
        else Debug.LogError("UIManager 호스트를 찾지 못했습니다: Resources/UI/EscapeMenuHost");
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (escapeMenu == null) escapeMenu = GetComponent<EscapeMenuController>();
    }

    private void OnEnable() => SceneManager.activeSceneChanged += OnActiveSceneChanged;

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        CloseAllPopups();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Instance != this) return;
        if (IsLoading())
        {
            CloseAllPopups();
            return;
        }

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        // 한 번 닫은 입력으로 메뉴까지 열지 않도록 두 분기 중 하나만 실행한다.
        if (HasOpenPopups) CloseTopPopup();
        else if (escapeMenu != null) escapeMenu.Open();
    }

    public static bool IsLoading()
    {
        // 목적지가 먼저 활성화되어도 additive 로딩 화면과 클릭 대기가 끝날 때까지 차단한다.
        Scene loading = SceneManager.GetSceneByPath(GameSceneManager.GetScenePath(GameScene.Loading));
        return GameSceneManager.IsLoading || (loading.IsValid() && loading.isLoaded);
    }

    public void RegisterPopup(GameObject root, Action close)
    {
        if (root == null || !root.activeInHierarchy || close == null) return;
        foreach (PopupEntry popup in popups)
            if (popup.Root == root) return;
        popups.Push(new PopupEntry { Root = root, Close = close });
    }

    public void UnregisterPopup(GameObject root)
    {
        // Tab/상자 거리 이탈/부모 닫기로 중간 UI가 닫혀도 나머지 순서를 보존한다.
        // UI 수가 적으므로 등록 해제 시 O(n) 순회하고 별도 인덱스는 두지 않는다.
        while (popups.Count > 0)
        {
            PopupEntry popup = popups.Pop();
            if (popup.Root != root && popup.Root != null && popup.Root.activeInHierarchy)
                retained.Push(popup);
        }
        while (retained.Count > 0) popups.Push(retained.Pop());
    }

    public bool IsTopPopupWithin(GameObject root)
    {
        return root != null && HasOpenPopups && popups.Peek().Root.transform.IsChildOf(root.transform);
    }

    public void CloseTopPopup()
    {
        if (!HasOpenPopups) return;
        // 먼저 꺼내야 닫기 콜백이 등록 해제를 요청해도 같은 항목을 다시 닫지 않는다.
        PopupEntry popup = popups.Pop();
        popup.Close();
    }

    private void CloseAllPopups()
    {
        while (HasOpenPopups) CloseTopPopup();
    }

    // UI 스택에서 이미 닫힌 팝업을 제거한다.
    private void PruneClosedPopups()
    {
        while (popups.Count > 0 && (popups.Peek().Root == null || !popups.Peek().Root.activeInHierarchy))
            popups.Pop();
    }

    // 씬 전환 시 모든 팝업을 닫는다.
    private void OnActiveSceneChanged(Scene previous, Scene current) => CloseAllPopups();
}
