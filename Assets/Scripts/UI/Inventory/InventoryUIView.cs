using TMPro;
using UnityEngine;

// UI-01: 편집한 화면 참조만 보관한다. 배치/크기/글꼴은 프리팹에서 결정한다.
public sealed class InventoryUIView : MonoBehaviour
{
    [Header("화면")]
    public Canvas canvas;
    public GameObject windows, lootPanel, detailPanel;
    public RectTransform bagGrid, equipmentGrid, lootGrid;
    public InventoryHotbarView hotbar;
    public InventorySlotView slotTemplate;
    public InventoryDropContextMenu dropMenu;
    public AmmoSelectionView[] ammoSelectors;
    public UnityEngine.EventSystems.EventSystem fallbackEventSystem;
    [Header("데이터 표시")]
    public TMP_Text bagTitle, lootTitle, weightText, detailText;
    public UnityEngine.UI.Image weightFill, detailIcon;
    [Header("드래그 표시")]
    public RectTransform dragGhost;
    public TMP_Text dragText;
    public UnityEngine.UI.Image dragIcon;
    private InventoryUIController owner;

    public bool Bind(InventoryUIController controller)
    {
        if (controller == null || canvas == null || windows == null || lootPanel == null || detailPanel == null ||
            bagGrid == null || equipmentGrid == null || lootGrid == null || hotbar == null ||
            hotbar.weaponGrid == null || hotbar.itemGrid == null || !ValidSlots(hotbar.weaponSlots) || !ValidSlots(hotbar.itemSlots) ||
            slotTemplate == null || dropMenu == null || detailIcon == null || dragGhost == null ||
            weightText == null || weightFill == null)
        {
            Debug.LogError("InventoryUIView의 프리팹 참조가 누락됐습니다.", this);
            return false;
        }
        owner = controller;
        if (ammoSelectors == null || ammoSelectors.Length != hotbar.weaponSlots.Length)
            Debug.LogWarning("Ammo Selectors 배열을 무기 슬롯 순서/개수에 맞게 연결하세요. 누락된 슬롯의 탄약 UI는 숨깁니다.", this);
        owner.ConfigureView(canvas, windows, lootPanel, detailPanel, bagGrid, equipmentGrid, lootGrid,
            hotbar.weaponGrid, null, null, null, detailIcon, null, dragGhost, null, dragIcon);
        owner.SetSlotTemplate(slotTemplate);
        owner.ConfigureAmmoSelector(ammoSelectors);
        owner.ConfigureReferenceLabels(bagTitle, lootTitle, weightText, weightFill, detailText, dragText,
            hotbar.itemGrid, hotbar.weaponSlots, hotbar.itemSlots);
        return true;
    }

    private static bool ValidSlots(InventorySlotView[] slots)
    {
        if (slots == null || slots.Length == 0) return false;
        foreach (InventorySlotView slot in slots) if (slot == null) return false;
        return true;
    }

    public void CloseInventory() => owner?.Close();
}
