using System;

// 새 종류의 아이템을 기존 인벤토리 슬롯에 보관한다. Definition과 현재 수량을 분리한다.
public class DefinitionInventoryItem : IInventoryItem
{
    public ItemDefinition Definition { get; }
    public ItemType Type => Definition.Type;
    public int ItemId => Definition.itemId;
    public int Count { get; private set; }
    public int RemainingCapacity => Definition.maxStackCount - Count;

    public DefinitionInventoryItem(ItemDefinition definition, int count = 1)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (count <= 0 || count > definition.maxStackCount) throw new ArgumentOutOfRangeException(nameof(count));
        Count = count;
    }

    // 받을 수 있는 수량만 추가하고 실제 들어간 수량을 반환한다.
    public int AddUpTo(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        int accepted = Math.Min(amount, RemainingCapacity);
        Count += accepted;
        return accepted;
    }

    // 회복 사용 완료 등 실제 소비가 확정된 시점에만 수량을 줄인다.
    public int TakeUpTo(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        int taken = Math.Min(amount, Count);
        Count -= taken;
        return taken;
    }
}
