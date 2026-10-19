public class GameSaveData : SaveData
{
    public InventorySaveData Bag { get; set; }
    public InventorySaveData Chest { get; set; }
    public EquipmentSaveData Equipment { get; set; }
    public int?[] QuickslotsBagIndices { get; set; }
}
