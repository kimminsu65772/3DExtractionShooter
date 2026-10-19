using ExtractionShooter.DataPipeline;

// 고정 지급과 무작위 지급이 같은 카탈로그·인스턴스 생성 규칙을 사용한다.
public static class LootItemFactory
{
    // 탄약 30발은 30칸이 아니라 한 스택이다. 상자 용량을 계산할 때 실제 저장 단위를 사용한다.
    public static long GetRequiredSlots(CombatDataCompositionRoot root, ItemType type, int id, int count)
    {
        int stackSize = 1;
        if (type == ItemType.Ammo && root.AmmoCatalog.TryGet(id, out AmmoDefinition ammo)) stackSize = ammo.maxStackCount;
        else if (TryGetDefinition(root, type, id, out ItemDefinition definition))
        {
            bool unique = definition is ProtectionDefinition || definition is MedicineDefinition medicine && medicine.useCostRatio.HasValue;
            stackSize = unique ? 1 : definition.maxStackCount;
        }
        return ((long)count + stackSize - 1) / stackSize;
    }

    public static bool IsAvailable(CombatDataCompositionRoot root, ItemType type, int id)
    {
        if (root == null || !root.isReady) return false;
        switch (type)
        {
            case ItemType.Weapon:
                return root.WeaponCatalog != null && root.WeaponCatalog.TryGet(id, out RuntimeWeapon weapon)
                    && weapon.Definition.weaponType == WeaponType.Ranged;
            case ItemType.Ammo:
                return root.AmmoCatalog != null && root.AmmoCatalog.TryGet(id, out _);
            default:
                return TryGetDefinition(root, type, id, out _);
        }
    }

    public static bool TryAdd(InventoryContainer inventory, CombatDataCompositionRoot root, ItemType type, int id, int count)
    {
        if (inventory == null || count <= 0 || !IsAvailable(root, type, id)) return false;
        if (type == ItemType.Weapon)
        {
            if (count > inventory.SlotCount) return false;
            root.WeaponCatalog.TryGet(id, out RuntimeWeapon weapon);
            // 총마다 별개의 탄창을 만들고 이후 획득·드롭에서는 이 객체를 그대로 옮긴다.
            for (int i = 0; i < count; i++)
                if (!inventory.TryPlaceInEmptySlot(new WeaponInventoryItem(new WeaponInstance(weapon)), out _)) return false;
            return true;
        }
        if (type == ItemType.Ammo)
        {
            root.AmmoCatalog.TryGet(id, out AmmoDefinition ammo);
            return inventory.AddAmmoUpTo(ammo, count) == count;
        }
        if (!TryGetDefinition(root, type, id, out ItemDefinition definition)) return false;
        // 기존 컨테이너가 키트 잔여량·보호구 내구도의 독립 인스턴스도 생성한다.
        return inventory.AddItemUpTo(definition, count) == count;
    }

    private static bool TryGetDefinition(CombatDataCompositionRoot root, ItemType type, int id, out ItemDefinition definition)
    {
        definition = null;
        if (type == ItemType.Meds && root.MedicineCatalog != null && root.MedicineCatalog.TryGet(id, out MedicineDefinition medicine)) definition = medicine;
        if (type == ItemType.Armor && root.ArmorCatalog != null && root.ArmorCatalog.TryGet(id, out ArmorDefinition armor)) definition = armor;
        if (type == ItemType.Helmet && root.HelmetCatalog != null && root.HelmetCatalog.TryGet(id, out HelmetDefinition helmet)) definition = helmet;
        return definition != null;
    }
}
