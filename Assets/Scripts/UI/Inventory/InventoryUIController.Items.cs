using System;
using System.Collections.Generic;
using ExtractionShooter.DataPipeline;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

// UI에서 요청한 이동·장착·버리기·퀵슬롯 조작을 기존 모델에 전달한다. Unity 컴포넌트는 기존 InventoryUIController 하나다.
public partial class InventoryUIController
{
    private bool CanDropFrom(InventoryUIArea area) => area != InventoryUIArea.Loot;

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
        if (IsGameplayBlocked) return;
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

    public void ClickSlot(InventorySlotView view, bool doubleClick)
    {
        if (IsGameplayBlocked || view == null) return;
        // 아이템 이동과 등록 요청이 끝난 시점에만 잘못된 참조를 정리한다.
        try { ClickSlotCore(view, doubleClick); }
        finally { RefreshQuickslotRegistrations(); }
    }

    private void RefreshQuickslotRegistrations()
    {
        itemQuickslots?.RemoveInvalidRegistrations();
        if (heldItem != null && heldItem.SelectedItem != null && !ReferenceEquals(itemQuickslots?.Quickslots.GetItem(heldItem.SelectedItemSlotIndex), heldItem.SelectedItem)) heldItem.ClearItemSelection();
        // [DATA-01 변경] 창고 이동처럼 컨테이너를 직접 수정하는 UI 경로도 최종 상태를 통지한다.
        inventory?.NotifyOwnedItemsChanged();
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
        if (selectedItem == null) { CloseDetail(); return; }
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
        if (IsGameplayBlocked || source == null) return;
        // 표시용 퀵슬롯을 가방의 실제 보관 칸처럼 드래그하지 않는다.
        if (!IsOpen || source.Area == InventoryUIArea.WeaponQuickslot || source.Area == InventoryUIArea.ItemQuickslot || !ValidateLootRange()) return;
        dragItem = GetItem(source.Area, source.Index);
        if (dragItem == null) return;
        dragArea = source.Area;
        dragIndex = source.Index;
        dragContainer = GetContainer(source.Area);
        SetLabel(dragText, referenceDrag, GetItemName(dragItem));
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
        if (IsGameplayBlocked || target == null) { CancelDrag(); return; }
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

    // 장비 화면만 무기 두 칸 뒤에 몸통과 머리를 배치한다. 무기 퀵슬롯은 두 칸을 유지한다.
    public static ItemType GetProtectionSlotType(int index) => index == 2 ? ItemType.Armor : index == 3 ? ItemType.Helmet : ItemType.None;

    private bool LootOwns(IInventoryItem item)
    {
        if (openedLoot == null) return false;
        for (int i = 0; i < openedLoot.InventoryContainer.SlotCount; i++)
            if (ReferenceEquals(openedLoot.InventoryContainer.GetSlot(i).Item, item)) return true;
        return false;
    }
}
