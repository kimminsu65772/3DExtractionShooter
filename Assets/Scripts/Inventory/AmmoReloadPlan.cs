using System;
using System.Collections.Generic;
using ExtractionShooter.DataPipeline;

// ACT-04: 기존 탄약 API로 가방 복사본에서 교환을 계산한다.
// 바닥 드롭 준비에 실패하거나 그 사이 원본이 바뀌면 실제 총과 가방은 건드리지 않는다.
public sealed class AmmoReloadPlan
{
    private readonly InventoryContainer bag;
    private readonly WeaponInstance weapon;
    private readonly AmmoDefinition selectedAmmo;
    private readonly IInventoryItem[] originals, destination;
    private readonly int[] originalCounts;
    private readonly int oldId, oldCount, oldLastId;
    private readonly List<IInventoryItem> overflow = new();
    private bool committed;
    public int LoadedCount { get; }
    public IReadOnlyList<IInventoryItem> Overflow => overflow;

    private AmmoReloadPlan(InventoryContainer bag, WeaponInstance weapon, AmmoCatalog catalog, AmmoDefinition selectedAmmo)
    {
        this.bag = bag;
        this.weapon = weapon;
        this.selectedAmmo = selectedAmmo;
        oldId = weapon.AmmoState.LoadedAmmoId;
        oldCount = weapon.AmmoState.LoadedAmmoCount;
        oldLastId = weapon.AmmoState.LastLoadedAmmoId;
        originals = new IInventoryItem[bag.SlotCount];
        destination = new IInventoryItem[bag.SlotCount];
        originalCounts = new int[bag.SlotCount];
        InventoryContainer staged = new InventoryContainer(bag.SlotCount);
        for (int i = 0; i < bag.SlotCount; i++)
        {
            IInventoryItem item = originals[i] = bag.GetSlot(i).Item;
            if (item is AmmoStack ammo)
            {
                originalCounts[i] = ammo.GetCurrentCount();
                item = new AmmoStack(ammo.Definition, originalCounts[i]);
            }
            staged.GetSlot(i).SetItem(item);
        }
        var storage = new InventoryAmmoStorage(staged, catalog);
        LoadedCount = Math.Min(weapon.AmmoState.Capacity, storage.GetCount(selectedAmmo.ammoId));
        if (weapon.Definition.reloadMode == WeaponReloadMode.SingleRound) LoadedCount = Math.Min(LoadedCount, 1);
        if (LoadedCount <= 0 || !storage.TryRemove(selectedAmmo.ammoId, LoadedCount)) return;
        // 먼저 새 탄약을 소비한 가방으로 반환 공간을 계산한다.
        catalog.TryGet(oldId, out AmmoDefinition previousAmmo);
        storage.TryAdd(oldId, oldCount, out int added);
        int remaining = oldCount - added;
        while (remaining > 0)
        {
            int amount = Math.Min(remaining, previousAmmo.maxStackCount);
            overflow.Add(new AmmoStack(previousAmmo, amount));
            remaining -= amount;
        }
        for (int i = 0; i < destination.Length; i++) destination[i] = staged.GetSlot(i).Item;
    }

    public static bool TryCreate(InventoryContainer bag, WeaponInstance weapon, AmmoCatalog catalog, int selectedId, out AmmoReloadPlan plan)
    {
        plan = null;
        if (bag == null || weapon == null || catalog == null || weapon.AmmoState.IsEmpty ||
            selectedId == weapon.AmmoState.LoadedAmmoId || !catalog.TryGet(selectedId, out AmmoDefinition selected) ||
            selected.ammoType != weapon.AmmoState.CompatibleAmmoType ||
            !catalog.TryGet(weapon.AmmoState.LoadedAmmoId, out AmmoDefinition previous) ||
            previous.ammoType != weapon.AmmoState.CompatibleAmmoType || previous.maxStackCount <= 0 ||
            bag.GetAmmoCount(selectedId) <= 0) return false;
        plan = new AmmoReloadPlan(bag, weapon, catalog, selected);
        return plan.LoadedCount > 0;
    }

    public bool TryCommit()
    {
        WeaponAmmoState magazine = weapon.AmmoState;
        if (committed || bag.SlotCount != originals.Length || magazine.LoadedAmmoId != oldId ||
            magazine.LoadedAmmoCount != oldCount || magazine.LastLoadedAmmoId != oldLastId) return false;
        for (int i = 0; i < originals.Length; i++)
            if (!ReferenceEquals(bag.GetSlot(i).Item, originals[i]) ||
                originals[i] is AmmoStack ammo && ammo.GetCurrentCount() != originalCounts[i]) return false;

        // 검사 후 콜백을 끼우지 않고 동기로 확정하며 기존 스택의 참조를 가능한 한 보존한다.
        for (int i = 0; i < destination.Length; i++)
        {
            if (originals[i] is AmmoStack original && destination[i] is AmmoStack planned && original.ItemId == planned.ItemId)
            {
                int delta = planned.GetCurrentCount() - originalCounts[i];
                if (delta > 0) original.AddAmmo(delta, out _);
                else if (delta < 0) original.TakeUpTo(-delta);
                destination[i] = original;
            }
        }
        bag.ApplyResize(destination);
        for (int i = 0; i < oldCount; i++) magazine.TryConsumeRound(out _);
        magazine.Load(selectedAmmo, LoadedCount);
        committed = true;
        return true;
    }
}
