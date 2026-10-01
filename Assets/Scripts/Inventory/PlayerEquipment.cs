using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [Header("참조 설정")]
    [SerializeField] private PlayerWeaponHolder weaponHolder;
    [SerializeField] private PlayerReloadController reloadController;
    [SerializeField] private PlayerAttackController attackController;
    [SerializeField] private PlayerStateMachine stateMachine;

    private WeaponEquipment weaponEquipment;
    private int selectedWeaponSlotIndex = -1;

    public WeaponEquipment WeaponEquipment => weaponEquipment;
    public int SelectedWeaponSlotIndex => selectedWeaponSlotIndex;

    private void Awake()
    {
        weaponEquipment = new WeaponEquipment();
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

        if (stateMachine.CurrentState != PlayerActionState.Free || stateMachine.CurrentState != PlayerActionState.Reload)
        {
            Debug.Log($"현재 상태 {stateMachine.CurrentState}에서는 슬롯 {slotIndex} 선택을 수행할 수 없습니다.", this);
            return false;
        }

        WeaponInventoryItem selectedWeapon = weaponEquipment.GetItem(slotIndex);

        if (selectedWeapon == null)
        {
            Debug.Log($"슬롯 {slotIndex}에는 장착 가능한 무기가 없습니다.", this);
            return false;
        }

        WeaponInstance weapon = selectedWeapon.GetWeapon();

        // 이미 선택된 무기라면 슬롯 인덱스만 업데이트하고 true 반환
        if (ReferenceEquals(weapon, weaponHolder.CurrentWeapon))
        {
            selectedWeaponSlotIndex = slotIndex;
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

        reloadController.CancelReload();
        attackController.CancelPendingFire();
        selectedWeaponSlotIndex = slotIndex;

        return true;
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

        return noError;
    }
}
