using System;
using System.Collections.Generic;
using ExtractionShooter.DataPipeline;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

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
public class InventoryUIController : MonoBehaviour
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
    [SerializeField] private Text detailText;
    [SerializeField] private Image detailIcon;
    [SerializeField] private Text prompt;
    [SerializeField] private RectTransform dragGhost;
    [SerializeField] private Text dragText;
    [SerializeField] private Image dragIcon;
    [SerializeField] private InventorySlotView slotTemplate;

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
    private string previousPrompt;
    private IInteractable previousTarget;
    private int previousBagUsed = -1;
    private int previousLootUsed = -1;
    private int previousDetailCount = -1;
    private float previousDetailUseRatio = -1f;
    private IInventoryItem previousDetailItem;
    private Sprite previousDetailIcon;
    private bool subscribed;
    private bool ownsUIFont;
    private bool isCursorPresentationOpen;
    private bool crosshairWasActive;
    private InventorySlotView hoveredSlot;
    private InventoryDropContextMenu dropMenu;
    private InventoryUIArea dropMenuArea;
    private int dropMenuIndex;
    private IInventoryItem dropMenuItem;

    public bool IsOpen => windows != null && windows.activeSelf;
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

    private bool CanDropFrom(InventoryUIArea area) => area != InventoryUIArea.Loot;

    public void ShowDropContextMenu(InventorySlotView slot, Vector2 position)
    {
        if (!IsOpen || slot == null || dragItem != null || !CanDropFrom(slot.Area)) return;
        IInventoryItem item = GetItem(slot.Area, slot.Index);
        if (item == null) return;
        dropMenuArea = slot.Area; dropMenuIndex = slot.Index; dropMenuItem = item;
        if (dropMenu == null) dropMenu = InventoryDropContextMenu.Create(uiCanvas, uiFont, DropContextItem, CloseDropMenu);
        dropMenu.Show(position, uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCanvas.worldCamera);
    }

    private void CloseDropMenu()
    {
        dropMenuItem = null;
        if (dropMenu != null) dropMenu.Hide();
    }

    private void DropContextItem()
    {
        IInventoryItem item = dropMenuItem;
        InventoryUIArea area = dropMenuArea; int index = dropMenuIndex;
        CloseDropMenu();
        // 메모한 물건이 교체되었으면 현재 칸의 다른 물건을 버리지 않는다.
        if (IsOpen && item != null && ReferenceEquals(GetItem(area, index), item)) DropOwnedItem(item);
    }

    private void DropHoveredItem()
    {
        if (!IsOpen || hoveredSlot == null || dragItem != null || !CanDropFrom(hoveredSlot.Area)) return;
        IInventoryItem item = GetItem(hoveredSlot.Area, hoveredSlot.Index);
        if (item != null) DropOwnedItem(item);
    }

    // 기존 외부 호출과의 호환 경계다. 실제 입력은 커서 아래 슬롯과 우클릭 메뉴에서 전달한다.
    public void DropSelectedItem()
    {
        if (!IsOpen || selectedItem == null) return;
        DropOwnedItem(selectedItem);
    }

    private void DropOwnedItem(IInventoryItem item)
    {
        bool dropped = false;
        InventoryContainer bag = inventory?.InventoryContainer;
        if (bag != null)
            for (int i = 0; i < bag.SlotCount; i++)
                if (ReferenceEquals(bag.GetSlot(i).Item, item)) { dropped = inventory.TryDropBagItem(i); break; }
        if (!dropped && equipment?.WeaponEquipment != null)
            for (int i = 0; i < equipment.WeaponEquipment.SlotCount; i++)
                if (ReferenceEquals(equipment.WeaponEquipment.GetItem(i), item)) { dropped = equipment.TryDropWeapon(i); break; }
        if (!dropped && item is ArmorInventoryItem protection && ReferenceEquals(equipment?.ProtectionEquipment.GetItem(protection.Type), item)) dropped = equipment.TryDropProtection(protection.Type);
        if (!dropped) return;
        selectedItem = null;
        CloseDropMenu();
        CancelDrag();
        RefreshQuickslotRegistrations();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        // Awake 순서에 의존하지 않고 가방과 장비가 만들어진 뒤 표시를 준비한다.
        BuildView();
        // 편집 중 생성한 OS 폰트가 씬 파일에 저장되지 않았을 때 런타임 폰트를 다시 연결한다.
        foreach (Text label in uiCanvas.GetComponentsInChildren<Text>(true))
            if (label.font == null) label.font = uiFont;
        EnsureEventSystem();
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
        SetCursorPresentation(false);
        icons.Release();
        if (uiCanvas != null) Destroy(uiCanvas.gameObject);
        if (ownsUIFont && uiFont != null) Destroy(uiFont);
    }

    private void Subscribe()
    {
        if (uiCanvas != null) uiCanvas.gameObject.SetActive(true);
        if (subscribed || interaction == null) return;
        interaction.OnInteracted += HandleInteracted;
        subscribed = true;
    }

    private void ResolveReferences()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
        if (itemQuickslots == null) itemQuickslots = GetComponent<PlayerItemQuickslots>();
        if (heldItem == null) heldItem = GetComponent<PlayerHeldItemController>();
        if (input == null) input = GetComponent<PlayerInputHandler>();
        if (interaction == null) interaction = GetComponent<PlayerInteractionController>();
        if (attack == null) attack = GetComponent<PlayerAttackController>();
        if (holder == null) holder = GetComponent<PlayerWeaponHolder>();
        if (health == null) health = GetComponent<HealthComponent>();
        if (aimCursor == null) aimCursor = GetComponent<PlayerAimCursor>();
        if (crosshairRoot == null)
        {
            // 기본 싱글플레이 씬에서는 조준점만 찾는다. 다른 HUD가 든 Canvas는 숨기지 않는다.
            Crosshair crosshair = UnityEngine.Object.FindFirstObjectByType<Crosshair>(FindObjectsInactive.Include);
            if (crosshair != null) crosshairRoot = crosshair.gameObject;
        }
        if (dataRoot == null && holder != null) dataRoot = holder.DataRoot;
        if (uiFont == null)
        {
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 18);
            ownsUIFont = uiFont != null;
            if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    [ContextMenu("UI/기본 레이아웃 생성")]
    public void BuildView()
    {
        ResolveReferences();
        // 이미 편집한 화면이 있으면 다시 생성하거나 덮어쓰지 않는다.
        if (uiCanvas == null) InventoryUILayout.Build(this);
        // 저장된 기존 UI에서도 전체 화면을 다시 만들지 않고 소모품 칸만 준비한다.
        if (itemQuickslotGrid == null && quickslotGrid != null) itemQuickslotGrid = InventoryUILayout.EnsureItemQuickslotGrid((RectTransform)quickslotGrid.parent);
        // 이전 기본 레이아웃의 한 줄을 네 장비 칸으로 확장한다. 사용자 편집 레이아웃은 건드리지 않는다.
        GridLayoutGroup gearLayout = equipmentGrid != null ? equipmentGrid.GetComponent<GridLayoutGroup>() : null;
        if (gearLayout != null && equipmentGrid.name == "Equipment Slots" && gearLayout.constraintCount == 2 && equipmentGrid.sizeDelta == new Vector2(378f, 96f)) gearLayout.constraintCount = 4;
    }

    public void ConfigureView(Canvas canvas, GameObject windowRoot, GameObject loot, GameObject detail, RectTransform bag, RectTransform gear, RectTransform lootSlots, RectTransform quick, Text bagLabel, Text lootLabel, Text detailLabel, Image infoIcon, Text interactionLabel, RectTransform ghost, Text ghostLabel, Image ghostIcon)
    {
        uiCanvas = canvas;
        windows = windowRoot;
        lootPanel = loot;
        detailPanel = detail;
        bagGrid = bag;
        equipmentGrid = gear;
        lootGrid = lootSlots;
        quickslotGrid = quick;
        bagTitle = bagLabel;
        lootTitle = lootLabel;
        detailText = detailLabel;
        detailIcon = infoIcon;
        prompt = interactionLabel;
        dragGhost = ghost;
        dragText = ghostLabel;
        dragIcon = ghostIcon;
    }

    private void Update()
    {
        if (uiCanvas == null) return;
        // 닫힌 HUD의 무기 칸을 누를 때도 선택 클릭이 총 발사로 새지 않게 한다.
        bool overHotbar = Mouse.current != null && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)quickslotGrid.parent, Mouse.current.position.ReadValue(), null);
        input?.SetUIInputBlocked(IsOpen || overHotbar);
        if (health != null && health.IsDead) { Close(); return; }
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard[inventoryKey].wasPressedThisFrame) SetOpen(!IsOpen);
        if (keyboard.escapeKey.wasPressedThisFrame && IsOpen) Close();
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
        // 무기 선택이 실패하면 기존 소모품 선택과 사용을 유지한다.
        if (equipment != null && equipment.TrySelectWeaponSlot(index)) heldItem?.ClearItemSelection();
    }

    private void LateUpdate()
    {
        if (uiCanvas == null) return;
        // 상자에서 멀어지면 가방까지 닫고 드래그 취소와 커서 복원을 함께 처리한다.
        if (!ValidateLootRange()) Close();
        RefreshViews();
        IInteractable target = interaction != null ? interaction.CurrentTarget : null;
        if (!ReferenceEquals(previousTarget, target))
        {
            previousTarget = target;
            previousPrompt = target != null ? $"F  {target.InteractionLabel}" : string.Empty;
            prompt.text = previousPrompt;
        }
    }

    private void HandleInteracted(IInteractable target)
    {
        if (target is LootInteractable loot) OpenLoot(loot.LootContainer);
    }

    public void OpenLoot(LootContainer loot)
    {
        if (loot == null || !loot.TryInitializeContents(dataRoot)) return;
        CancelDrag();
        openedLoot = loot;
        openedLootColliders = loot.GetComponentsInChildren<Collider>();
        previousLootUsed = -1;
        selectedItem = null;
        SetOpen(true);
        lootPanel.SetActive(true);
        RefreshViews();
    }

    public void Close() { SetOpen(false); }
    private void SetOpen(bool open)
    {
        if (!open) { hoveredSlot = null; CloseDropMenu(); }
        // 화면이 이미 파괴된 종료 과정에서도 커서 복원 요청은 처리한다.
        SetCursorPresentation(open && windows != null);
        if (windows == null) return;
        windows.SetActive(open);
        input?.SetUIInputBlocked(open);
        if (open) attack?.CancelPendingFire();
        if (!open)
        {
            CloseLoot();
            selectedItem = null;
            detailPanel.SetActive(false);
            CancelDrag();
        }
    }

    private void SetCursorPresentation(bool open)
    {
        if (isCursorPresentationOpen == open) return;
        isCursorPresentationOpen = open;
        aimCursor?.SetUICursorActive(open);
        if (crosshairRoot == null) return;
        if (open)
        {
            // 원래 숨겨져 있던 조준점을 창을 닫을 때 임의로 켜지 않는다.
            crosshairWasActive = crosshairRoot.activeSelf;
            crosshairRoot.SetActive(false);
        }
        else crosshairRoot.SetActive(crosshairWasActive);
    }

    private void CloseLoot()
    {
        // 원본 데이터는 드래그 시작 때 꺼내지 않았으므로 취소할 때 복구 작업 자체가 필요 없다.
        CancelDrag();
        openedLoot = null;
        openedLootColliders = null;
        if (lootPanel != null) lootPanel.SetActive(false);
        if (selectedItem != null && !PlayerOwns(selectedItem)) selectedItem = null;
    }

    private bool ValidateLootRange()
    {
        if (ReferenceEquals(openedLoot, null)) return true;
        if (openedLoot == null || !openedLoot.isActiveAndEnabled || interaction == null || !interaction.isActiveAndEnabled || openedLootColliders == null) return false;
        // 열어 둔 상자는 바라보는 방향이 바뀌어도 유지하고, 실제 표면까지의 거리가 멀어지면 닫는다.
        foreach (Collider collider in openedLootColliders)
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy && interaction.IsWithinInteractionDistance(collider)) return true;
        return false;
    }

    private InventoryContainer GetContainer(InventoryUIArea area)
    {
        return area == InventoryUIArea.Bag ? inventory?.InventoryContainer : area == InventoryUIArea.Loot && openedLoot != null ? openedLoot.InventoryContainer : null;
    }

    public IInventoryItem GetItem(InventoryUIArea area, int index)
    {
        // 퀵슬롯은 가방의 아이템을 참조하며 별도 아이템을 생성하거나 소유하지 않는다.
        if (area == InventoryUIArea.ItemQuickslot)
            return itemQuickslots != null && index >= 0 && index < itemQuickslots.Quickslots.SlotCount ? itemQuickslots.Quickslots.GetItem(index) : null;
        if (area == InventoryUIArea.Equipment && GetProtectionSlotType(index) != ItemType.None)
            return equipment?.ProtectionEquipment.GetItem(GetProtectionSlotType(index));
        if (area == InventoryUIArea.Equipment || area == InventoryUIArea.WeaponQuickslot)
            return equipment?.WeaponEquipment != null && index >= 0 && index < equipment.WeaponEquipment.SlotCount ? equipment.WeaponEquipment.GetItem(index) : null;
        InventoryContainer container = GetContainer(area);
        return container != null && index >= 0 && index < container.SlotCount ? container.GetSlot(index).Item : null;
    }

    public int GetReserve(int ammoId) => ammoId > 0 && inventory?.InventoryContainer != null ? inventory.InventoryContainer.GetAmmoCount(ammoId) : 0;

    public int GetDisplayAmmoId(WeaponAmmoState magazine) => InventoryHUDReadModel.GetDisplayAmmoId(magazine, inventory?.InventoryContainer, dataRoot?.AmmoCatalog);

    public string GetItemName(IInventoryItem item)
    {
        if (item is WeaponInventoryItem weapon) return weapon.GetWeapon().Definition.displayName;
        if (item is DefinitionInventoryItem defined) return defined.Definition.displayName;
        if (item is AmmoStack && dataRoot?.AmmoCatalog != null && dataRoot.AmmoCatalog.TryGet(item.ItemId, out AmmoDefinition ammo)) return ammo.displayName;
        return item == null ? string.Empty : $"{item.Type} #{item.ItemId}";
    }

    private InventoryIconOverride GetVisual(IInventoryItem item)
    {
        if (item == null || itemVisuals == null) return null;
        foreach (InventoryIconOverride visual in itemVisuals)
            if (visual != null && visual.type == item.Type && visual.itemId == item.ItemId) return visual;
        return null;
    }

    public Sprite GetIcon(IInventoryItem item)
    {
        if (item == null) return null;
        InventoryIconOverride visual = GetVisual(item);
        if (visual?.icon != null) return visual.icon;
        if (item is DefinitionInventoryItem defined) return icons.Get(defined.Definition.iconAddress);
        if (item is WeaponInventoryItem weapon) return icons.Get(weapon.GetWeapon().Definition.iconAddress);
        if (item is AmmoStack && dataRoot?.AmmoCatalog != null && dataRoot.AmmoCatalog.TryGet(item.ItemId, out AmmoDefinition ammo)) return icons.Get(ammo.iconAddress);
        return null;
    }

    public void ClickSlot(InventorySlotView view, bool doubleClick)
    {
        // 아이템 이동과 등록 요청이 끝난 시점에만 잘못된 참조를 정리한다.
        try { ClickSlotCore(view, doubleClick); }
        finally { RefreshQuickslotRegistrations(); }
    }

    private void RefreshQuickslotRegistrations()
    {
        itemQuickslots?.RemoveInvalidRegistrations();
        if (heldItem != null && heldItem.SelectedItem != null && !ReferenceEquals(itemQuickslots?.Quickslots.GetItem(heldItem.SelectedItemSlotIndex), heldItem.SelectedItem)) heldItem.ClearItemSelection();
    }

    private void ClickSlotCore(InventorySlotView view, bool doubleClick)
    {
        if (view.Area == InventoryUIArea.WeaponQuickslot)
        {
            // 창을 열어 둔 상태의 클릭도 UI의 무기 선택으로 처리한다. 발사 입력은 계속 차단한다.
            TrySelectWeapon(view.Index);
            if (IsOpen) selectedItem = GetItem(view.Area, view.Index);
            return;
        }
        if (view.Area == InventoryUIArea.ItemQuickslot)
        {
            // 칸을 누르는 클릭은 선택만 한다. 사용은 게임 화면에서 새로 누른 왼쪽 클릭으로 요청한다.
            heldItem?.TrySelectItemSlot(view.Index);
            if (IsOpen) selectedItem = GetItem(view.Area, view.Index);
            return;
        }
        if (!IsOpen || !ValidateLootRange()) return;
        selectedItem = GetItem(view.Area, view.Index);
        if (selectedItem == null) { detailPanel.SetActive(false); return; }
        if (!doubleClick) return;
        if (view.Area == InventoryUIArea.Loot)
        {
            // 빈 무기 장비 칸이 있으면 가방 공간을 요구하지 않고 자동 장착한다.
            if (TryAutoEquip(GetContainer(view.Area), view.Index)) return;
            // 일부만 합쳐진 경우도 획득 성공이다. 상자의 객체가 아니라 가방의 실제 스택을 등록한다.
            if (GetContainer(view.Area)?.TryTransferTo(inventory.InventoryContainer, view.Index, out _) == true)
                TryAutoRegisterAcquiredItem(selectedItem);
        }
        else if (view.Area == InventoryUIArea.Bag)
        {
            if (TryAutoEquip(inventory.InventoryContainer, view.Index)) return;
            if (openedLoot != null) inventory.InventoryContainer.TryTransferTo(openedLoot.InventoryContainer, view.Index, out _);
        }
        else if (view.Area == InventoryUIArea.Equipment && selectedItem is ArmorInventoryItem)
        {
            // 더블클릭 해제는 빈 가방 칸만 사용한다. 가득 찼을 때 임의의 물건을 바꾸지 않는다.
            for (int i = 0; i < inventory.InventoryContainer.SlotCount; i++)
                if (inventory.InventoryContainer.GetSlot(i).IsEmpty) { equipment.TryMoveProtectionToBag(GetProtectionSlotType(view.Index), i); break; }
        }
    }

    private bool TryAutoEquip(InventoryContainer source, int index)
    {
        if (equipment?.WeaponEquipment == null || source == null || index < 0 || index >= source.SlotCount) return false;
        IInventoryItem item = source.GetSlot(index).Item;
        if (item is ArmorInventoryItem protection)
            return equipment.ProtectionEquipment.GetItem(protection.Type) == null && equipment.TryMoveProtectionToEquipment(source, index, protection.Type);
        if (item is not WeaponInventoryItem) return false;
        for (int i = 0; i < equipment.WeaponEquipment.SlotCount; i++)
            if (equipment.WeaponEquipment.GetItem(i) == null) return equipment.TryMoveWeaponToEquipment(source, index, i);
        return false;
    }

    private void TryAutoRegisterAcquiredItem(IInventoryItem acquiredItem)
    {
        if (itemQuickslots == null || acquiredItem == null || inventory?.InventoryContainer == null) return;
        ItemType type = acquiredItem.Type;
        if (type != ItemType.Consumable && type != ItemType.Meds && type != ItemType.Buff && type != ItemType.Throwables) return;
        // 이전 획득·사용으로 남은 잘못된 참조를 먼저 지워 빈 칸과 중복 여부를 정확히 판단한다.
        itemQuickslots.RemoveInvalidRegistrations();
        InventoryContainer bag = inventory.InventoryContainer;
        for (int i = 0; i < bag.SlotCount; i++)
        {
            IInventoryItem candidate = bag.GetSlot(i).Item;
            if (candidate == null || candidate.Type != type || candidate.ItemId != acquiredItem.ItemId) continue;
            if (candidate is DefinitionInventoryItem defined && defined.Count <= 0) continue;
            // 합치기 전 출발 객체가 소진되어도 가방에 보관된 같은 종류·ID의 실제 객체를 찾는다.
            itemQuickslots.TryAutoRegister(candidate);
            return;
        }
    }

    public void BeginDrag(InventorySlotView source, Vector2 screenPosition)
    {
        CancelDrag();
        // 표시용 퀵슬롯을 가방의 실제 보관 칸처럼 드래그하지 않는다.
        if (!IsOpen || source.Area == InventoryUIArea.WeaponQuickslot || source.Area == InventoryUIArea.ItemQuickslot || !ValidateLootRange()) return;
        dragItem = GetItem(source.Area, source.Index);
        if (dragItem == null) return;
        dragArea = source.Area;
        dragIndex = source.Index;
        dragContainer = GetContainer(source.Area);
        dragText.text = GetItemName(dragItem);
        dragIcon.sprite = GetIcon(dragItem);
        dragIcon.enabled = dragIcon.sprite != null;
        dragGhost.gameObject.SetActive(true);
        dragGhost.SetAsLastSibling();
        MoveDrag(screenPosition);
    }

    public void MoveDrag(Vector2 screenPosition)
    {
        if (dragItem == null || dragGhost == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)uiCanvas.transform, screenPosition, null, out Vector2 local);
        dragGhost.anchoredPosition = local;
    }

    public void CancelDrag()
    {
        dragItem = null;
        dragContainer = null;
        if (dragGhost != null) dragGhost.gameObject.SetActive(false);
    }

    public void DropOn(InventorySlotView target)
    {
        // 이동·교환·등록 요청이 끝난 뒤 참조를 한 번 정리한다.
        try { DropOnCore(target); }
        finally { RefreshQuickslotRegistrations(); }
    }

    private void DropOnCore(InventorySlotView target)
    {
        if (!IsOpen || dragItem == null || !ValidateLootRange()) { CancelDrag(); return; }
        // 드래그 도중 장전이나 다른 입력으로 출발지 내용이 바뀌었다면 이전 물건을 이동시키지 않는다.
        if (!ReferenceEquals(GetItem(dragArea, dragIndex), dragItem) || !ReferenceEquals(GetContainer(dragArea), dragContainer)) { CancelDrag(); return; }
        if (target.Area == InventoryUIArea.ItemQuickslot)
        {
            // 퀵슬롯 등록은 소유권 이동이 아니다. 가방 아이템을 그대로 두고 참조만 연결한다.
            if (dragArea == InventoryUIArea.Bag && itemQuickslots != null)
            {
                itemQuickslots.RemoveInvalidRegistrations();
                itemQuickslots.TryRegister(target.Index, dragItem);
            }
            CancelDrag();
            return;
        }
        InventoryContainer destination = GetContainer(target.Area);
        bool targetGear = target.Area == InventoryUIArea.Equipment || target.Area == InventoryUIArea.WeaponQuickslot;
        if (dragArea == InventoryUIArea.Equipment && target.Area == InventoryUIArea.Bag)
        {
            ItemType sourceType = GetProtectionSlotType(dragIndex);
            if (sourceType != ItemType.None) equipment.TryMoveProtectionToBag(sourceType, target.Index);
            else equipment.TryMoveEquipmentWeaponToBag(dragIndex, target.Index);
        }
        else if (dragContainer != null && targetGear)
        {
            ItemType targetType = target.Area == InventoryUIArea.Equipment ? GetProtectionSlotType(target.Index) : ItemType.None;
            if (targetType != ItemType.None && dragItem is ArmorInventoryItem) equipment.TryMoveProtectionToEquipment(dragContainer, dragIndex, targetType);
            else if (targetType == ItemType.None && dragItem is WeaponInventoryItem) equipment.TryMoveWeaponToEquipment(dragContainer, dragIndex, target.Index);
        }
        else if (dragContainer != null && destination != null)
        {
            bool moved = ReferenceEquals(dragContainer, destination) ? dragContainer.TryMoveOrSwap(dragIndex, target.Index) : dragContainer.TryMoveOrSwapTo(destination, dragIndex, target.Index);
            if (moved && dragArea == InventoryUIArea.Loot && target.Area == InventoryUIArea.Bag)
                TryAutoEquip(destination, target.Index);
            if (moved && dragArea == InventoryUIArea.Loot && target.Area == InventoryUIArea.Bag)
                TryAutoRegisterAcquiredItem(dragItem);
        }
        CancelDrag();
    }

    private bool PlayerOwns(IInventoryItem item)
    {
        if (inventory?.InventoryContainer != null)
            for (int i = 0; i < inventory.InventoryContainer.SlotCount; i++)
                if (ReferenceEquals(inventory.InventoryContainer.GetSlot(i).Item, item)) return true;
        if (equipment?.WeaponEquipment != null)
            for (int i = 0; i < equipment.WeaponEquipment.SlotCount; i++)
                if (ReferenceEquals(equipment.WeaponEquipment.GetItem(i), item)) return true;
        if (equipment?.ProtectionEquipment != null && (ReferenceEquals(equipment.ProtectionEquipment.Armor, item) || ReferenceEquals(equipment.ProtectionEquipment.Helmet, item))) return true;
        return false;
    }

    private void RefreshViews()
    {
        EnsureSlots(quickViews, quickslotGrid, InventoryUIArea.WeaponQuickslot, equipment?.WeaponEquipment?.SlotCount ?? 0);
        foreach (InventorySlotView view in quickViews) view.Refresh();
        // 가방 창이 닫혀 있어도 등록된 아이템의 수량과 아이콘을 갱신한다.
        if (itemQuickslotGrid != null)
        {
            EnsureSlots(itemQuickViews, itemQuickslotGrid, InventoryUIArea.ItemQuickslot, 6);
            foreach (InventorySlotView view in itemQuickViews) view.Refresh();
        }
        if (!IsOpen || inventory?.InventoryContainer == null) return;
        EnsureSlots(bagViews, bagGrid, InventoryUIArea.Bag, inventory.InventoryContainer.SlotCount);
        EnsureSlots(equipmentViews, equipmentGrid, InventoryUIArea.Equipment, equipment?.WeaponEquipment != null ? equipment.WeaponEquipment.SlotCount + 2 : 0);
        foreach (InventorySlotView view in bagViews) view.Refresh();
        foreach (InventorySlotView view in equipmentViews) view.Refresh();
        RefreshTitle(bagTitle, "가방", inventory.InventoryContainer, ref previousBagUsed);
        if (openedLoot != null)
        {
            EnsureSlots(lootViews, lootGrid, InventoryUIArea.Loot, openedLoot.InventoryContainer.SlotCount);
            foreach (InventorySlotView view in lootViews) view.Refresh();
            RefreshTitle(lootTitle, openedLoot.name, openedLoot.InventoryContainer, ref previousLootUsed);
        }
        RefreshDetail();
    }

    private void EnsureSlots(List<InventorySlotView> views, RectTransform parent, InventoryUIArea area, int count)
    {
        // 처음과 칸 수 변경 때만 화면 칸을 만든다. 가방이나 무기 객체를 복사하지 않는다.
        while (views.Count < count)
        {
            InventorySlotView view = slotTemplate != null ? Instantiate(slotTemplate, parent) : InventoryUILayout.CreateSlot(parent, this);
            view.gameObject.SetActive(true);
            view.Bind(this, area, views.Count);
            views.Add(view);
        }
        for (int i = 0; i < views.Count; i++)
        {
            bool active = i < count;
            if (views[i].gameObject.activeSelf != active) views[i].gameObject.SetActive(active);
            if (active && (views[i].Area != area || views[i].Index != i)) views[i].Bind(this, area, i);
        }
    }

    private static void RefreshTitle(Text label, string name, InventoryContainer container, ref int previousUsed)
    {
        int used = 0;
        for (int i = 0; i < container.SlotCount; i++) if (!container.GetSlot(i).IsEmpty) used++;
        // 사용 칸이 같아도 장비 변경으로 전체 칸 수가 바뀔 수 있다.
        string title = $"{name} ({used}/{container.SlotCount})";
        if (used == previousUsed && label.text == title) return;
        previousUsed = used;
        label.text = title;
    }

    private void RefreshDetail()
    {
        bool exists = selectedItem != null && (PlayerOwns(selectedItem) || LootOwns(selectedItem));
        detailPanel.SetActive(exists);
        if (!exists) { selectedItem = null; return; }
        int count = selectedItem is DefinitionInventoryItem definedItem ? definedItem.Count : selectedItem is AmmoStack ammoStack ? ammoStack.GetCurrentCount() : 0;
        float useRatio = selectedItem is MedicineInventoryItem kit ? kit.RemainingUseRatio : selectedItem is ArmorInventoryItem protectionItem ? protectionItem.Armor.CurrentDurability : -1f;
        Sprite sprite = GetIcon(selectedItem);
        if (ReferenceEquals(previousDetailItem, selectedItem) && previousDetailCount == count && previousDetailUseRatio == useRatio && previousDetailIcon == sprite) return;
        previousDetailItem = selectedItem;
        previousDetailCount = count;
        previousDetailUseRatio = useRatio;
        previousDetailIcon = sprite;
        string description = GetVisual(selectedItem)?.description ?? string.Empty;
        string info = string.Empty;
        if (selectedItem is WeaponInventoryItem weapon)
        {
            WeaponDefinition definition = weapon.GetWeapon().Definition;
            // 탄창 숫자는 퀵슬롯에 맡기고 상세 창에는 무기의 기본 성능만 표시한다.
            info = $"기본 피해  {definition.baseDamage}\n초당 발사  {definition.fireRate}\n재장전  {definition.reloadDuration}초\n탄약 종류  {definition.ammoType}";
        }
        else if (selectedItem is AmmoStack && dataRoot?.AmmoCatalog != null && dataRoot.AmmoCatalog.TryGet(selectedItem.ItemId, out AmmoDefinition ammo))
            info = $"수량  {count}/{ammo.maxStackCount}\n관통 등급  {ammo.armorPenetrationLevel}\n피해 배율  {ammo.damageMultiplier}\n개당 무게  {ammo.unitWeight} kg";
        else if (selectedItem is DefinitionInventoryItem defined)
        {
            ItemDefinition definition = defined.Definition;
            info = $"수량  {count}/{definition.maxStackCount}\n개당 무게  {definition.unitWeight} kg";
            if (definition is MedicineDefinition medicine) info += $"\n회복량  {medicine.healAmount}\n사용 시간  {medicine.useDuration}초";
            if (selectedItem is MedicineInventoryItem) info += $"\n남은 사용량  {useRatio * 100f:0.##}%";
            else if (definition is ArmorDefinition armor) info += $"\n몸통 방어 등급  {armor.armorLevel}\n추가 가방 칸  {armor.backpackCapacityBonus}";
            else if (definition is HelmetDefinition helmet) info += $"\n머리 방어 등급  {helmet.armorLevel}";
            if (selectedItem is ArmorInventoryItem protection) info += $"\n내구도  {protection.Armor.CurrentDurability:0.##}/{protection.Armor.Definition.maxDurability:0.##}\n보호 효과  {protection.Armor.ProtectionFactor * 100f:0}%";
        }
        detailText.text = $"{GetItemName(selectedItem)}\n\n{description}\n\n{info}";
        detailIcon.sprite = sprite;
        detailIcon.enabled = sprite != null;
    }

    // 장비 화면만 무기 두 칸 뒤에 몸통과 머리를 배치한다. 무기 퀵슬롯은 두 칸을 유지한다.
    public static ItemType GetProtectionSlotType(int index) => index == 2 ? ItemType.Armor : index == 3 ? ItemType.Helmet : ItemType.None;

    private bool LootOwns(IInventoryItem item)
    {
        if (openedLoot == null) return false;
        for (int i = 0; i < openedLoot.InventoryContainer.SlotCount; i++)
            if (ReferenceEquals(openedLoot.InventoryContainer.GetSlot(i).Item, item)) return true;
        return false;
    }

    private static void EnsureEventSystem()
    {
        // 기존 EventSystem을 재사용한다. 없는 테스트 씬에서만 Input System용 모듈을 만든다.
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
        GameObject events = new GameObject("Inventory EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
