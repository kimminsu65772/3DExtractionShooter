public class InventorySlot
{
    // 슬롯이 담고있는 아이템
    private IInventoryItem item;

    // null은 비어있는 슬롯을 의미.
    public IInventoryItem Item => item;

    public bool IsEmpty => item == null;

    // internal: 같은 어셈블리 내에서만 접근 가능하도록 제한
    internal void SetItem(IInventoryItem newItem)
    {
        item = newItem;
    }
}
