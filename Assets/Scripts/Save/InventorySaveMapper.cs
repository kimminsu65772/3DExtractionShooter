using ExtractionShooter.DataPipeline;

public class InventorySaveMapper
{
    public static ItemSaveData CaptureItem(IInventoryItem item)
    {
        if (item == null)
        {
            return null;
        }

        ItemSaveData itemSavedata = new();

        itemSavedata.Type = item.Type;
        itemSavedata.ItemId = item.ItemId;
        itemSavedata.Count = 1;

        // 스택이 가능한 아이템은 따로 스택의 개수를 저장한다.
        if (item is AmmoStack ammoStack)
            itemSavedata.Count = ammoStack.GetCurrentCount();
        else if (item is DefinitionInventoryItem itemDefinition)
            itemSavedata.Count = itemDefinition.Count;

        // 아이템별로 저장해야 하는 추가 정보가 있다면, 해당 정보를 저장한다.
        if (item is WeaponInventoryItem weapon)
        {
            WeaponAmmoState weaponState = weapon.GetWeapon().AmmoState;
            itemSavedata.WeaponAmmo = new WeaponSaveData
            {
                LoadedAmmoId = weaponState.LoadedAmmoId,
                LoadedAmmoCount = weaponState.LoadedAmmoCount,
                LastLoadedAmmoId = weaponState.LastLoadedAmmoId
            };
        }
        else if (item is ArmorInventoryItem armor)
            itemSavedata.CurrentDurability = armor.CurrentDurability;
        else if (item is MedicineInventoryItem medicine)
            itemSavedata.RemainingUseRatio = medicine.RemainingUseRatio;

        return itemSavedata;
    }

    public static InventorySaveData CaptureContainer(InventoryContainer container)
    {
        InventorySaveData saveData = new InventorySaveData();
        saveData.Slots = new ItemSaveData[container.SlotCount];
        for (int i = 0; i < saveData.Slots.Length; i++)
        {
            saveData.Slots[i] = CaptureItem(container.GetSlot(i).Item);
        }
        return saveData;
    }

    public static EquipmentSaveData CaptureEquipment(PlayerEquipment equipment)
    {
        EquipmentSaveData saveData = new EquipmentSaveData();
        saveData.WeaponSlots = new ItemSaveData[equipment.WeaponEquipment.SlotCount];
        for (int i = 0; i < saveData.WeaponSlots.Length; i++)
        {
            saveData.WeaponSlots[i] = CaptureItem(equipment.WeaponEquipment.GetItem(i));
        }
        saveData.Armor = CaptureItem(equipment.ProtectionEquipment.Armor);
        saveData.Helmet = CaptureItem(equipment.ProtectionEquipment.Helmet);
        saveData.SelectedWeaponIndex = equipment.SelectedWeaponSlotIndex;
        return saveData;
    }

    public static int?[] CaptureQuickslots(ItemQuickslots quickslots, InventoryContainer container)
    {
        int?[] quickSlotData = new int?[quickslots.SlotCount];

        for (int i = 0; i < quickslots.SlotCount; i++)
        {
            IInventoryItem item = quickslots.GetItem(i);

            if (item == null)
                continue;

            bool isItemInContainer = TryFindSameItemInContainer(item, container, out int containerIndex);

            if (isItemInContainer)
            {
                quickSlotData[i] = containerIndex;
            }
        }

        return quickSlotData;
    }

    public static GameSaveData CaptureGameData(InventoryContainer bag, InventoryContainer chest, PlayerEquipment equipment, ItemQuickslots quickslots)
    {
        GameSaveData saveData = SaveDataFactory.CreateNewData<GameSaveData>();

        saveData.Bag = CaptureContainer(bag);
        saveData.Chest = CaptureContainer(chest);
        saveData.Equipment = CaptureEquipment(equipment);
        saveData.QuickslotsBagIndices = CaptureQuickslots(quickslots, bag);

        return saveData;
    }

