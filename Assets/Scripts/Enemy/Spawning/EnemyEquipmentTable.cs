using System;
using System.Collections.Generic;
using ExtractionShooter.DataPipeline;
using UnityEngine;

// 공유 SO에는 확률만 저장한다. 실제 무기·내구도는 생성된 적의 Loadout이 소유한다.
[CreateAssetMenu(menuName = "ExtractionShooter/Enemy/Equipment Table")]
public sealed class EnemyEquipmentTable : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [Min(1)] public int itemId;
        [Tooltip("무기 후보에만 지정하는 호환 탄약 ID")]
        [Min(0)] public int ammoId;
        [Min(0)] public float weight = 1f;
    }

    [Serializable]
    public sealed class Slot
    {
        [Range(0f, 1f)] public float equipChance = 1f;
        public Entry[] entries = Array.Empty<Entry>();
    }

    public Slot weapon = new();
    public Slot armor = new();
    public Slot helmet = new();
    [TextArea] public string sourceNote;

    public readonly struct Selection
    {
        public readonly int WeaponId, AmmoId, ArmorId, HelmetId;
        public Selection(int weaponId, int ammoId, int armorId, int helmetId)
        { WeaponId = weaponId; AmmoId = ammoId; ArmorId = armorId; HelmetId = helmetId; }
    }

    public bool Validate(CombatDataCompositionRoot root, out string error)
    {
        error = null;
        if (root == null || !root.isReady) { error = "전투 데이터가 준비되지 않았다."; return false; }
        // 현재 원거리 스폰에서는 빈손이 허용되지 않는다. 미장착 보호구는 확률 0으로 표현한다.
        if (weapon == null || weapon.equipChance != 1f)
        { error = "원거리 적은 무기를 확정 장착해야 한다."; return false; }
        return ValidateSlot(weapon, ItemType.Weapon, root, out error)
            && ValidateSlot(armor, ItemType.Armor, root, out error)
            && ValidateSlot(helmet, ItemType.Helmet, root, out error);
    }

    private static bool ValidateSlot(Slot slot, ItemType type, CombatDataCompositionRoot root, out string error)
    {
        error = null;
        if (slot == null || float.IsNaN(slot.equipChance) || slot.equipChance < 0f || slot.equipChance > 1f || slot.entries == null)
        { error = $"{type}: 장착 확률 또는 후보 배열이 유효하지 않다."; return false; }
        double total = 0;
        var ids = new HashSet<int>();
        foreach (Entry entry in slot.entries)
        {
            if (entry == null || entry.itemId <= 0 || !ids.Add(entry.itemId) || float.IsNaN(entry.weight) || float.IsInfinity(entry.weight) || entry.weight < 0f)
            { error = $"{type}: 잘못된 후보 또는 중복 ID가 있다."; return false; }
            bool available;
            if (type == ItemType.Weapon)
                available = root.WeaponCatalog != null && root.AmmoCatalog != null
                    && root.WeaponCatalog.TryGet(entry.itemId, out var gun)
                    && root.AmmoCatalog.TryGet(entry.ammoId, out var ammo)
                    && gun.Definition.ammoType == ammo.ammoType;
            else if (type == ItemType.Armor)
                available = entry.ammoId == 0 && root.ArmorCatalog != null && root.ArmorCatalog.TryGet(entry.itemId, out _);
            else
                available = entry.ammoId == 0 && root.HelmetCatalog != null && root.HelmetCatalog.TryGet(entry.itemId, out _);
            if (!available) { error = $"{type}: ID {entry.itemId} 또는 탄약 {entry.ammoId}가 유효하지 않다."; return false; }
            total += entry.weight;
        }
        if (slot.equipChance > 0f && (total <= 0 || double.IsInfinity(total)))
        { error = $"{type}: 장착할 양수 가중치 후보가 없다."; return false; }
        return true;
    }

    public bool TryRoll(CombatDataCompositionRoot root, System.Random random, out Selection selection)
    {
        selection = default;
        // 누락 후보를 몰래 빼거나 실패한 후보를 재추첨하지 않는다.
        if (random == null || !Validate(root, out _)) return false;
        Entry gun = Roll(weapon, random);
        Entry body = Roll(armor, random);
        Entry head = Roll(helmet, random);
        selection = new Selection(gun.itemId, gun.ammoId, body?.itemId ?? 0, head?.itemId ?? 0);
        return true;
    }

    private static Entry Roll(Slot slot, System.Random random)
    {
        if (slot.equipChance <= 0f || (slot.equipChance < 1f && random.NextDouble() >= slot.equipChance)) return null;
        double total = 0;
        foreach (Entry entry in slot.entries) total += entry.weight;
        double roll = random.NextDouble() * total;
        Entry last = null;
        foreach (Entry entry in slot.entries)
        {
            if (entry.weight <= 0f) continue;
            last = entry;
            roll -= entry.weight;
            if (roll < 0) return entry;
        }
        return last;
    }
}
