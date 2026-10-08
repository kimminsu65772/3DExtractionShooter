using UnityEngine;

public class WeaponEquipment
{
    // 원작을 따라 무기 슬롯은 2개로 제한.
    private readonly WeaponInventoryItem[] weaponSlots = new WeaponInventoryItem[2];

    public int SlotCount => weaponSlots.Length;

    public WeaponInventoryItem GetItem(int slotIndex)
    {
        // 배열을 읽기 전에 요청한 칸이 존재하는지 확인
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length)
            throw new System.ArgumentOutOfRangeException(nameof(slotIndex), "무기 장비 칸의 범위를 벗어났습니다.");

        // 실제 보관 중인 아이템을 반환.
        return weaponSlots[slotIndex];
    }

    internal void SetItem(int slotIndex, WeaponInventoryItem item)
    {
        // 배열을 읽기 전에 요청한 칸이 존재하는지 확인
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length)
            throw new System.ArgumentOutOfRangeException(nameof(slotIndex), "무기 장비 칸의 범위를 벗어났습니다.");
        // 실제 보관 중인 아이템을 설정.
        weaponSlots[slotIndex] = item;
    }
}
