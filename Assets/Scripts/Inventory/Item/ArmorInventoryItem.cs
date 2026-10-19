using System;

// 가방과 장비 칸이 동일한 보호 장비 인스턴스를 옮겨 보관한다.
public class ArmorInventoryItem : DefinitionInventoryItem
{
    public ArmorInstance Armor { get; }

    public float CurrentDurability => Armor.CurrentDurability;

    public ArmorInventoryItem(ArmorInstance armor) : base(armor?.Definition ?? throw new ArgumentNullException(nameof(armor)), 1)
    {
        Armor = armor;
    }
}
