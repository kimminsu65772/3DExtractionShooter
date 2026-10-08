using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum InventoryUIArea { Bag, Equipment, Loot, WeaponQuickslot, ItemQuickslot }

// 한 칸의 그림과 입력만 담당한다. 실제 소유권과 수량은 기존 모델에 남겨 둔다.
public class InventorySlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private Text itemName;
    [SerializeField] private Text countText;
    [SerializeField] private Text keyText;
    private InventoryUIController owner;
    private IInventoryItem shownItem;
    private int shownCount = -1;
    private int shownAmmoId = -1;
    private int shownReserve = -1;
    private float shownUseRatio = -1f;
    private Sprite shownIcon;
    private bool shownSelected;
    private bool initialized;

    public InventoryUIArea Area { get; private set; }
    public int Index { get; private set; }

    public void Configure(Image panel, Image itemIcon, Text label, Text count, Text key)
    {
        background = panel;
        icon = itemIcon;
        itemName = label;
        countText = count;
        keyText = key;
    }

    public void Bind(InventoryUIController controller, InventoryUIArea area, int index)
    {
        owner = controller;
        Area = area;
        Index = index;
        initialized = false;
        // 배열의 위치 대신 해당 칸에 대응하는 키 번호를 표시한다.
        keyText.text = area == InventoryUIArea.WeaponQuickslot ? (index + 1).ToString() : area == InventoryUIArea.ItemQuickslot ? (index + 3).ToString() : string.Empty;
    }

    public void Refresh()
    {
        if (owner == null) return;
        IInventoryItem item = owner.GetItem(Area, Index);
        // 장탄 수와 예비 탄약은 퀵슬롯에서만 조회하고 표시한다.
        WeaponAmmoState magazine = Area == InventoryUIArea.WeaponQuickslot ? (item as WeaponInventoryItem)?.GetWeapon().AmmoState : null;
        int count = item is DefinitionInventoryItem defined ? defined.Count : item is AmmoStack ammo ? ammo.GetCurrentCount() : magazine?.LoadedAmmoCount ?? 0;
        float useRatio = item is MedicineInventoryItem kit ? kit.RemainingUseRatio : item is ArmorInventoryItem protection ? protection.Armor.DurabilityRatio : -1f;
        int ammoId = magazine != null ? owner.GetDisplayAmmoId(magazine) : 0;
        int reserve = Area == InventoryUIArea.WeaponQuickslot ? owner.GetReserve(ammoId) : 0;
        Sprite sprite = owner.GetIcon(item);
        bool selected = (Area == InventoryUIArea.Equipment || Area == InventoryUIArea.WeaponQuickslot) && owner.SelectedWeaponIndex == Index;
        if (Area == InventoryUIArea.ItemQuickslot) selected = owner.SelectedItemIndex == Index;
        // 참조와 숫자만 비교한다. 변화가 없는 프레임에는 문자열과 UI를 다시 만들지 않는다.
        if (initialized && ReferenceEquals(item, shownItem) && count == shownCount && useRatio == shownUseRatio && ammoId == shownAmmoId && reserve == shownReserve && sprite == shownIcon && selected == shownSelected) return;
        initialized = true;
        shownItem = item;
        shownCount = count;
        shownUseRatio = useRatio;
        shownAmmoId = ammoId;
        shownReserve = reserve;
        shownIcon = sprite;
        shownSelected = selected;
        itemName.text = owner.GetItemName(item);
        if (item == null && Area == InventoryUIArea.Equipment)
            itemName.text = Index == 2 ? "방어구" : Index == 3 ? "헬멧" : "무기";
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        // 가방·장비·전리품의 무기에는 탄창 숫자를 표시하지 않고 일반 아이템의 수량은 유지한다.
        if (item == null || (item is WeaponInventoryItem && Area != InventoryUIArea.WeaponQuickslot)) countText.text = string.Empty;
        else if (magazine != null) countText.text = $"{count} / {reserve}";
        // 키트는 물건 개수 대신 해당 키트에 남은 사용량을 표시한다.
        else if (item is MedicineInventoryItem) countText.text = $"{useRatio * 100f:0.##}%";
        // 보호 장비는 개수 대신 해당 객체의 남은 내구도를 표시한다.
        else if (item is ArmorInventoryItem) countText.text = $"{useRatio * 100f:0.##}%";
        else countText.text = count.ToString();
        background.color = selected ? new Color(0.2f, 0.48f, 0.5f, 0.95f) : new Color(0.12f, 0.2f, 0.26f, 0.94f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && owner != null) owner.ClickSlot(this, eventData.clickCount >= 2);
        else if (eventData.button == PointerEventData.InputButton.Right && owner != null) owner.ShowDropContextMenu(this, eventData.position);
    }
    public void OnPointerEnter(PointerEventData eventData) { if (owner != null) owner.SetHoveredSlot(this); }
    public void OnPointerExit(PointerEventData eventData) { if (owner != null) owner.ClearHoveredSlot(this); }
    private void OnDisable() { if (owner != null) owner.ClearHoveredSlot(this); }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && owner != null) owner.BeginDrag(this, eventData.position);
    }
    public void OnDrag(PointerEventData eventData) { if (owner != null) owner.MoveDrag(eventData.position); }
    public void OnEndDrag(PointerEventData eventData) { if (owner != null) owner.CancelDrag(); }
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && owner != null) owner.DropOn(this);
    }
}
