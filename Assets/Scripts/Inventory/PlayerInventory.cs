using UnityEngine;
using ExtractionShooter.DataPipeline;

public class PlayerInventory : MonoBehaviour
{
    [Header("기본 칸 수 설정")]
    [SerializeField, Min(1)] private int baseCapacity = 16;
    [SerializeField] private CombatDataCompositionRoot dataRoot;
    [SerializeField] private PlayerWorldItemDropper worldItemDropper;
    [SerializeField, Min(0)] private int additionalBagCapacity;

    private InventoryContainer inventoryContainer;
    private InventoryAmmoStorage ammoStorage;

    public InventoryContainer InventoryContainer => inventoryContainer;
    public InventoryAmmoStorage AmmoStorage => ammoStorage;
    public event System.Action OnCapacityChanged;
    // [DATA-01 변경] 저장 담당자는 변경 통지만 받고, 한 행동의 처리가 끝난 뒤 DTO를 캡처한다.
    public event System.Action OnOwnedItemsChanged;
    internal void NotifyOwnedItemsChanged() => OnOwnedItemsChanged?.Invoke();

    // OnEnable에서 초기화 이벤트를 구독하므로 최초 활성화 전에 연결한다.
    public void SetDataRoot(CombatDataCompositionRoot dataRoot)
    {
        this.dataRoot = dataRoot;
    }

    private void Awake()
    {
        inventoryContainer = new InventoryContainer(Mathf.Max(1, baseCapacity));
        if (worldItemDropper == null) worldItemDropper = GetComponent<PlayerWorldItemDropper>();
        // 드롭은 외형 프리팹 없이도 임시 도형으로 동작한다. 필수 담당자가 빠졌다면 같은 플레이어에 준비한다.
        if (worldItemDropper == null) worldItemDropper = gameObject.AddComponent<PlayerWorldItemDropper>();
    }

    private void Start()
    {
        TryRefreshCapacity();
    }

    public AmmoDefinition FindAmmoDefinition(int ammoId)
    {
        return dataRoot != null && dataRoot.AmmoCatalog != null && dataRoot.AmmoCatalog.TryGet(ammoId, out AmmoDefinition ammo) ? ammo : null;
    }

    public bool OwnsWeapon(WeaponInstance weapon)
    {
        if (weapon == null || inventoryContainer == null) return false;
        for (int i = 0; i < inventoryContainer.SlotCount; i++)
            if (inventoryContainer.GetSlot(i).Item is WeaponInventoryItem item && ReferenceEquals(item.GetWeapon(), weapon)) return true;
        WeaponEquipment equipment = GetComponent<PlayerEquipment>()?.WeaponEquipment;
        if (equipment != null)
            for (int i = 0; i < equipment.SlotCount; i++)
                if (ReferenceEquals(equipment.GetItem(i)?.GetWeapon(), weapon)) return true;
        return false;
    }

    // 가방에 실제 수용된 만큼만 클릭한 동일 총에서 뺀다. 무게는 수용 조건이 아니다.
    public bool TryUnloadWeapon(WeaponInstance weapon, out int unloadedCount)
    {
        unloadedCount = 0;
        if (!OwnsWeapon(weapon) || ammoStorage == null || weapon.AmmoState.IsEmpty) return false;
        // UI가 먼저 해당 총의 장전을 취소해야 한다. 직접 API 호출도 진행 중인 장전과 겹치지 않는다.
        if (GetComponent<PlayerReloadController>()?.IsReloadingWeapon(weapon) == true) return false;
        WeaponAmmoState magazine = weapon.AmmoState;
        if (!ammoStorage.TryAdd(magazine.LoadedAmmoId, magazine.LoadedAmmoCount, out unloadedCount)) return false;
        for (int i = 0; i < unloadedCount; i++) magazine.TryConsumeRound(out _);
        RefreshOwnedItemReferences();
        return true;
    }

    // 장전 완료 시 호출한다. 새 탄약 소비 후 반환 공간을 계산하고 초과분은 기존 월드 드롭으로 넘긴다.
    public bool TryCompleteAmmoExchange(WeaponInstance weapon, int selectedAmmoId, out int loadedCount)
    {
        loadedCount = 0;
        if (!OwnsWeapon(weapon) || dataRoot == null ||
            !AmmoReloadPlan.TryCreate(inventoryContainer, weapon, dataRoot.AmmoCatalog, selectedAmmoId, out AmmoReloadPlan plan)) return false;
        PlayerWorldItemDropper.PreparedDrop drop = null;
        if (plan.Overflow.Count > 0 && (worldItemDropper == null || !worldItemDropper.TryPrepare(plan.Overflow, out drop))) return false;
        if (!OwnsWeapon(weapon) || !ReferenceEquals(GetComponent<PlayerWeaponHolder>()?.CurrentWeaponInstance, weapon) || !plan.TryCommit())
        {
            drop?.Cancel();
            return false;
        }
        drop?.Commit();
        loadedCount = plan.LoadedCount;
        RefreshOwnedItemReferences();
        return true;
    }

