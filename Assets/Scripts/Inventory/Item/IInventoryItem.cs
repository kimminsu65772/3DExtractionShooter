using UnityEngine;

public interface IInventoryItem
{
    public ItemType Type { get; }
    public int ItemId { get; }
}
