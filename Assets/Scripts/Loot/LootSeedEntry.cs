using System;
using UnityEngine;

[Serializable]
public class LootSeedEntry
{
    [SerializeField] private ItemType itemType;
    [SerializeField] private int itemId;
    [SerializeField] private int count = 1;

    public ItemType ItemType => itemType;
    public int ItemId => itemId;
    public int Count => count;
}
