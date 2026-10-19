using System;
using System.Collections.Generic;
using ExtractionShooter.DataPipeline;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class InventoryIconOverride
{
    public ItemType type;
    public int itemId;
    public Sprite icon;
    [TextArea] public string description;
}

// 창의 수명과 입력을 관리하고, 기존 가방·장비 API에 UI 요청을 전달한다.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public partial class InventoryUIController : MonoBehaviour
{
    [Header("플레이어 참조")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerItemQuickslots itemQuickslots;
    [SerializeField] private PlayerHeldItemController heldItem;
    [SerializeField] private PlayerInputHandler input;
    [SerializeField] private PlayerInteractionController interaction;
    [SerializeField] private PlayerAttackController attack;
    [SerializeField] private PlayerWeaponHolder holder;
    [SerializeField] private HealthComponent health;
    [SerializeField] private PlayerCarryWeight carryWeight;
    [SerializeField] private CombatDataCompositionRoot dataRoot;
    [Header("커서 전환")]
    [SerializeField] private PlayerAimCursor aimCursor;
    [SerializeField] private GameObject crosshairRoot;
    [Header("표시 설정")]
    [SerializeField] private Key inventoryKey = Key.Tab;
    [SerializeField] private Font uiFont;
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite slotSprite;
    [SerializeField] private InventoryIconOverride[] itemVisuals = Array.Empty<InventoryIconOverride>();
    [Header("편집 가능한 UI 참조")]
    [SerializeField] private InventoryUIView viewPrefab;
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private GameObject windows;
    [SerializeField] private GameObject lootPanel;
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private RectTransform bagGrid;
    [SerializeField] private RectTransform equipmentGrid;
    [SerializeField] private RectTransform lootGrid;
    [SerializeField] private RectTransform quickslotGrid;
    [SerializeField] private RectTransform itemQuickslotGrid;
    [SerializeField] private Text bagTitle;
    [SerializeField] private Text lootTitle;
    [SerializeField] private Text weightText;
    [SerializeField] private UnityEngine.UI.Image weightFill;
    [SerializeField] private Text detailText;
    [SerializeField] private Image detailIcon;
    // 이전 프리팹의 하단 안내 참조는 직렬화 호환을 위해 보존하고 초기화 때 비운다.
    [SerializeField, HideInInspector] private Text prompt;
    [SerializeField] private RectTransform dragGhost;
    [SerializeField] private Text dragText;
    [SerializeField] private Image dragIcon;
    [SerializeField] private InventorySlotView slotTemplate;
    private TMP_Text referenceBagTitle, referenceLootTitle, referenceWeight, referenceDetail, referenceDrag;
    private bool usesReferenceLayout;

    private readonly List<InventorySlotView> bagViews = new();
    private readonly List<InventorySlotView> equipmentViews = new();
    private readonly List<InventorySlotView> lootViews = new();
    private readonly List<InventorySlotView> quickViews = new();
    private readonly List<InventorySlotView> itemQuickViews = new();
    private readonly InventoryIconCache icons = new();
    private LootContainer openedLoot;
    private Collider[] openedLootColliders;
    private IInventoryItem selectedItem;
    private IInventoryItem dragItem;
    private InventoryUIArea dragArea;
    private int dragIndex;
    private InventoryContainer dragContainer;
    private int previousBagUsed = -1;
    private int previousLootUsed = -1;
    private float previousWeight = -1f;
    private float previousMaximumWeight = -1f;
    private int previousDetailCount = -1;
    private float previousDetailUseRatio = -1f;
    private IInventoryItem previousDetailItem;
    private Sprite previousDetailIcon;
    private bool subscribed;
    private InventoryUIView boundView;
    private bool isCursorPresentationOpen;
    private bool crosshairWasActive;
    private InventorySlotView hoveredSlot;
    private InventoryDropContextMenu dropMenu;
    private InventoryUIArea dropMenuArea;
    private int dropMenuIndex;
    private IInventoryItem dropMenuItem;

    public bool IsOpen => windows != null && windows.activeSelf;
    // [DATA-01 변경] 로딩·결과 저장·실패 재시도 중에는 UI의 직접 입력도 소유 상태를 바꾸지 못한다.
    private bool IsGameplayBlocked => input != null && input.IsGameplayInputBlocked;
    // 소모품을 선택한 동안에는 마지막 무기 칸을 현재 선택처럼 표시하지 않는다.
    public int SelectedWeaponIndex => heldItem != null && heldItem.SelectedItem != null ? -1 : equipment != null ? equipment.SelectedWeaponSlotIndex : -1;
    public int SelectedItemIndex => heldItem != null && heldItem.SelectedItem != null && ReferenceEquals(itemQuickslots?.Quickslots.GetItem(heldItem.SelectedItemSlotIndex), heldItem.SelectedItem) ? heldItem.SelectedItemSlotIndex : -1;
    public Font UIFont => uiFont;
    public Sprite PanelSprite => panelSprite;
    public Sprite SlotSprite => slotSprite;
    public InventorySlotView SlotTemplate => slotTemplate;
    public void SetSlotTemplate(InventorySlotView template) { slotTemplate = template; }

    public void SetHoveredSlot(InventorySlotView slot) { hoveredSlot = slot; }
    public void ClearHoveredSlot(InventorySlotView slot) { if (hoveredSlot == slot) hoveredSlot = null; }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        // Awake 순서에 의존하지 않고 가방과 장비가 만들어진 뒤 표시를 준비한다.
        BuildView();
        if (uiCanvas == null) { enabled = false; return; }
        EnsureEventSystem();
        // 상호작용 안내는 물체 위치를 따라가는 독립 HUD로 옮겼다.
        SetLabel(prompt, null, string.Empty);
        SetOpen(false);
        Subscribe();
    }

    private void OnEnable() { Subscribe(); }
    private void OnDisable()
    {
        if (subscribed && interaction != null) interaction.OnInteracted -= HandleInteracted;
        subscribed = false;
        Close();
        if (uiCanvas != null) uiCanvas.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        CloseAmmoSelection();
        if (inventory != null) inventory.OnOwnedItemsChanged -= InvalidateAmmoChoices;
        SetCursorPresentation(false);
        icons.Release();
        if (uiCanvas != null) Destroy(uiCanvas.gameObject);
    }

    private void Subscribe()
    {
        if (uiCanvas != null) uiCanvas.gameObject.SetActive(true);
        if (subscribed || interaction == null) return;
        interaction.OnInteracted += HandleInteracted;
        subscribed = true;
    }

    private void Update()
    {
        if (uiCanvas == null) return;
        if (IsGameplayBlocked)
        {
            Close();
            if (ammoSelectors != null)
            {
                foreach (var selector in ammoSelectors)
                {
                    if (selector != null) selector.gameObject.SetActive(false);
                }
            }
            return;
        }
        Keyboard keyboard = Keyboard.current;
        UpdateAmmoSelection(keyboard);
        // 닫힌 HUD의 무기 칸을 누를 때도 선택 클릭이 총 발사로 새지 않게 한다.
        bool overHotbar = Mouse.current != null &&
            (RectTransformUtility.RectangleContainsScreenPoint(usesReferenceLayout ? quickslotGrid : (RectTransform)quickslotGrid.parent, Mouse.current.position.ReadValue(), null) ||
             (itemQuickslotGrid != null && RectTransformUtility.RectangleContainsScreenPoint(itemQuickslotGrid, Mouse.current.position.ReadValue(), null)));
        bool hasPopup = UIManager.Instance != null && UIManager.Instance.HasOpenPopups;
        input?.SetUIInputBlocked(IsOpen || hasPopup || overHotbar);
        SetCursorPresentation(IsOpen || hasPopup);
        if (health != null && health.IsDead) { Close(); return; }
        if (keyboard == null) return;
        // 다른 Canvas의 메뉴가 최상위면 뒤에 있는 인벤토리의 키 입력은 처리하지 않는다.
        if (hasPopup && !UIManager.Instance.IsTopPopupWithin(uiCanvas.gameObject)) return;
        if (keyboard[inventoryKey].wasPressedThisFrame) SetOpen(!IsOpen);
        if (IsOpen && keyboard.xKey.wasPressedThisFrame)
        {
            if (dropMenuItem != null) DropContextItem();
            else DropHoveredItem();
        }
        // 창 안에서 누른 숫자키는 무기 사용 입력으로 전달하지 않는다.
        if (!IsOpen)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) TrySelectWeapon(0);
            if (keyboard.digit2Key.wasPressedThisFrame) TrySelectWeapon(1);
            // 화면의 3~8번을 소모품 배열의 0~5번으로 연결한다.
            if (keyboard.digit3Key.wasPressedThisFrame) heldItem?.TrySelectItemSlot(0);
            if (keyboard.digit4Key.wasPressedThisFrame) heldItem?.TrySelectItemSlot(1);
            if (keyboard.digit5Key.wasPressedThisFrame) heldItem?.TrySelectItemSlot(2);
            if (keyboard.digit6Key.wasPressedThisFrame) heldItem?.TrySelectItemSlot(3);
            if (keyboard.digit7Key.wasPressedThisFrame) heldItem?.TrySelectItemSlot(4);
            if (keyboard.digit8Key.wasPressedThisFrame) heldItem?.TrySelectItemSlot(5);
        }
    }

    private void TrySelectWeapon(int index)
    {
        if (IsGameplayBlocked) return;
        // 무기 선택이 실패하면 기존 소모품 선택과 사용을 유지한다.
        if (equipment != null && equipment.TrySelectWeaponSlot(index)) heldItem?.ClearItemSelection();
    }

    private void LateUpdate()
    {
        if (uiCanvas == null) return;
        // 상자에서 멀어지면 가방까지 닫고 드래그 취소와 커서 복원을 함께 처리한다.
        if (!ValidateLootRange()) Close();
        RefreshViews();
    }

    private void HandleInteracted(IInteractable target)
    {
        if (target is LootInteractable loot) OpenLoot(loot.LootContainer);
    }

}
