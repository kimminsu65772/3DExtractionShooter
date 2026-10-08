public class ItemQuickslots
{
    // 가방의 아이템을 가리키는 여섯 칸을 만든다. 아이템 자체를 복사하지 않는다.
    private readonly IInventoryItem[] slots = new IInventoryItem[6];

    // UI가 생성할 소모품 퀵슬롯의 개수를 제공한다.
    public int SlotCount => slots.Length;

    public IInventoryItem GetItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length)
        {
            // 존재하지 않는 칸을 요청한 것은 호출 코드의 오류이므로 예외로 알린다.
            throw new System.ArgumentOutOfRangeException(nameof(slotIndex), "소모품 퀵슬롯의 범위를 벗어났다.");
        }

        return slots[slotIndex];
    }

    internal void SetItem(int slotIndex, IInventoryItem item)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(slotIndex), "소모품 퀵슬롯의 범위를 벗어났다.");
        }

        slots[slotIndex] = item;
    }
}