    public static bool TryRestoreItem(ItemSaveData data, CombatDataCompositionRoot dataRoot, out IInventoryItem item)
    {
        item = null;

        // 입력과 데이터 준비 상태 확인
        if (data == null || dataRoot == null)
            return false;

        // 데이터 준비 상태 확인
        if (!dataRoot.isReady)
            return false;

        // 카탈로그 준비 상태 확인
        if (dataRoot.MedicineCatalog == null || dataRoot.WeaponCatalog == null || dataRoot.AmmoCatalog == null || dataRoot.ArmorCatalog == null)
            return false;

        switch(data.Type)
        {
            case ItemType.Weapon:
                if (data.Count != 1)
                    return false;
                if (!dataRoot.WeaponCatalog.TryGet(data.ItemId, out RuntimeWeapon weaponData))
                    return false;
                WeaponInstance weapon = new WeaponInstance(weaponData);
                if (data.WeaponAmmo == null)
                    return false;

                if (data.WeaponAmmo.LastLoadedAmmoId < 0)
                    return false;

                else if (data.WeaponAmmo.LastLoadedAmmoId > 0)
                {
                    if (!dataRoot.AmmoCatalog.TryGet(data.WeaponAmmo.LastLoadedAmmoId, out AmmoDefinition lastLoadedAmmoData))
                        return false;

                    if (lastLoadedAmmoData.ammoType != weapon.AmmoState.CompatibleAmmoType)
                        return false;
                }

                if (!weapon.AmmoState.TryRestore(data.WeaponAmmo.LoadedAmmoId, data.WeaponAmmo.LoadedAmmoCount, data.WeaponAmmo.LastLoadedAmmoId))
                    return false;

                item = new WeaponInventoryItem(weapon);
                break;
            case ItemType.Ammo:
                if (!dataRoot.AmmoCatalog.TryGet(data.ItemId, out AmmoDefinition ammoData))
                    return false;
                if (data.Count <= 0 || data.Count > ammoData.maxStackCount)
                    return false;
                item = new AmmoStack(ammoData, data.Count);
                break;
            case ItemType.Armor:
                if (data.Count != 1)
                    return false;
                if (!dataRoot.ArmorCatalog.TryGet(data.ItemId, out ArmorDefinition armorDefinition))
                    return false;
                ArmorInstance armorInstance = new ArmorInstance(armorDefinition);

                if (!data.CurrentDurability.HasValue)
                    return false;

                float armorDurability = data.CurrentDurability.Value;

                if (!armorInstance.TryRestoreDurability(armorDurability))
                    return false;

                item = new ArmorInventoryItem(armorInstance);
                break;
            case ItemType.Helmet:
                if (data.Count != 1)
                    return false;
                if (!dataRoot.HelmetCatalog.TryGet(data.ItemId, out HelmetDefinition helmetDefinition))
                    return false;
                ArmorInstance helmetInstance = new ArmorInstance(helmetDefinition);

                if (!data.CurrentDurability.HasValue)
                    return false;

                float helmetDurability = data.CurrentDurability.Value;

                if (!helmetInstance.TryRestoreDurability(helmetDurability))
                    return false;

                item = new ArmorInventoryItem(helmetInstance);
                break;
            case ItemType.Meds:
                if (!dataRoot.MedicineCatalog.TryGet(data.ItemId, out MedicineDefinition medicineDefinition))
                    return false;
                if (!medicineDefinition.useCostRatio.HasValue)
                {
                    if (data.Count <= 0 || data.Count > medicineDefinition.maxStackCount)
                        return false;
                    item = new DefinitionInventoryItem(medicineDefinition, data.Count);
                }
                else
                {
                    if (data.Count != 1)
                        return false;
                    MedicineInventoryItem aedKit = new MedicineInventoryItem(medicineDefinition);
                    if (!aedKit.TryRestoreRemainingUseRatio(data.RemainingUseRatio))
                        return false;
                    item = aedKit;
                }
                break;
            default:
                return false;
        }

        return true;
    }

    public static bool TryRestoreSlots(InventorySaveData data, CombatDataCompositionRoot dataRoot, out IInventoryItem[] items)
    {
        items = null;
        if (data == null || data.Slots == null || dataRoot == null)
            return false;

        if (!dataRoot.isReady)
            return false;

        IInventoryItem[] restoredItems = new IInventoryItem[data.Slots.Length];
        for (int i = 0; i < data.Slots.Length; i++)
        {
            ItemSaveData itemData = data.Slots[i];

            if (itemData == null)
                continue;

            if (!TryRestoreItem(itemData, dataRoot, out IInventoryItem item))
                return false;
            restoredItems[i] = item;
        }

        items = restoredItems;
        return true;
    }

