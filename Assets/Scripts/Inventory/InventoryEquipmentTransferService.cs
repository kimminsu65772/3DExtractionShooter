using UnityEngine;

public class InventoryEquipmentTransferService
{
    // 인벤토리에서 장비 칸으로 무기를 이동하거나 교체하는 메서드
    public bool TryMoveOrSwapToEquipment(InventoryContainer inventory, int inventoryIndex, WeaponEquipment equipment, int equipmentIndex)
    {
        // 두 보관함이 유효한지 확인한다.
        if (inventory == null || equipment == null)
        {
            Debug.LogError("Inventory or Equipment is null.");
            return false;
        }

        if (inventoryIndex < 0 || inventoryIndex >= inventory.SlotCount)
            return false;

        if (equipmentIndex < 0 || equipmentIndex >= equipment.SlotCount)
            return false;

        InventorySlot inventorySlot = inventory.GetSlot(inventoryIndex);
        IInventoryItem item = inventorySlot.Item;

        if (item is not WeaponInventoryItem weaponItem)
        {
            Debug.LogError("The item in the inventory slot is not a weapon.");
            return false;
        }

        WeaponInventoryItem equippedWeapon = equipment.GetItem(equipmentIndex);

        if (equippedWeapon != null && ReferenceEquals(weaponItem.GetWeapon(), equippedWeapon.GetWeapon()))
        {
            Debug.Log("The Item in the inventory slot is the same as the equipped weapon.");
            return false;
        }

        inventorySlot.SetItem(equippedWeapon);
        equipment.SetItem(equipmentIndex, weaponItem);
        return true;
    }

    // 장비 칸에서 인벤토리로 무기를 이동하는 메서드
    public bool TryMoveToInventory(WeaponEquipment equipment, int equipmentIndex, InventoryContainer inventory, int inventoryIndex)
    {
        if (inventory == null || equipment == null)
        {
            Debug.LogError("Inventory or Equipment is null.");
            return false;
        }

        if (inventoryIndex < 0 || inventoryIndex >= inventory.SlotCount)
            return false;

        if (equipmentIndex < 0 || equipmentIndex >= equipment.SlotCount)
            return false;

        WeaponInventoryItem weapon = equipment.GetItem(equipmentIndex);
        
        if (weapon == null)
            return false;

        InventorySlot inventorySlot = inventory.GetSlot(inventoryIndex);
        IInventoryItem item = inventorySlot.Item;

        if (item != null && item is not WeaponInventoryItem)
            return false;

        WeaponInventoryItem weaponInventoryItem = item as WeaponInventoryItem;

        if (weaponInventoryItem != null && ReferenceEquals(weapon.GetWeapon(), weaponInventoryItem.GetWeapon()))
        {
            Debug.Log("The equipped weapon is the same as the item in the inventory slot.");
            return false;
        }

        inventorySlot.SetItem(weapon);

        // 어차피 인벤토리 아이템이 null이면 장비 칸에 무기가 없다는 뜻이 되므로, null 확인을 하지 않고 바로 장비 칸에 세팅한다.
        equipment.SetItem(equipmentIndex, weaponInventoryItem);

        return true;
    }
}
