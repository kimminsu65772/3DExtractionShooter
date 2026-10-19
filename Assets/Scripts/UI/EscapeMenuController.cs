using UnityEngine;

// UI-01: 입력과 스택은 UIManager에 맡기고 메뉴 외형의 생성/표시만 담당한다.
[DisallowMultipleComponent]
public sealed class EscapeMenuController : MonoBehaviour
{
    [SerializeField] private GameObject menuPrefab;
    private GameObject menuInstance;

    public void Open()
    {
        if (UIManager.IsLoading()) return;
        if (menuPrefab == null)
        {
            Debug.LogError("EscapeMenuController에 표시할 메뉴 프리팹이 없습니다.", this);
            return;
        }

        if (menuInstance == null)
        {
            menuInstance = Instantiate(menuPrefab, transform, false);
            if (menuInstance.TryGetComponent(out PauseMenuController controller))
                controller.Initialize(Close);
        }

        menuInstance.SetActive(true);
        UIManager.Instance?.RegisterPopup(menuInstance, Close);
    }

    public void Close()
    {
        if (menuInstance == null) return;
        UIManager.Instance?.UnregisterPopup(menuInstance);
        menuInstance.SetActive(false);
    }

    private void OnDisable() => Close();
}
