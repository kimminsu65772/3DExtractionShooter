using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 작은 우클릭 메뉴만 생성한다. 인벤토리 전체 레이아웃이나 게임 규칙은 변경하지 않는다.
public class InventoryDropContextMenu : MonoBehaviour, IPointerClickHandler
{
    private RectTransform panel;
    private UnityAction dismiss;

    public static InventoryDropContextMenu Create(Canvas canvas, Font font, UnityAction drop, UnityAction close)
    {
        GameObject overlay = new GameObject("Item Drop Context Menu", typeof(RectTransform), typeof(Image), typeof(InventoryDropContextMenu));
        RectTransform rect = (RectTransform)overlay.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = Color.clear;
        InventoryDropContextMenu menu = overlay.GetComponent<InventoryDropContextMenu>();
        menu.dismiss = close;
        GameObject action = new GameObject("Drop", typeof(RectTransform), typeof(Image), typeof(Button));
        menu.panel = (RectTransform)action.transform;
        menu.panel.SetParent(rect, false);
        menu.panel.anchorMin = menu.panel.anchorMax = new Vector2(0.5f, 0.5f);
        menu.panel.pivot = new Vector2(0f, 1f);
        menu.panel.sizeDelta = new Vector2(170f, 44f);
        action.GetComponent<Image>().color = new Color(0.08f, 0.13f, 0.18f, 0.98f);
        action.GetComponent<Button>().onClick.AddListener(drop);
        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(Text));
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.SetParent(menu.panel, false); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        Text text = label.GetComponent<Text>(); text.font = font; text.text = "버리기 (X)"; text.fontSize = 18; text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        overlay.SetActive(false);
        return menu;
    }

    public void Show(Vector2 screenPosition, Camera eventCamera)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RectTransform bounds = (RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(bounds, screenPosition, eventCamera, out Vector2 local);
        local.x = Mathf.Clamp(local.x, bounds.rect.xMin, bounds.rect.xMax - panel.rect.width);
        local.y = Mathf.Clamp(local.y, bounds.rect.yMin + panel.rect.height, bounds.rect.yMax);
        panel.anchoredPosition = local;
    }

    public void Hide() => gameObject.SetActive(false);
    public void OnPointerClick(PointerEventData eventData) => dismiss?.Invoke();
}
