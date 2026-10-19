using System;

// 몸통 방어구와 헬멧을 각각 한 칸에 보관한다. 외형과 피해 계산은 담당하지 않는다.
public class ProtectionEquipment
{
    public const int ArmorSlotIndex = 0;
    public const int HelmetSlotIndex = 1;

    // 무기 장비와 같은 고정 슬롯 모델을 사용한다. 0번은 방어구, 1번은 헬멧이다.
    private readonly ArmorInventoryItem[] protectionSlots = new ArmorInventoryItem[2];

    public int SlotCount => protectionSlots.Length;

    // 기존 피격·무게·UI 호출도 같은 배열의 객체를 읽는다. 별도 상태를 중복 보관하지 않는다.
    public ArmorInventoryItem Armor => GetItem(ArmorSlotIndex);
    public ArmorInventoryItem Helmet => GetItem(HelmetSlotIndex);

    public ArmorInventoryItem GetItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= protectionSlots.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "보호 장비 칸의 범위를 벗어났다.");

        return protectionSlots[slotIndex];
    }

    public ArmorInventoryItem GetItem(ItemType type)
    {
        return GetItem(GetSlotIndex(type));
    }

    // 소유권 이동 담당자만 칸을 변경한다. 다른 종류를 잘못 장착하지 않는다.
    internal void SetItem(int slotIndex, ArmorInventoryItem item)
    {
        if (slotIndex < 0 || slotIndex >= protectionSlots.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "보호 장비 칸의 범위를 벗어났다.");

        ItemType slotType = slotIndex == ArmorSlotIndex ? ItemType.Armor : ItemType.Helmet;
        if (item != null && item.Type != slotType)
            throw new ArgumentException("장비 칸과 아이템 종류가 다르다.", nameof(item));

        protectionSlots[slotIndex] = item;
    }

    internal void SetItem(ItemType type, ArmorInventoryItem item)
    {
        SetItem(GetSlotIndex(type), item);
    }

    private static int GetSlotIndex(ItemType type)
    {
        if (type == ItemType.Armor) return ArmorSlotIndex;
        if (type == ItemType.Helmet) return HelmetSlotIndex;
        throw new ArgumentOutOfRangeException(nameof(type), "보호 장비 종류가 아니다.");
    }
}
