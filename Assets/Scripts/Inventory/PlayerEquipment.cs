using UnityEngine;

public class PlayerEquipment : MonoBehaviour, IProtectionProvider
{
    [Header("참조 설정")]
    [SerializeField] private PlayerWeaponHolder weaponHolder;
    [SerializeField] private PlayerReloadController reloadController;
    [SerializeField] private PlayerAttackController attackController;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerInventory inventory;

    private readonly WeaponEquipment weaponEquipment = new();
    private readonly ProtectionEquipment protectionEquipment = new();
    private int selectedWeaponSlotIndex = 0;

    private readonly InventoryEquipmentTransferService equipmentTransferService = new();

    public WeaponEquipment WeaponEquipment => weaponEquipment;
    public ProtectionEquipment ProtectionEquipment => protectionEquipment;
    public int SelectedWeaponSlotIndex => selectedWeaponSlotIndex;

    // INV-02: 소유권 변경이 확정된 뒤에만 외형에 알린다. 내구도 변경과는 별개다.
    public event System.Action OnProtectionChanged;

    public void ClearForDeath()
    {
        // 일반 장비 이동 API는 Dead에서 거부되므로 소멸 전용 경로에서 정리한다.
        reloadController.CancelReload();
        attackController.CancelPendingFire();
        // 풀에서 빌린 외형을 플레이어와 함께 파괴하지 않고 먼저 반환한다.
        weaponHolder.Unequip();
        for (int i = 0; i < weaponEquipment.SlotCount; i++)
            weaponEquipment.SetItem(i, null);

        protectionEquipment.SetItem(ItemType.Armor, null);
        protectionEquipment.SetItem(ItemType.Helmet, null);
        selectedWeaponSlotIndex = 0;
        OnProtectionChanged?.Invoke();
    }

    private void Awake()
    {
        ResolveReferences();
        AlertReferencesError();
    }

