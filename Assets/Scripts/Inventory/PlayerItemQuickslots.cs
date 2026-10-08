using UnityEngine;

public class PlayerItemQuickslots : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;

    private readonly ItemQuickslots quickslots = new();

    // UI에서 접근할 수 있도록 Quickslots 프로퍼티 제공
    public ItemQuickslots Quickslots => quickslots;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (inventory == null)
        {
            Debug.LogError("PlayerInventory component is not assigned or found on the GameObject.");
            return;
        }
    }

    private bool CanRegister(IInventoryItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("Cannot register a null item.");
            return false;
        }
        if (item.Type != ItemType.Consumable 
            && item.Type != ItemType.Meds 
            && item.Type != ItemType.Buff 
            && item.Type != ItemType.Throwables)
        {
            Debug.LogWarning($"Item type {item.Type} is not allowed in quickslots.");
            return false;
        }

        InventoryContainer container = inventory != null ? inventory.InventoryContainer : null;

        if (container == null)
        {
            Debug.LogError("InventoryContainer is not initialized.");
            return false;
        }

        for (int i = 0; i < container.SlotCount; i++)
        {
            var slotItem = container.GetSlot(i).Item;
            if (ReferenceEquals(slotItem, item))
            {
                return true;
            }
        }

        return false;
    }
    
    public bool TryRegister(int slotIndex, IInventoryItem item)
    {
        if (slotIndex < 0 || slotIndex >= quickslots.SlotCount)
        {
            Debug.LogWarning($"Slot index {slotIndex} is out of bounds.");
            return false;
        }

        if (!CanRegister(item))
            return false;

        quickslots.SetItem(slotIndex, item);
        return true;
    }

    public bool TryAutoRegister(IInventoryItem item)
    {
        if (!CanRegister(item))
            return false;

        int firstEmptySlot = -1;

        for (int i = 0; i < quickslots.SlotCount; i++)
        {
            IInventoryItem registeredItem = quickslots.GetItem(i);
            if (registeredItem == null)
            {
                // 첫 번째 빈 칸만 기억하고, 뒤쪽의 중복 등록 검사도 계속한다.
                if (firstEmptySlot == -1) 
                    firstEmptySlot = i;
                continue;
            }
            if (registeredItem.Type == item.Type && registeredItem.ItemId == item.ItemId)
            {
                return false;
            }
        }

        if (firstEmptySlot != -1)
        {
            quickslots.SetItem(firstEmptySlot, item);
            return true;
        }

        return false;
    }

    public bool TryUnregister(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= quickslots.SlotCount)
        {
            Debug.LogWarning($"Slot index {slotIndex} is out of bounds.");
            return false;
        }
        if (quickslots.GetItem(slotIndex) == null)
        {
            Debug.LogWarning($"Slot index {slotIndex} is already empty.");
            return false;
        }
        quickslots.SetItem(slotIndex, null);
        return true;
    }

    public void RemoveInvalidRegistrations()
    {
        for (int i = 0; i < quickslots.SlotCount; i++)
        {
            IInventoryItem item = quickslots.GetItem(i);

            // 등록되지 않은 칸은 검사하지 않는다.
            if (item == null) continue;

            // 가방에서 빠졌거나 수량을 모두 사용한 아이템의 등록을 해제한다.
            if (!CanRegister(item) || (item is DefinitionInventoryItem definitionItem && definitionItem.Count <= 0))
                quickslots.SetItem(i, null);
        }
    }
}
