using System;

// 몸통 방어구와 헬멧을 각각 한 칸에 보관한다. 외형과 피해 계산은 담당하지 않는다.
public class ProtectionEquipment
{
    public ArmorInventoryItem Armor { get; private set; }
    public ArmorInventoryItem Helmet { get; private set; }

    public ArmorInventoryItem GetItem(ItemType type)
    {
        if (type == ItemType.Armor) return Armor;
        if (type == ItemType.Helmet) return Helmet;
        throw new ArgumentOutOfRangeException(nameof(type), "보호 장비 종류가 아니다.");
    }

    // 소유권 이동 담당자만 칸을 변경한다. 다른 종류를 잘못 장착하지 않는다.
    internal void SetItem(ItemType type, ArmorInventoryItem item)
    {
        if (item != null && item.Type != type) throw new ArgumentException("장비 칸과 아이템 종류가 다르다.", nameof(item));
        if (type == ItemType.Armor) Armor = item;
        else if (type == ItemType.Helmet) Helmet = item;
        else throw new ArgumentOutOfRangeException(nameof(type));
    }
}
