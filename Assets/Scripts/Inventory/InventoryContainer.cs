using System.Collections.Generic;
using System;

public class InventoryContainer
{
    private readonly List<InventorySlot> slots = new();

    public int SlotCount => slots.Count;

    public bool IsFull => slots.TrueForAll(slot => !slot.IsEmpty);

    public InventoryContainer(int slotCount)
    {
        if (slotCount <= 0)
        {
            throw new ArgumentException("설정하려는 슬롯은 0보다 커야합니다.");
        }

        for (int i = 0; i < slotCount; i++)
        {
            slots.Add(new InventorySlot());
        }
    }

    public InventorySlot GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "슬롯 인덱스가 범위를 벗어났습니다.");
        }
        return slots[index];
    }

    public bool TryPlaceInEmptySlot(IInventoryItem item, out int storedSlotIndex)
    {
        storedSlotIndex = -1;
        int firstEmptySlotIndex = -1;

        if (item == null)
            throw new ArgumentNullException(nameof(item), "저장하려는 아이템이 null입니다.");

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty && firstEmptySlotIndex == -1)
            {
                firstEmptySlotIndex = i;
            }

            if (slots[i].Item != null && ReferenceEquals(slots[i].Item, item))
            {
                return false;
            }
        }

        if (firstEmptySlotIndex != -1)
        {
            storedSlotIndex = firstEmptySlotIndex;
            slots[storedSlotIndex].SetItem(item);
            return true;
        }

        return false;
    }

    public bool TryMoveOrSwap(int fromIdx, int toIdx)
    {
        if (fromIdx < 0 || fromIdx >= slots.Count || toIdx < 0 || toIdx >= slots.Count)
        {
            return false;
        }

        if (fromIdx == toIdx)
        {
            return false;
        }

        var fromSlot = slots[fromIdx];
        var toSlot = slots[toIdx];
        if (fromSlot.IsEmpty)
        {
            return false;
        }
        if (toSlot.IsEmpty)
        {
            toSlot.SetItem(fromSlot.Item);
            fromSlot.SetItem(null);
            return true;
        }
        else if (fromSlot.Item is AmmoStack fromAmmo && toSlot.Item is AmmoStack toAmmo 
            && fromAmmo.ItemId == toAmmo.ItemId)
        {
            int requestedAmount = fromAmmo.GetCurrentCount();
            toAmmo.AddAmmo(requestedAmount, out int overflow);
            int accepted = requestedAmount - overflow;
            if (accepted > 0)
            {
                fromAmmo.TakeUpTo(accepted);
                if (fromAmmo.GetCurrentCount() == 0)
                {
                    fromSlot.SetItem(null);
                }
                return true;
            }
            return false;
        }
        else
        {
            var tempItem = toSlot.Item;
            toSlot.SetItem(fromSlot.Item);
            fromSlot.SetItem(tempItem);
            return true;
        }
    }

    // 서로 다른 인벤토리 간의 아이템 이동을 처리하는 메서드 (더블클릭 전용)
    public bool TryTransferTo(InventoryContainer target, int fromIdx, out int movedAmount)
    {
        movedAmount = 0;

        if (fromIdx < 0 || fromIdx >= slots.Count)
        {
            return false;
        }

        InventorySlot sourceSlot = GetSlot(fromIdx);

        // 타겟 인벤토리가 null이거나 옮기려는 슬롯이 비어있으면 false 반환
        if (target == null || sourceSlot.IsEmpty)
        {
            return false;
        }

        // 이 메서드는 서로 다른 인벤토리 간의 아이템 이동을 처리하므로, 같은 인벤토리에서 이동하려고 하면 false 반환
        if (ReferenceEquals(this, target))
        {
            return false;
        }

        bool movedAny = false;

        // 같은 종류의 탄약이 있는 슬롯을 찾아 합치기 시도
        if (sourceSlot.Item is AmmoStack source)
        {
            movedAny = TryFindSameAmmoSlotandMerge(target, sourceSlot, out movedAmount);

            // 아이템이 모두 합쳐져서 sourceSlot이 비어있다면 true 반환
            if (sourceSlot.IsEmpty)
            {
                return movedAny; 
            }
        }

        // 타겟 인벤토리에서 빈 슬롯을 찾아 아이템을 옮기고, 성공하면 sourceSlot을 비움
        if (!target.TryPlaceInEmptySlot(sourceSlot.Item, out int targetIdx))
            return movedAny;

        movedAny = true;
        movedAmount += sourceSlot.Item is AmmoStack ammo ? ammo.GetCurrentCount() : 1;
        sourceSlot.SetItem(null);
        return true;
    }

    // 서로 다른 인벤토리 간의 아이템 이동 또는 교환을 처리하는 메서드 (드래그 앤 드롭 전용)
    public bool TryMoveOrSwapTo(InventoryContainer target, int fromIdx, int toIdx)
    {
        if (target == null)
        {
            return false;
        }

        if (ReferenceEquals(this, target))
        {
            return false;
        }

        if (fromIdx < 0 || fromIdx >= slots.Count || toIdx < 0 || toIdx >= target.SlotCount)
        {
            return false;
        }

        InventorySlot sourceSlot = GetSlot(fromIdx);
        InventorySlot targetSlot = target.GetSlot(toIdx);
        if (sourceSlot.IsEmpty)
        {
            return false;
        }

        if (targetSlot.Item is AmmoStack targetAmmo && sourceSlot.Item is AmmoStack sourceAmmo 
            && targetAmmo.ItemId == sourceAmmo.ItemId)
        {
            int requestedAmount = sourceAmmo.GetCurrentCount();
            targetAmmo.AddAmmo(requestedAmount, out int overflow);
            int accepted = requestedAmount - overflow;
            if (accepted > 0)
            {
                sourceAmmo.TakeUpTo(accepted);
                if (sourceAmmo.GetCurrentCount() == 0)
                {
                    sourceSlot.SetItem(null);
                }
                return true;
            }
            return false;
        }

        var tempItem = targetSlot.Item;
        targetSlot.SetItem(sourceSlot.Item);
        sourceSlot.SetItem(tempItem);
        return true;
    }

    public int GetAmmoCount(int ammoId)
    {
        int totalCount = 0;
        foreach (var slot in slots)
        {
            if (slot.Item is AmmoStack ammo && ammo.ItemId == ammoId)
            {
                totalCount += ammo.GetCurrentCount();
            }
        }
        return totalCount;
    }

    public bool TryRemoveAmmo(int ammoId, int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "amount는 0보다 커야합니다.");
        }

        if (GetAmmoCount(ammoId) < amount)
        {
            return false;
        }

        int remainingToRemove = amount;

        while (remainingToRemove > 0)
        {
            bool removedAny = false;
            foreach (var slot in slots)
            {
                if (slot.Item is AmmoStack ammo && ammo.ItemId == ammoId)
                {
                    int taken = ammo.TakeUpTo(remainingToRemove);
                    remainingToRemove -= taken;
                    removedAny = true;
                    if (ammo.GetCurrentCount() == 0)
                    {
                        slot.SetItem(null);
                    }
                    if (remainingToRemove <= 0)
                    {
                        return true;
                    }
                }
            }
            // 순회를 했는데도 제거할 수 있는 탄약을 찾지 못했다면 false 반환
            if (!removedAny)
            {
                return false; 
            }
        }

        // 요구하는 양을 모두 제거하지 못했지만, 남은 양이 0이 되었으므로 true 반환
        return true;
    }

    public int AddAmmoUpTo(AmmoDefinition ammo, int amount)
    {
        if (ammo == null)
        {
            throw new ArgumentNullException(nameof(ammo), "AmmoDefinition은 null이면 안 됩니다.");
        }
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "amount는 0보다 커야합니다.");
        }
        
        int remainingToAdd = amount;

        // 먼저 같은 종류의 탄약이 있는 슬롯을 찾아 합치기 시도
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot.Item is AmmoStack existingAmmo && existingAmmo.ItemId == ammo.ammoId)
            {
                existingAmmo.AddAmmo(remainingToAdd, out int overflow);
                remainingToAdd = overflow;
                if (remainingToAdd <= 0)
                {
                    return amount; // 모든 양을 추가했으므로 요청한 양 반환
                }
            }
        }

        // 남은 양이 있다면 빈 슬롯을 찾아 새 AmmoStack을 생성하여 추가
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot.IsEmpty)
            {
                int toAdd = Math.Min(remainingToAdd, ammo.maxStackCount);
                AmmoStack newAmmoStack = new AmmoStack(ammo, toAdd);
                slot.SetItem(newAmmoStack);
                remainingToAdd -= toAdd;
                if (remainingToAdd <= 0)
                {
                    return amount; // 모든 양을 추가했으므로 요청한 양 반환
                }
            }
        }


        // 실제로 인벤토리에 추가한 양 반환
        return amount - remainingToAdd;
    }

    private bool TryFindSameAmmoSlotandMerge(InventoryContainer target, InventorySlot sourceSlot, out int movedAmount)
    {
        movedAmount = 0;

        if (target == null || sourceSlot?.Item is not AmmoStack sourceAmmo || sourceAmmo.GetCurrentCount() <= 0)
            return false;

        bool movedAny = false;

        for (int i = 0; i < target.SlotCount; i++)
        {
            InventorySlot targetSlot = target.GetSlot(i);
            if (targetSlot.Item is AmmoStack targetAmmo)
            {
                if (targetAmmo.ItemId == sourceAmmo.ItemId)
                {
                    int requestedAmount = sourceAmmo.GetCurrentCount();
                    targetAmmo.AddAmmo(requestedAmount, out int overflow);
                    int accepted = requestedAmount - overflow;
                    if (accepted > 0)
                    {
                        movedAny = true;
                        sourceAmmo.TakeUpTo(accepted);
                        movedAmount += accepted;
                        if (sourceAmmo.GetCurrentCount() == 0)
                        {
                            sourceSlot.SetItem(null);
                            return movedAny;
                        }
                    }
                }
            }
        }
        return movedAny;
    }
}