    // 가방 아이템 시스템이 추가되면 그 장비 담당자가 제공하는 칸 수를 이 경계로 전달한다.
    public bool TrySetAdditionalBagCapacity(int capacity)
    {
        if (capacity < 0) return false;
        if (!TryResizeForEquipment(capacity)) return false;
        additionalBagCapacity = capacity;
        return true;
    }

    public bool TryRefreshCapacity() => TryResizeForEquipment(additionalBagCapacity);

    public void ClearForDeath()
    {
        // 슬롯을 먼저 비워 외부 UI가 보관한 슬롯 참조에도 아이템이 남지 않게 한다.
        for (int i = 0; i < inventoryContainer.SlotCount; i++)
            inventoryContainer.GetSlot(i).SetItem(null);

        additionalBagCapacity = 0;
        // 사망 소멸은 일반 칸 감소와 달리 넘치는 아이템을 바닥에 드롭하지 않는다.
        inventoryContainer.ApplyResize(new IInventoryItem[Mathf.Max(1, baseCapacity)]);
        OnCapacityChanged?.Invoke();
    }

    // 보너스 칸 수를 포함한 최종 용량을 계산한다. int 범위를 넘어가면 false를 반환한다.
    private bool TryCalculateCapacity(int bagBonus, out int capacity)
    {
        capacity = 0;
        PlayerEquipment equipment = GetComponent<PlayerEquipment>();
        int armorBonus = equipment?.ProtectionEquipment.Armor?.Definition is ArmorDefinition armor ? armor.backpackCapacityBonus : 0;
        long requestedCapacity = (long)baseCapacity + bagBonus + Mathf.Max(0, armorBonus);
        if (requestedCapacity > int.MaxValue) return false;
        capacity = Mathf.Max(1, (int)requestedCapacity);
        return true;
    }

    private bool TryResizeForEquipment(int bagBonus)
    {
        // [DATA-01 변경] 기존의 용량 계산을 공통 메서드로 옮겼다. 아래 실제 정리·드롭 흐름은 그대로 사용한다.
        if (inventoryContainer == null || !TryCalculateCapacity(bagBonus, out int capacity)) return false;
        if (inventoryContainer.SlotCount == capacity) return true;
        InventoryResizePlan plan = new InventoryResizePlan(inventoryContainer, capacity);
        PlayerWorldItemDropper.PreparedDrop drop = null;
        if (plan.Overflow.Count > 0 && (worldItemDropper == null || !worldItemDropper.TryPrepare(plan.Overflow, out drop))) return false;
        if (!plan.TryCommit())
        {
            drop?.Cancel();
            return false;
        }
        drop?.Commit();
        RefreshOwnedItemReferences();
        OnCapacityChanged?.Invoke();
        return true;
    }

    public bool TryDropBagItem(int index)
    {
        if (inventoryContainer == null || index < 0 || index >= inventoryContainer.SlotCount || worldItemDropper == null) return false;
        InventorySlot slot = inventoryContainer.GetSlot(index);
        IInventoryItem item = slot.Item;
        if (item == null || !worldItemDropper.TryPrepare(new[] { item }, out PlayerWorldItemDropper.PreparedDrop drop)) return false;
        slot.SetItem(null);
        drop.Commit();
        RefreshOwnedItemReferences();
        return true;
    }

    // 바닥 획득도 장비 자동 장착과 가방 부분 이동을 사용한다. 무게는 획득 성공 조건으로 검사하지 않는다.
    public bool TryAcquireFrom(InventoryContainer source, int index)
    {
        if (source == null || ReferenceEquals(source, inventoryContainer) || inventoryContainer == null || index < 0 || index >= source.SlotCount) return false;
        IInventoryItem item = source.GetSlot(index).Item;
        if (item == null) return false;
        PlayerEquipment equipment = GetComponent<PlayerEquipment>();
        bool acquired = false;
        if (item is WeaponInventoryItem && equipment?.WeaponEquipment != null)
        {
            for (int i = 0; i < equipment.WeaponEquipment.SlotCount; i++)
                if (equipment.WeaponEquipment.GetItem(i) == null) { acquired = equipment.TryMoveWeaponToEquipment(source, index, i); break; }
        }
        else if (item is ArmorInventoryItem protection && equipment != null && equipment.ProtectionEquipment.GetItem(protection.Type) == null)
            acquired = equipment.TryMoveProtectionToEquipment(source, index, protection.Type);
        if (!acquired) acquired = source.TryTransferTo(inventoryContainer, index, out _);
        if (!acquired) return false;
        RefreshOwnedItemReferences();
        PlayerItemQuickslots quickslots = GetComponent<PlayerItemQuickslots>();
        if (quickslots != null)
            for (int i = 0; i < inventoryContainer.SlotCount; i++)
            {
                IInventoryItem owned = inventoryContainer.GetSlot(i).Item;
                if (owned == null || owned.Type != item.Type || owned.ItemId != item.ItemId) continue;
                if (owned.Type == ItemType.Meds || owned.Type == ItemType.Consumable || owned.Type == ItemType.Buff || owned.Type == ItemType.Throwables) quickslots.TryAutoRegister(owned);
                break;
            }
        return true;
    }

