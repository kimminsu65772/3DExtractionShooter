using System.Collections.Generic;
using System;

public class InventoryContainer
{
    private readonly List<InventorySlot> slots = new();

    public int SlotCount => slots.Count;

    public InventoryContainer(int slotCount)
    {
        if (slotCount <= 0)
        {
            throw new System.ArgumentException("설정하려는 슬롯은 0보다 커야합니다.");
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
            throw new System.ArgumentOutOfRangeException(nameof(index), "슬롯 인덱스가 범위를 벗어났습니다.");
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
        else
        {
            var tempItem = toSlot.Item;
            toSlot.SetItem(fromSlot.Item);
            fromSlot.SetItem(tempItem);
            return true;
        }
    }

    public bool TryTransferTo(InventoryContainer target, int fromIdx, out int targetIdx)
    {
        targetIdx = -1;

        if (fromIdx < 0 || fromIdx >= slots.Count)
        {
            return false;
        }

        InventorySlot sourceSlot = GetSlot(fromIdx);

        if (target == null || sourceSlot == null)
        {
            return false;
        }

        if (ReferenceEquals(this, target))
        {
            return false;
        }

        if (!target.TryPlaceInEmptySlot(slots[fromIdx].Item, out targetIdx))
            return false;

        sourceSlot.SetItem(null);
        return true;
    }
}