    public bool TrySelectWeaponSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponEquipment.SlotCount)
        {
            Debug.LogWarning($"잘못된 슬롯 인덱스 {slotIndex}입니다.", this);
            return false;
        }

        if (!AlertReferencesError())
        {
            Debug.LogWarning($"참조 오류로 인해 슬롯 {slotIndex} 선택을 수행할 수 없습니다.", this);
            return false;
        }

        if (stateMachine.CurrentState != PlayerActionState.Free && stateMachine.CurrentState != PlayerActionState.Reload)
        {
            Debug.Log($"현재 상태 {stateMachine.CurrentState}에서는 슬롯 {slotIndex} 선택을 수행할 수 없습니다.", this);
            return false;
        }

        WeaponInventoryItem selectedWeapon = weaponEquipment.GetItem(slotIndex);

        if (selectedWeapon == null)
        {
            // 이미 빈손이라면 다시 해제하거나 진행 중인 동작을 취소할 필요가 없다.
            if (weaponHolder.CurrentWeaponInstance == null)
            {
                selectedWeaponSlotIndex = slotIndex;
                inventory.NotifyOwnedItemsChanged(); // [DATA-01 변경] 빈 칸 선택도 복원할 선택 상태다.
                return true;
            }

            weaponHolder.Unequip();
        }
        else
        {
            WeaponInstance weapon = selectedWeapon.GetWeapon();

            // 이미 선택된 무기라면 슬롯 인덱스만 업데이트하고 true 반환
            if (ReferenceEquals(weapon, weaponHolder.CurrentWeaponInstance))
            {
                selectedWeaponSlotIndex = slotIndex;
                inventory.NotifyOwnedItemsChanged();
                return true;
            }

            if (weapon != null)
            {
                if (!weaponHolder.TryEquip(weapon))
                {
                    Debug.LogWarning($"슬롯 {slotIndex}의 무기 {weapon.Definition.displayName} 장착에 실패했습니다.", this);
                    return false;
                }
            }
            else
            {
                weaponHolder.Unequip();
            }
        }

        reloadController.CancelReload();
        attackController.CancelPendingFire();
        selectedWeaponSlotIndex = slotIndex;
        inventory.NotifyOwnedItemsChanged();

        return true;
    }

    // 인벤토리 <-> 장비 슬롯 이동 혹은 교환을 시도하는 메서드.
    public bool TryMoveBagWeaponToEquipment(int inventorySlotIndex, int equipmentIndex)
    {
        return TryMoveWeaponToEquipment(inventory != null ? inventory.InventoryContainer : null, inventorySlotIndex, equipmentIndex);
    }

    // 상자와 가방의 무기를 같은 경로로 옮긴다. 선택 무기 외형 준비 실패 시 양쪽 소유권을 복구한다.
    public bool TryMoveWeaponToEquipment(InventoryContainer source, int inventorySlotIndex, int equipmentIndex)
    {
        if (!AlertReferencesError() || source == null || weaponEquipment == null)
            return false;
        if (inventorySlotIndex < 0 || inventorySlotIndex >= source.SlotCount || equipmentIndex < 0 || equipmentIndex >= weaponEquipment.SlotCount)
            return false;
        if (stateMachine.CurrentState != PlayerActionState.Free && stateMachine.CurrentState != PlayerActionState.Reload)
            return false;

        InventorySlot slot = source.GetSlot(inventorySlotIndex);
        IInventoryItem inventoryItem = slot.Item;

        if (inventoryItem is not WeaponInventoryItem weaponItem)
        {
            Debug.LogWarning($"인벤토리 슬롯 {inventorySlotIndex}의 아이템은 무기가 아닙니다.", this);
            return false;
        }

        WeaponInventoryItem targetWeapon = weaponEquipment.GetItem(equipmentIndex);

        if (!equipmentTransferService.TryMoveOrSwapToEquipment(source, inventorySlotIndex, weaponEquipment, equipmentIndex))
        {
            Debug.LogWarning($"인벤토리 슬롯 {inventorySlotIndex}의 무기 {weaponItem.GetWeapon().Definition.displayName}을 장비 슬롯 {equipmentIndex}로 이동/교환하는 데 실패했습니다.", this);
            return false;
        }

        if (equipmentIndex == selectedWeaponSlotIndex)
        {
            if (!TrySelectWeaponSlot(equipmentIndex))
            {
                Debug.LogWarning($"장비 슬롯 {equipmentIndex}의 무기 선택에 실패했습니다.", this);
                // 이동/교환 실패 시 원래 상태로 되돌리기
                slot.SetItem(inventoryItem);
                weaponEquipment.SetItem(equipmentIndex, targetWeapon);
                return false;
            }
        }

        // [DATA-01 변경] 외형 선택·되돌리기까지 성공한 소유 상태를 통지한다.
        inventory.RefreshOwnedItemReferences();
        return true;
    }

    public bool TryMoveEquipmentWeaponToBag(int equipmentIndex, int inventorySlotIndex)
    {
        if (!AlertReferencesError() || inventory.InventoryContainer == null || weaponEquipment == null)
            return false;
        if (equipmentIndex < 0 || equipmentIndex >= weaponEquipment.SlotCount || inventorySlotIndex < 0 || inventorySlotIndex >= inventory.InventoryContainer.SlotCount)
            return false;
        if (stateMachine.CurrentState != PlayerActionState.Free && stateMachine.CurrentState != PlayerActionState.Reload)
            return false;

        WeaponInventoryItem weaponItem = weaponEquipment.GetItem(equipmentIndex);
        InventorySlot slot = inventory.InventoryContainer.GetSlot(inventorySlotIndex);
        IInventoryItem previousInventoryItem = slot.Item;

        if (weaponItem == null)
        {
            Debug.LogWarning($"장비 슬롯 {equipmentIndex}에는 무기가 없습니다.", this);
            return false;
        }

        if (!equipmentTransferService.TryMoveToInventory(weaponEquipment, equipmentIndex, inventory.InventoryContainer, inventorySlotIndex))
        {
            Debug.LogWarning($"장비 슬롯 {equipmentIndex}의 무기 {weaponItem.GetWeapon().Definition.displayName}을 인벤토리 슬롯 {inventorySlotIndex}로 이동하는 데 실패했습니다.", this);
            return false;
        }

        if (equipmentIndex == selectedWeaponSlotIndex)
        {
            if (!TrySelectWeaponSlot(equipmentIndex))
            {
                Debug.LogWarning($"장비 슬롯 {equipmentIndex}의 무기 선택에 실패했습니다.", this);
                // 이동 실패 시 원래 상태로 되돌리기
                weaponEquipment.SetItem(equipmentIndex, weaponItem);
                slot.SetItem(previousInventoryItem);
                return false;
            }
        }
        inventory.RefreshOwnedItemReferences();
        return true;
    }

    // 체력은 피격 부위에 해당하는 장비만 조회한다. 장비가 없으면 보호하지 않는다.
    public ArmorInstance GetProtection(HitPart hitPart)
    {
        return hitPart == HitPart.Head ? protectionEquipment.Helmet?.Armor : protectionEquipment.Armor?.Armor;
    }

    // 외형 준비가 필요 없는 보호 장비는 소유권 이동 성공 즉시 효력이 바뀐다.
    public bool TryMoveProtectionToEquipment(InventoryContainer source, int sourceIndex, ItemType type)
    {
        if (!CanChangeProtection() || source == null || sourceIndex < 0 || sourceIndex >= source.SlotCount || (type != ItemType.Armor && type != ItemType.Helmet)) return false;
        InventorySlot slot = source.GetSlot(sourceIndex);
        IInventoryItem incoming = slot.Item;
        ArmorInventoryItem previous = protectionEquipment.GetItem(type);
        if (!equipmentTransferService.TryMoveOrSwapToEquipment(source, sourceIndex, protectionEquipment, type)) return false;
        if (inventory != null && inventory.TryRefreshCapacity())
        {
            inventory.RefreshOwnedItemReferences();
            OnProtectionChanged?.Invoke();
            return true;
        }
        // 칸 감소에 필요한 드롭이 준비되지 않으면 양쪽 장비 이동도 되돌린다.
        slot.SetItem(incoming);
        protectionEquipment.SetItem(type, previous);
        return false;
    }

    public bool TryMoveProtectionToBag(ItemType type, int inventoryIndex)
    {
        if (!CanChangeProtection() || inventory?.InventoryContainer == null || (type != ItemType.Armor && type != ItemType.Helmet)) return false;
        InventoryContainer bag = inventory.InventoryContainer;
        if (inventoryIndex < 0 || inventoryIndex >= bag.SlotCount) return false;
        InventorySlot slot = bag.GetSlot(inventoryIndex);
        IInventoryItem previousBag = slot.Item;
        ArmorInventoryItem previousEquipment = protectionEquipment.GetItem(type);
        if (!equipmentTransferService.TryMoveToInventory(protectionEquipment, type, bag, inventoryIndex)) return false;
        if (inventory.TryRefreshCapacity())
        {
            inventory.RefreshOwnedItemReferences();
            OnProtectionChanged?.Invoke();
            return true;
        }
        slot.SetItem(previousBag);
        protectionEquipment.SetItem(type, previousEquipment);
        return false;
    }

    public bool TryDropWeapon(int index)
    {
        if (!CanChangeProtection() || !AlertReferencesError() || weaponEquipment == null || index < 0 || index >= weaponEquipment.SlotCount) return false;
        PlayerWorldItemDropper dropper = GetComponent<PlayerWorldItemDropper>();
        WeaponInventoryItem item = weaponEquipment.GetItem(index);
        if (item == null || dropper == null || !dropper.TryPrepare(new[] { item }, out PlayerWorldItemDropper.PreparedDrop drop)) return false;
        weaponEquipment.SetItem(index, null);
        if (index == selectedWeaponSlotIndex && !TrySelectWeaponSlot(index))
        {
            weaponEquipment.SetItem(index, item);
            drop.Cancel();
            return false;
        }
        drop.Commit();
        inventory.RefreshOwnedItemReferences();
        return true;
    }

    public bool TryDropProtection(ItemType type)
    {
        if (!CanChangeProtection() || inventory == null || (type != ItemType.Armor && type != ItemType.Helmet)) return false;
        PlayerWorldItemDropper dropper = GetComponent<PlayerWorldItemDropper>();
        ArmorInventoryItem item = protectionEquipment.GetItem(type);
        if (item == null || dropper == null || !dropper.TryPrepare(new[] { item }, out PlayerWorldItemDropper.PreparedDrop drop)) return false;
        protectionEquipment.SetItem(type, null);
        if (!inventory.TryRefreshCapacity())
        {
            protectionEquipment.SetItem(type, item);
            drop.Cancel();
            return false;
        }
        drop.Commit();
        inventory.RefreshOwnedItemReferences();
        OnProtectionChanged?.Invoke();
        return true;
    }

    public bool TryApplyRestoredEquipment(WeaponEquipment restoredWeapons, ProtectionEquipment restoredProtection)
    {
        if (restoredWeapons == null || restoredProtection == null)
            return false;

        if (restoredWeapons.SlotCount != weaponEquipment.SlotCount || restoredProtection.SlotCount != protectionEquipment.SlotCount)
        {
            Debug.LogWarning("복원된 장비 슬롯 수가 현재 장비 슬롯 수와 일치하지 않습니다.", this);
            return false;
        }

        for (int i = 0; i < weaponEquipment.SlotCount; i++)
            weaponEquipment.SetItem(i, restoredWeapons.GetItem(i));

        for (int i = 0; i < protectionEquipment.SlotCount; i++)
            protectionEquipment.SetItem(i, restoredProtection.GetItem(i));

        OnProtectionChanged?.Invoke();
        return true;
    }

    private bool CanChangeProtection()
    {
        // 회피나 사망 중에는 무기와 동일하게 장비 교체를 차단한다.
        return stateMachine != null && (stateMachine.CurrentState == PlayerActionState.Free || stateMachine.CurrentState == PlayerActionState.Reload);
    }

    private void ResolveReferences()
    {
        if (weaponHolder == null)
        {
            weaponHolder = GetComponent<PlayerWeaponHolder>();
        }
        if (reloadController == null)
        {
            reloadController = GetComponent<PlayerReloadController>();
        }
        if (attackController == null)
        {
            attackController = GetComponent<PlayerAttackController>();
        }
        if (stateMachine == null)
        {
            stateMachine = GetComponent<PlayerStateMachine>();
        }
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }
    }

    private bool AlertReferencesError()
    {
        bool noError = true;
        if (weaponHolder == null)
        {
            noError = false;
            Debug.LogError("PlayerWeaponHolder 참조가 설정되지 않았습니다.", this);
        }
        if (reloadController == null)
        {
            noError = false;
            Debug.LogError("PlayerReloadController 참조가 설정되지 않았습니다.", this);
        }
        if (attackController == null)
        {
            noError = false;
            Debug.LogError("PlayerAttackController 참조가 설정되지 않았습니다.", this);
        }
        if (stateMachine == null)
        {
            noError = false;
            Debug.LogError("PlayerStateMachine 참조가 설정되지 않았습니다.", this);
        }
        if (inventory == null)
        {
            noError = false;
            Debug.LogError("PlayerInventory 참조가 설정되지 않았습니다.", this);
        }

        return noError;
    }
}
