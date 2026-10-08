using System;
using System.Collections.Generic;

// 칸 감소를 먼저 계산하고 나중에 확정한다. 월드 드롭 준비 실패 시 원래 수량과 슬롯은 그대로 남는다.
public class InventoryResizePlan
{
    private readonly InventoryContainer source;
    private readonly IInventoryItem[] originals;
    private readonly int[] originalCounts;
    private readonly int[] plannedCounts;
    private readonly IInventoryItem[] destination;
    private readonly List<IInventoryItem> overflow = new();
    private bool committed;

    public IReadOnlyList<IInventoryItem> Overflow => overflow;

    public InventoryResizePlan(InventoryContainer source, int capacity)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        originals = new IInventoryItem[source.SlotCount];
        originalCounts = new int[source.SlotCount];
        plannedCounts = new int[source.SlotCount];
        destination = new IInventoryItem[capacity];
        for (int i = 0; i < originals.Length; i++)
        {
            originals[i] = source.GetSlot(i).Item;
            originalCounts[i] = plannedCounts[i] = GetCount(originals[i]);
        }
        if (capacity >= originals.Length)
        {
            Array.Copy(originals, destination, originals.Length);
            return;
        }
        int used = 0;
        for (int i = 0; i < originals.Length; i++)
        {
            IInventoryItem item = originals[i];
            if (item == null) continue;
            // 이미 남길 스택부터 채운다. 키트와 보호 장비는 고유 상태가 있으므로 합치지 않는다.
            for (int j = 0; j < i && plannedCounts[i] > 0; j++)
            {
                if (!CanMerge(originals[j], item) || !Contains(destination, originals[j])) continue;
                int moved = Math.Min(GetMaxCount(originals[j]) - plannedCounts[j], plannedCounts[i]);
                plannedCounts[j] += moved;
                plannedCounts[i] -= moved;
            }
            if (plannedCounts[i] == 0) continue;
            if (used < capacity) destination[used++] = item;
            else overflow.Add(item);
        }
    }

    public bool TryCommit()
    {
        if (committed || source.SlotCount != originals.Length) return false;
        // 준비 이후 소유권이나 수량이 달라졌으면 낡은 계획을 실행하지 않는다.
        for (int i = 0; i < originals.Length; i++)
            if (!ReferenceEquals(source.GetSlot(i).Item, originals[i]) || GetCount(originals[i]) != originalCounts[i]) return false;
        for (int i = 0; i < originals.Length; i++)
        {
            int delta = plannedCounts[i] - originalCounts[i];
            if (originals[i] is AmmoStack ammo)
            {
                if (delta > 0) ammo.AddAmmo(delta, out _);
                else if (delta < 0) ammo.TakeUpTo(-delta);
            }
            else if (originals[i] is DefinitionInventoryItem item)
            {
                if (delta > 0) item.AddUpTo(delta);
                else if (delta < 0) item.TakeUpTo(-delta);
            }
        }
        source.ApplyResize(destination);
        committed = true;
        return true;
    }

    private static int GetCount(IInventoryItem item) => item is AmmoStack ammo ? ammo.GetCurrentCount() : item is DefinitionInventoryItem defined ? defined.Count : item != null ? 1 : 0;
    private static int GetMaxCount(IInventoryItem item) => item is AmmoStack ammo ? ammo.Definition.maxStackCount : item is DefinitionInventoryItem defined ? defined.Definition.maxStackCount : 1;
    private static bool Contains(IInventoryItem[] items, IInventoryItem item)
    {
        foreach (IInventoryItem candidate in items) if (ReferenceEquals(candidate, item)) return true;
        return false;
    }
    private static bool CanMerge(IInventoryItem left, IInventoryItem right)
    {
        if (left == null || left.Type != right.Type || left.ItemId != right.ItemId) return false;
        if (left is AmmoStack && right is AmmoStack) return true;
        return left is DefinitionInventoryItem && right is DefinitionInventoryItem && left is not MedicineInventoryItem && right is not MedicineInventoryItem && left is not ArmorInventoryItem && right is not ArmorInventoryItem && GetMaxCount(left) > 1;
    }
}
