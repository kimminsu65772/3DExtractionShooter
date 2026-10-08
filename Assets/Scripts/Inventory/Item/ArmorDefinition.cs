using System;

// 몸통 보호 장비의 기본 성능을 정의한다. 현재 내구도는 ArmorInstance가 보관한다.
[Serializable]
public class ArmorDefinition : ProtectionDefinition
{
    public int backpackCapacityBonus;
    public override ItemType Type => ItemType.Armor;
}
