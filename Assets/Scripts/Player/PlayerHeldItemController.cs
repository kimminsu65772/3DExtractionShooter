using UnityEngine;

[RequireComponent(typeof(PlayerItemUseController))]
public class PlayerHeldItemController : MonoBehaviour
{
    // 가방 아이템을 가리키는 퀵슬롯을 조회한다.
    [SerializeField] private PlayerItemQuickslots itemQuickslots;

    // 소모품을 선택할 때 손에 든 총의 외형을 해제한다.
    [SerializeField] private PlayerWeaponHolder weaponHolder;

    // 이전 총의 장전과 예약 발사를 취소한다.
    [SerializeField] private PlayerReloadController reloadController;
    [SerializeField] private PlayerAttackController attackController;

    // 회피·피격·사망 중에는 선택을 변경하지 않는다.
    [SerializeField] private PlayerStateMachine stateMachine;

    // 아이템 종류를 몰라도 공통 창구를 통해 사용과 취소를 요청한다.
    [SerializeField] private PlayerItemUseController itemUse;

    [SerializeField] private PlayerInputHandler input;

    // -1: 슬롯을 선택하지 않은 상태
    public int SelectedItemSlotIndex { get; private set; } = -1;

    // 손에 선택한 실제 아이템을 기억한다. 가방에서 꺼내지는 않는다.
    public IInventoryItem SelectedItem { get; private set; }

    private void Awake()
    {
        RegisterReferences();

        if (AlertReferenceError())
            return;
    }

    private void Update()
    {
        // 선택한 소모품이나 입력 담당자가 없으면 사용 입력을 처리하지 않는다.
        if (SelectedItem == null || input == null) return;

        // UI를 조작하는 클릭은 아이템 사용으로 전달하지 않는다.
        if (input.IsUIInputBlocked) return;

        // 새 클릭이 들어왔을 때만 선택한 아이템의 사용을 요청한다.
        if (input.ConsumeAttackInput())
            TryUseSelectedItem();
    }

    public bool TrySelectItemSlot(int slotIndex)
    {
        if (AlertReferenceError())
            return false;

        if (stateMachine.CurrentState != PlayerActionState.Free && stateMachine.CurrentState != PlayerActionState.Reload)
            return false;
        itemQuickslots.RemoveInvalidRegistrations();
        
        ItemQuickslots quickslots = itemQuickslots.Quickslots;
        if (slotIndex < 0 || slotIndex >= quickslots.SlotCount)
            return false;
        IInventoryItem item = quickslots.GetItem(slotIndex);
        if (item == null)
            return false;
        if (SelectedItemSlotIndex == slotIndex && item == SelectedItem)
        {
            return true;
        }
        reloadController.CancelReload();
        attackController.CancelPendingFire();
        itemUse.CancelUse();
        weaponHolder.Unequip();
        SelectedItemSlotIndex = slotIndex;
        SelectedItem = item;

        return true;
    }
    public void ClearItemSelection()
    {
        // 진행 중인 소모품 사용을 취소한다.
        itemUse?.CancelUse();

        // 손에 선택했던 소모품의 참조와 칸 번호만 지운다.
        SelectedItem = null;
        SelectedItemSlotIndex = -1;
    }

    public bool TryUseSelectedItem()
    {
        if (AlertReferenceError())
            return false;
        if (SelectedItem == null)
            return false;
        // 가방에서 빠지거나 소진된 아이템을 사용 요청 전에 정리한다.
        itemQuickslots.RemoveInvalidRegistrations();
        if (SelectedItemSlotIndex < 0 || SelectedItemSlotIndex >= itemQuickslots.Quickslots.SlotCount)
        {
            ClearItemSelection();
            return false;
        }

        IInventoryItem item = itemQuickslots.Quickslots.GetItem(SelectedItemSlotIndex);
        if (!ReferenceEquals(SelectedItem, item))
        {
            ClearItemSelection();
            return false;
        }
        // 구체적인 정의나 효과는 여기에서 검사하지 않고 사용 담당자에게 맡긴다.
        return itemUse.TryUse(item);
    }

    private void RegisterReferences()
    {
        if (itemQuickslots == null)
            itemQuickslots = GetComponent<PlayerItemQuickslots>();
        if (weaponHolder == null)
            weaponHolder = GetComponent<PlayerWeaponHolder>();
        if (reloadController == null)
            reloadController = GetComponent<PlayerReloadController>();
        if (attackController == null)
            attackController = GetComponent<PlayerAttackController>();
        if (stateMachine == null)
            stateMachine = GetComponent<PlayerStateMachine>();  
        if (itemUse == null)
            itemUse = GetComponent<PlayerItemUseController>();
        if (input == null)
            input = GetComponent<PlayerInputHandler>();
    }
    private bool AlertReferenceError()
    {
        if (itemQuickslots == null)
        {
            Debug.LogError($"{nameof(PlayerHeldItemController)}.{nameof(itemQuickslots)} is not assigned.");
            return true;
        }

        if (weaponHolder == null)
        {
            Debug.LogError($"{nameof(PlayerHeldItemController)}.{nameof(weaponHolder)} is not assigned.");
            return true;
        }

        if (reloadController == null)
        {
            Debug.LogError($"{nameof(PlayerHeldItemController)}.{nameof(reloadController)} is not assigned.");
            return true;
        }

        if (attackController == null)
        {
            Debug.LogError($"{nameof(PlayerHeldItemController)}.{nameof(attackController)} is not assigned.");
            return true;
        }

        if (stateMachine == null)
        {
            Debug.LogError($"{nameof(PlayerHeldItemController)}.{nameof(stateMachine)} is not assigned.");
            return true;
        }

        if (itemUse == null)
        {
            Debug.LogError($"{nameof(PlayerHeldItemController)}.{nameof(itemUse)} is not assigned.");
            return true;
        }

        if (input == null)
        {
            Debug.LogError($"{nameof(PlayerHeldItemController)}.{nameof(input)} is not assigned.");
            return true;
        }

        return false;
    }
}