    public bool TryApplyRestoredSlots(IInventoryItem[] restoredSlots)
    {
        if (restoredSlots == null || inventoryContainer == null) return false;

        if (!TryCalculateCapacity(additionalBagCapacity, out int capacity) || restoredSlots.Length != capacity) return false;

        inventoryContainer.ApplyResize(restoredSlots);

        return true;
    }

    // 소유 공간을 떠난 아이템만 등록에서 제거한다. 프레임마다 등록 전체를 검사하지 않는다.
    internal void RefreshOwnedItemReferences()
    {
        PlayerItemQuickslots quickslots = GetComponent<PlayerItemQuickslots>();
        quickslots?.RemoveInvalidRegistrations();
        PlayerHeldItemController held = GetComponent<PlayerHeldItemController>();
        if (held != null && held.SelectedItem != null && quickslots != null)
        {
            int index = held.SelectedItemSlotIndex;
            if (index < 0 || index >= quickslots.Quickslots.SlotCount || !ReferenceEquals(held.SelectedItem, quickslots.Quickslots.GetItem(index))) held.ClearItemSelection();
        }
        NotifyOwnedItemsChanged();
    }

    private void OnEnable()
    {
        if (dataRoot == null)
        {
            Debug.LogError("PlayerInventory의 DataRoot를 연결해주세요.", this);
            return;
        }

        dataRoot.OnInitialized += HandleDataInitialized;

        // 데이터가 먼저 준비됐다면 등록한 이벤트가 호출되지 않으므로, 준비 상태를 확인하고 직접 호출하도록 한다.
        if (dataRoot.isReady)
            HandleDataInitialized();
    }

    private void OnDisable()
    {
        if (dataRoot != null)
            dataRoot.OnInitialized -= HandleDataInitialized;
    }

    private void HandleDataInitialized()
    {
        if (ammoStorage != null)
            return;

        if (dataRoot.AmmoCatalog == null)
        {
            Debug.LogError("PlayerInventory의 DataRoot에 AmmoCatalog가 연결되지 않았습니다.", this);
            return;
        }

        ammoStorage = new InventoryAmmoStorage(inventoryContainer, dataRoot.AmmoCatalog);
    }

#if UNITY_EDITOR

    [ContextMenu("검증/총과 탄약 지급 (AK-47)")]
    private void GrantFirearmAndAmmoForTesting()
    {
        if (!Application.isPlaying || inventoryContainer == null || ammoStorage == null || dataRoot == null || !dataRoot.isReady)
        {
            Debug.LogWarning("플레이 중이고 플레이어의 전투 데이터와 인벤토리가 준비되어야 한다.", this);
            return;
        }

        const int weaponId = 1001;
        const int ammoId = 2001;
        const int reserveAmmoCount = 90;

        AmmoDefinition ammo = FindAmmoDefinition(ammoId);
        if (!dataRoot.WeaponCatalog.TryGet(weaponId, out RuntimeWeapon runtimeWeapon) ||
            ammo == null || runtimeWeapon.Definition.ammoType != ammo.ammoType)
        {
            Debug.LogWarning("AK-47(ID 1001)과 호환 소총탄(ID 2001)의 정의가 필요하다.", this);
            return;
        }

        // 새로 지급하는 총만 장전한다. 이미 소유한 총의 잔탄은 변경하지 않는다.
        var weapon = new WeaponInstance(runtimeWeapon);
        weapon.AmmoState.Load(ammo, weapon.AmmoState.Capacity);

        // 임시 지급함에서 실제 획득 경로로 이동해 장비·가방·무게 계산에 동일 객체를 등록한다.
        var source = new InventoryContainer(1);
        source.GetSlot(0).SetItem(new WeaponInventoryItem(weapon));
        if (!TryAcquireFrom(source, 0))
        {
            Debug.LogWarning("총을 지급하지 못했다. 장비 슬롯 또는 가방의 빈 공간과 무기 외형 설정을 확인해야 한다.", this);
            return;
        }

        bool selected = false;
        PlayerEquipment equipment = GetComponent<PlayerEquipment>();
        if (equipment != null && equipment.WeaponEquipment != null)
        {
            for (int i = 0; i < equipment.WeaponEquipment.SlotCount; i++)
            {
                if (!ReferenceEquals(equipment.WeaponEquipment.GetItem(i)?.GetWeapon(), weapon))
                    continue;

                selected = equipment.TrySelectWeaponSlot(i);
                break;
            }
        }

        ammoStorage.TryAdd(ammoId, reserveAmmoCount, out int addedCount);
        string placement = selected ? "손에 장착 완료" : "장비 또는 가방에 보관됨. 사격하려면 무기 슬롯에서 선택해야 한다";
        Debug.Log($"{runtimeWeapon.Definition.displayName} 지급: 탄창 {weapon.AmmoState.LoadedAmmoCount}발, 예비 탄약 {addedCount}/{reserveAmmoCount}발 추가. {placement}.", this);

        if (addedCount < reserveAmmoCount)
            Debug.LogWarning("가방 공간이 부족해 예비 탄약은 들어갈 수 있는 수량만 지급했다.", this);
    }

