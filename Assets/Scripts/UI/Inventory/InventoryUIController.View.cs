using System;
using System.Collections.Generic;
using ExtractionShooter.DataPipeline;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

// 뷰 참조 연결·슬롯/상세/무게 표시 갱신을 담당한다. Unity 컴포넌트는 기존 InventoryUIController 하나다.
public partial class InventoryUIController
{
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
        if (carryWeight == null) carryWeight = GetComponent<PlayerCarryWeight>();
        if (aimCursor == null) aimCursor = GetComponent<PlayerAimCursor>();
        if (crosshairRoot == null)
        {
            // 기본 싱글플레이 씬에서는 조준점만 찾는다. 다른 HUD가 든 Canvas는 숨기지 않는다.
            Crosshair crosshair = UnityEngine.Object.FindFirstObjectByType<Crosshair>(FindObjectsInactive.Include);
            if (crosshair != null) crosshairRoot = crosshair.gameObject;
        }
        if (dataRoot == null && holder != null) dataRoot = holder.DataRoot;
    }

    [ContextMenu("UI/프리팹 바인딩")]
    public void BuildView()
    {
        ResolveReferences();
        if (boundView == null && uiCanvas != null) boundView = uiCanvas.GetComponent<InventoryUIView>();
        if (boundView == null && uiCanvas == null)
        {
            InventoryUIView prefab = viewPrefab != null ? viewPrefab : Resources.Load<InventoryUIView>("UI/InventoryHUD");
            if (prefab != null) boundView = Instantiate(prefab);
        }
        if (boundView == null || !boundView.Bind(this))
        {
            Debug.LogError("연결할 InventoryUIView 프리팹이 필요합니다.", this);
            return;
        }
        dropMenu = boundView.dropMenu;
        dropMenu.Initialize(DropContextItem, CloseDropMenu, UnloadContextItem);
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

    public void ConfigureWeightView(Text label, UnityEngine.UI.Image fill)
    {
        if (weightText == null) weightText = label;
        if (weightFill == null) weightFill = fill;
        previousWeight = previousMaximumWeight = -1f;
    }

    public void ConfigureReferenceLabels(TMP_Text bag, TMP_Text loot, TMP_Text weight, Image fill,
        TMP_Text detail, TMP_Text ghost, RectTransform itemSlots,
        InventorySlotView[] weaponSlots, InventorySlotView[] consumableSlots)
    {
        referenceBagTitle = bag;
        referenceLootTitle = loot;
        referenceWeight = weight;
        weightFill = fill;
        referenceDetail = detail;
        referenceDrag = ghost;
        itemQuickslotGrid = itemSlots;
        quickViews.Clear();
        itemQuickViews.Clear();
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            weaponSlots[i].Bind(this, InventoryUIArea.WeaponQuickslot, i);
            quickViews.Add(weaponSlots[i]);
        }
        for (int i = 0; i < consumableSlots.Length; i++)
        {
            consumableSlots[i].Bind(this, InventoryUIArea.ItemQuickslot, i);
            itemQuickViews.Add(consumableSlots[i]);
        }
        usesReferenceLayout = true;
    }

    private static void SetLabel(Text legacy, TMP_Text reference, string value)
    {
        if (reference != null) reference.text = value;
        else if (legacy != null) legacy.text = value;
    }

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
        RefreshWeight();
        EnsureSlots(bagViews, bagGrid, InventoryUIArea.Bag, inventory.InventoryContainer.SlotCount);
        EnsureSlots(equipmentViews, equipmentGrid, InventoryUIArea.Equipment, equipment?.WeaponEquipment != null ? equipment.WeaponEquipment.SlotCount + 2 : 0);
        foreach (InventorySlotView view in bagViews) view.Refresh();
        foreach (InventorySlotView view in equipmentViews) view.Refresh();
        RefreshTitle(bagTitle, referenceBagTitle, "가방", inventory.InventoryContainer, ref previousBagUsed);
        if (openedLoot != null)
        {
            EnsureSlots(lootViews, lootGrid, InventoryUIArea.Loot, openedLoot.InventoryContainer.SlotCount);
            foreach (InventorySlotView view in lootViews) view.Refresh();
            RefreshTitle(lootTitle, referenceLootTitle, openedLoot.name, openedLoot.InventoryContainer, ref previousLootUsed);
        }
        RefreshDetail();
    }

    private void RefreshWeight()
    {
        if (carryWeight == null || (weightText == null && referenceWeight == null) || weightFill == null) return;
        // 장착 장비·장전 탄약까지 포함한 기존 계산값을 쓰고 UI에서 중복 집계하지 않는다.
        float current = carryWeight.CurrentWeight;
        float maximum = carryWeight.MaximumWeight;
        if (current == previousWeight && maximum == previousMaximumWeight) return;
        previousWeight = current;
        previousMaximumWeight = maximum;
        SetLabel(weightText, referenceWeight, $"전체 소지 중량: {current:F1} / {maximum:F1} kg");
        // 한도를 초과해도 수치는 그대로 표시하고 막대의 길이만 전체 폭으로 제한한다.
        weightFill.fillAmount = Mathf.Clamp01(current / maximum);
    }

    private void EnsureSlots(List<InventorySlotView> views, RectTransform parent, InventoryUIArea area, int count)
    {
        // 처음과 칸 수 변경 때만 화면 칸을 만든다. 가방이나 무기 객체를 복사하지 않는다.
        while (views.Count < count)
        {
            if (slotTemplate == null) { Debug.LogError("슬롯 프리팹 참조가 필요합니다.", this); return; }
            InventorySlotView view = Instantiate(slotTemplate, parent);
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

    private static void RefreshTitle(Text label, TMP_Text reference, string name, InventoryContainer container, ref int previousUsed)
    {
        int used = 0;
        for (int i = 0; i < container.SlotCount; i++) if (!container.GetSlot(i).IsEmpty) used++;
        // 사용 칸이 같아도 장비 변경으로 전체 칸 수가 바뀔 수 있다.
        string title = $"{name} ({used}/{container.SlotCount})";
        if (used == previousUsed && (reference != null ? reference.text : label != null ? label.text : null) == title) return;
        previousUsed = used;
        SetLabel(label, reference, title);
    }

    private void RefreshDetail()
    {
        bool exists = IsOpen && selectedItem != null && (PlayerOwns(selectedItem) || LootOwns(selectedItem));
        SetDetailVisible(exists);
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
        SetLabel(detailText, referenceDetail, $"{GetItemName(selectedItem)}\n\n{description}\n\n{info}");
        detailIcon.sprite = sprite;
        detailIcon.enabled = sprite != null;
    }

    private void EnsureEventSystem()
    {
        // 기존 EventSystem을 재사용하고, 없을 때만 프리팹에 준비된 대상을 켠다.
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
        if (boundView != null && boundView.fallbackEventSystem != null)
            boundView.fallbackEventSystem.gameObject.SetActive(true);
        else Debug.LogError("EventSystem 참조가 필요합니다.", this);
    }
}
