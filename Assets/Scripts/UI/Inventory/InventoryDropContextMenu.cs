using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryDropContextMenu : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private Button dropButton;
    [SerializeField] private Button unloadButton;
    private UnityAction dismiss;

    public void Initialize(UnityAction drop, UnityAction close, UnityAction unload = null)
    {
        dismiss = close;
        if (dropButton == null) return;
        dropButton.onClick.RemoveListener(HandleDrop);
        dropAction = drop;
        dropButton.onClick.AddListener(HandleDrop);
        unloadAction = unload;
        if (unloadButton != null)
        {
            unloadButton.onClick.RemoveListener(HandleUnload);
            unloadButton.onClick.AddListener(HandleUnload);
        }
    }

    private UnityAction dropAction;
    private UnityAction unloadAction;
    private void HandleDrop() => dropAction?.Invoke();
    private void HandleUnload() => unloadAction?.Invoke();

    public void SetWeaponActions(bool isWeapon, bool canUnload)
    {
        if (unloadButton == null) return;
        unloadButton.gameObject.SetActive(isWeapon);
        unloadButton.interactable = canUnload;
    }

    public void Show(Vector2 screenPosition, Camera eventCamera)
    {
        gameObject.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        // 요소를 항상 최상위로 이동시켜 다른 UI 요소 위에 표시되도록 함.
        transform.SetAsLastSibling();
        RectTransform bounds = (RectTransform)transform;
        // 마우스 위치를 UI 로컬 좌표로 변환한다.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(bounds, screenPosition, eventCamera, out Vector2 local);

        // 툴팁 팝업이 화면 밖으로 나가지 않도록 위치를 조정한다.
        local.x = Mathf.Clamp(local.x, bounds.rect.xMin, bounds.rect.xMax - panel.rect.width);
        local.y = Mathf.Clamp(local.y, bounds.rect.yMin + panel.rect.height, bounds.rect.yMax);
        panel.anchoredPosition = local;
    }

    public void Hide() => gameObject.SetActive(false);
    public void OnPointerClick(PointerEventData eventData) => dismiss?.Invoke();
}
