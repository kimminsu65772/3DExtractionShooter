public static class GameSaveDataFactory
{
    public static GameSaveData CreateNewGameData(int bagSlotCount, int chestSlotCount)
    {
        if (bagSlotCount <= 0 || chestSlotCount <= 0)
        {
            return null;
        }

        GameSaveData newData = SaveDataFactory.CreateNewData<GameSaveData>();
        newData.Bag = new InventorySaveData();
        newData.Chest = new InventorySaveData();
        newData.Equipment = new EquipmentSaveData();

        newData.Bag.Slots = new ItemSaveData[bagSlotCount];
        newData.Chest.Slots = new ItemSaveData[chestSlotCount];

        newData.Equipment.WeaponSlots = new ItemSaveData[2];
        newData.Equipment.SelectedWeaponIndex = 0;
        
        newData.QuickslotsBagIndices = new int?[6];

        return newData;
    }
}