    [ContextMenu("검증/가방 빈 칸 모두 채우기")]
    private void FillEmptyBagSlotsForTesting()
    {
        if (!Application.isPlaying || inventoryContainer == null)
        {
            Debug.LogWarning("플레이 중에만 가방을 채울 수 있다.", this);
            return;
        }

        AmmoDefinition ammo = FindAmmoDefinition(2001);
        if (ammo == null || ammo.maxStackCount <= 0)
        {
            Debug.LogWarning("데이터 초기화 후 소총탄 ID 2001의 유효한 정의가 필요하다.", this);
            return;
        }

        int filledCount = 0;
        for (int i = 0; i < inventoryContainer.SlotCount; i++)
        {
            InventorySlot slot = inventoryContainer.GetSlot(i);
            if (!slot.IsEmpty) continue;
            // 칸 감소 검증용으로 최대 스택을 배치한다. 다른 칸과 합쳐져 초과 드롭이 사라지는 경우를 줄인다.
            // 칸마다 별도 객체를 만든다. 같은 스택을 공유하면 한 칸의 소비가 다른 칸에도 반영된다.
            slot.SetItem(new AmmoStack(ammo, ammo.maxStackCount));
            filledCount++;
        }
        Debug.Log($"가방 빈 칸 {filledCount}개를 {ammo.displayName} 최대 스택으로 채웠다. 기존 아이템은 변경하지 않았다.", this);
    }

    // 컴포넌트 메뉴에서 직접 선택했을 때 이 메서드를 실행합니다.
    [ContextMenu("검증/소총탄 90발 지급")]
    private void AddAmmoForTesting()
    {
        // 플레이 중이 아니거나 데이터 연결이 끝나지 않았다면 지급하지 않습니다.
        if (!Application.isPlaying || ammoStorage == null)
        {
            Debug.LogWarning("플레이 중이고 탄약 보관소가 준비되어야 합니다.", this);
            return;
        }

        // CSV에 등록된 소총탄을 실제 가방에 가능한 수량만큼 추가합니다.
        ammoStorage.TryAdd(2001, 90, out int addedCount);

        // 요청량이 아니라 실제 추가량과 현재 가방 보유량을 확인합니다.
        Debug.Log($"추가: {addedCount}발 / 가방: {ammoStorage.GetCount(2001)}발", this);
    }

    [ContextMenu("검증/탄약량 확인")]
    private void PrintAmmoForTesting()
    {
        // 준비되지 않은 가방에는 접근하지 않습니다.
        if (!Application.isPlaying || ammoStorage == null)
            return;

        // 같은 플레이어에 붙은 무기 홀더를 조회합니다.
        PlayerWeaponHolder holder = GetComponent<PlayerWeaponHolder>();

        // 무기가 있다면 현재 탄창 상태를 가져옵니다.
        WeaponAmmoState magazine = holder != null ? holder.CurrentAmmoState : null;

        // 무기가 없으면 장탄 수는 0으로 표시합니다.
        int loadedCount = magazine != null ? magazine.LoadedAmmoCount : 0;

        // 가방의 예비 탄약과 총 안의 탄약을 따로 표시합니다.
        Debug.Log($"가방 소총탄: {ammoStorage.GetCount(2001)}발 / 탄창: {loadedCount}발", this);
    }

#endif
}
