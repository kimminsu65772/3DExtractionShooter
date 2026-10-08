using System;
using System.Collections.Generic;

// 소유 공간만 합산한다. 손과 퀵슬롯은 소유 공간이 아니라 기존 아이템의 참조다.
public static class InventoryWeightCalculator
{
    public static float Calculate(InventoryContainer bag, WeaponEquipment weapons, ProtectionEquipment protection, Func<int, AmmoDefinition> findAmmo, HashSet<object> scratch = null)
    {
        // 이동에서 매 프레임 호출되므로 런타임 담당자는 중복 검사 집합을 재사용한다.
        HashSet<object> counted = scratch ?? new HashSet<object>();
        counted.Clear();
        double total = 0d;
        if (bag != null)
            for (int i = 0; i < bag.SlotCount; i++) total += GetWeight(bag.GetSlot(i).Item, counted, findAmmo);
        if (weapons != null)
            for (int i = 0; i < weapons.SlotCount; i++) total += GetWeight(weapons.GetItem(i), counted, findAmmo);
        if (protection != null)
        {
            total += GetWeight(protection.Armor, counted, findAmmo);
            total += GetWeight(protection.Helmet, counted, findAmmo);
        }
        return (float)Math.Min(total, float.MaxValue);
    }

    public static float GetMovementMultiplier(float weight, float maximum, float blockedRatio, float overloadedMultiplier)
    {
        if (weight >= maximum * blockedRatio) return 0f;
        return weight > maximum ? overloadedMultiplier : 1f;
    }

    private static double GetWeight(IInventoryItem item, HashSet<object> counted, Func<int, AmmoDefinition> findAmmo)
    {
        if (item == null || !counted.Add(item)) return 0d;
        if (item is WeaponInventoryItem gun)
        {
            WeaponInstance weapon = gun.GetWeapon();
            if (!counted.Add(weapon)) return 0d;
            AmmoDefinition ammo = weapon.AmmoState.LoadedAmmoCount > 0 ? findAmmo?.Invoke(weapon.AmmoState.LoadedAmmoId) : null;
            return SafeWeight(weapon.Definition.unitWeight) + (ammo != null ? SafeWeight(ammo.unitWeight) * weapon.AmmoState.LoadedAmmoCount : 0d);
        }
        if (item is AmmoStack stack) return SafeWeight(stack.Definition.unitWeight) * stack.GetCurrentCount();
        if (item is ArmorInventoryItem armor && !counted.Add(armor.Armor)) return 0d;
        return item is DefinitionInventoryItem defined ? SafeWeight(defined.Definition.unitWeight) * defined.Count : 0d;
    }

    private static double SafeWeight(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value) ? value : 0d;
}
