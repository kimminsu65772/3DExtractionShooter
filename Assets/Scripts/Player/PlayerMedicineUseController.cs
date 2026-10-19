using UnityEngine;

// 회복 아이템의 사용 시간을 관리한다. UI의 단순 클릭에서는 호출하지 않는다.
[DisallowMultipleComponent]
public class PlayerMedicineUseController : MonoBehaviour, IItemUseHandler
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private HealthComponent health;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerWeaponHolder weaponHolder;
    [SerializeField] private PlayerInputHandler input;
    private DefinitionInventoryItem usingItem;
    private WeaponInstance weaponAtStart;
    private float completeTime;
    private float startTime;
    private PlayerItemQuickslots itemQuickslots;
    public bool IsUsing => usingItem != null;
    public float UseRemainingTime => IsUsing ? Mathf.Max(0f, completeTime - Time.time) : 0f;
    public float UseProgress => !IsUsing ? 0f : completeTime <= startTime ? 1f :
        Mathf.Clamp01((Time.time - startTime) / (completeTime - startTime));
    public event System.Action<bool> OnUseStateChanged;
    public event System.Action<float> OnUseProgressChanged;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (health == null) health = GetComponent<HealthComponent>();
        if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
        if (weaponHolder == null) weaponHolder = GetComponent<PlayerWeaponHolder>();
        if (input == null) input = GetComponent<PlayerInputHandler>();
        itemQuickslots = GetComponent<PlayerItemQuickslots>();
    }

    // 회복약의 종류 판정은 회복약 담당자 안에만 둔다.
    public bool CanHandle(IInventoryItem item)
    {
        return item is DefinitionInventoryItem defined && defined.Definition is MedicineDefinition;
    }

    public bool TryUse(IInventoryItem item)
    {
        return isActiveAndEnabled && CanHandle(item) && TryStartUse((DefinitionInventoryItem)item);
    }

    // 소유한 회복 아이템을 손에서 사용하기로 확정한 시점에 호출한다.
    public bool TryStartUse(DefinitionInventoryItem item)
    {
        if (IsUsing || item == null || item.Count <= 0 || item.Definition is not MedicineDefinition medicine) return false;
        // 소비 비율이 있는 키트는 반드시 개별 잔여량을 가진 객체로 사용한다.
        if (medicine.useCostRatio.HasValue && (item is not MedicineInventoryItem kit || kit.IsDepleted)) return false;
        if (health == null || health.IsDead || health.CurrentHealth >= health.MaxHealth || stateMachine == null || stateMachine.CurrentState != PlayerActionState.Free) return false;
        if (input != null && input.IsUIInputBlocked) return false;
        if (FindOwnedSlot(item) == null || medicine.useDuration <= 0f || medicine.healAmount <= 0f) return false;
        usingItem = item;
        weaponAtStart = weaponHolder != null ? weaponHolder.CurrentWeaponInstance : null;
        startTime = Time.time;
        completeTime = startTime + medicine.useDuration;
        OnUseStateChanged?.Invoke(true);
        OnUseProgressChanged?.Invoke(0f);
        return true;
    }

    private void Update()
    {
        if (!IsUsing) return;
        // 이동은 허용하고, 회피·사망·무기 교체·소유권 상실은 사용을 취소한다.
        InventorySlot slot = FindOwnedSlot(usingItem);
        WeaponInstance currentWeapon = weaponHolder != null ? weaponHolder.CurrentWeaponInstance : null;
        if (health == null || health.IsDead || stateMachine == null || stateMachine.CurrentState != PlayerActionState.Free || !ReferenceEquals(currentWeapon, weaponAtStart) || slot == null || usingItem.Count <= 0)
        {
            CancelUse();
            return;
        }
        // UI는 별도 타이머 없이 실제 사용 완료 시각을 기준으로 표시한다.
        OnUseProgressChanged?.Invoke(UseProgress);
        if (Time.time < completeTime) return;
        var medicine = (MedicineDefinition)usingItem.Definition;
        // 대기 중 다른 효과로 체력이 가득 찼다면 회복 아이템을 낭비하지 않는다.
        float missingHealth = health.MaxHealth - health.CurrentHealth;
        float restored = 0f;
        if (missingHealth > 0f)
        {
            if (usingItem is MedicineInventoryItem kit)
            {
                // 사용 완료 시점의 부족한 체력만큼 회복하고 그에 대응하는 잔여량만 소비한다.
                restored = kit.ConsumeForHealing(missingHealth);
                if (kit.IsDepleted) { kit.TakeUpTo(1); slot.SetItem(null); }
            }
            else if (usingItem.TakeUpTo(1) == 1)
            {
                // 붕대는 일부만 회복해도 한 개를 소비한다.
                restored = Mathf.Min(missingHealth, medicine.healAmount);
                if (usingItem.Count == 0) slot.SetItem(null);
            }
            // 실제 소비가 끝난 순간에만 소진된 아이템의 퀵슬롯 등록을 정리한다.
            itemQuickslots?.RemoveInvalidRegistrations();
            if (restored > 0f)
            {
                health.Restore(restored);
                // [DATA-01 변경] 부분 사용한 키트의 잔여량도 로비 저장에 반영한다.
                inventory.RefreshOwnedItemReferences();
            }
        }
        CancelUse();
    }

    private InventorySlot FindOwnedSlot(DefinitionInventoryItem item)
    {
        InventoryContainer bag = inventory != null ? inventory.InventoryContainer : null;
        if (bag == null) return null;
        for (int i = 0; i < bag.SlotCount; i++)
            if (ReferenceEquals(bag.GetSlot(i).Item, item)) return bag.GetSlot(i);
        return null;
    }

    public void CancelUse()
    {
        bool wasUsing = IsUsing;
        usingItem = null;
        weaponAtStart = null;
        completeTime = 0f;
        startTime = 0f;
        if (!wasUsing) return;
        OnUseStateChanged?.Invoke(false);
        OnUseProgressChanged?.Invoke(0f);
    }
    private void OnDisable() { CancelUse(); }
}