    public static bool TryRestoreEquipment(
        EquipmentSaveData data,
        CombatDataCompositionRoot dataRoot,
        out WeaponEquipment weapons,
        out ProtectionEquipment protection,
        out int selectedWeaponIndex)
    {
        weapons = null;
        protection = null;
        selectedWeaponIndex = -1;

        if (data == null || data.WeaponSlots == null || dataRoot == null)
            return false;

        if (!dataRoot.isReady)
            return false;

        if (data.SelectedWeaponIndex < 0 || data.SelectedWeaponIndex >= data.WeaponSlots.Length)
            return false;

        WeaponEquipment weaponsEquipment = new WeaponEquipment();
        ProtectionEquipment protectionEquipment = new ProtectionEquipment();

        if (data.WeaponSlots.Length != weaponsEquipment.SlotCount)
            return false;

        for (int i = 0; i < data.WeaponSlots.Length; i++)
        {
            ItemSaveData itemData = data.WeaponSlots[i];
            if (itemData == null)
                continue;
            if (!TryRestoreItem(itemData, dataRoot, out IInventoryItem item))
                return false;
            if (item is WeaponInventoryItem weaponItem)
                weaponsEquipment.SetItem(i, weaponItem);
            else
                return false;
        }

        if (data.Armor != null)
        {
            if (!TryRestoreItem(data.Armor, dataRoot, out IInventoryItem armorItem))
                return false;
            if (armorItem is ArmorInventoryItem armor)
            {
                if (armor.Type != ItemType.Armor)
                    return false;
                protectionEquipment.SetItem(ItemType.Armor, armor);
            }  
            else
                return false;
        }

        if (data.Helmet != null)
        {
            if(!TryRestoreItem(data.Helmet, dataRoot, out IInventoryItem helmetItem))
                return false;
            if (helmetItem is ArmorInventoryItem helmet)
            {
                if (helmet.Type != ItemType.Helmet)
                    return false;
                protectionEquipment.SetItem(ItemType.Helmet, helmet);
            }
            else
                return false;
        }

        weapons = weaponsEquipment;
        protection = protectionEquipment;
        selectedWeaponIndex = data.SelectedWeaponIndex;

        return true;
    }

    public static bool TryRestoreQuickslots(int?[] quickslotData, InventoryContainer container, out ItemQuickslots quickslots)
    {
        quickslots = null;
        if (quickslotData == null || container == null)
            return false;
        ItemQuickslots restoredQuickslots = new ItemQuickslots();

        if (quickslotData.Length != restoredQuickslots.SlotCount)
            return false;

        for (int i = 0; i < quickslotData.Length; i++)
        {
            int? containerIndex = quickslotData[i];
            if (!containerIndex.HasValue)
                continue;
            if (containerIndex.Value < 0 || containerIndex.Value >= container.SlotCount)
                return false;
            IInventoryItem item = container.GetSlot(containerIndex.Value).Item;
            if (item == null)
                return false;
            if (item.Type != ItemType.Consumable
                && item.Type != ItemType.Meds
                && item.Type != ItemType.Buff
                && item.Type != ItemType.Throwables)
                return false;

            if(!ReferenceEquals(item, container.GetSlot(containerIndex.Value).Item))
                return false;

            restoredQuickslots.SetItem(i, item);
        }
        quickslots = restoredQuickslots;
        return true;
    }

    private static bool TryFindSameItemInContainer(IInventoryItem item, InventoryContainer container, out int containerIndex)
    {
        containerIndex = -1;

        for (int i = 0; i < container.SlotCount; i++)
        {
            IInventoryItem containerItem = container.GetSlot(i).Item;

            if (containerItem == null)
                continue;

            if (ReferenceEquals(item, containerItem))
            {
                containerIndex = i;
                return true;
            }
        }

        return false;
    }


}
