public class ItemSaveData
{
    public ItemType Type { get; set; }

    public int ItemId { get; set; }

    public int Count { get; set; }

    public WeaponSaveData WeaponAmmo { get; set; }

    public float? CurrentDurability { get; set; }

    public float RemainingUseRatio { get; set; }


}
