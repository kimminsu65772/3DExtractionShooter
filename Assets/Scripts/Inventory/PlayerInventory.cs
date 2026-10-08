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

    // 가방 아이템 시스템이 추가되면 그 장비 담당자가 제공하는 칸 수를 이 경계로 전달한다.
    public bool TrySetAdditionalBagCapacity(int capacity)
    {
        if (capacity < 0) return false;
        if (!TryResizeForEquipment(capacity)) return false;
        additionalBagCapacity = capacity;
        return true;
    }

    public bool TryRefreshCapacity() => TryResizeForEquipment(additionalBagCapacity);

    private bool TryResizeForEquipment(int bagBonus)
    {
        PlayerEquipment equipment = GetComponent<PlayerEquipment>();
        int armorBonus = equipment?.ProtectionEquipment.Armor?.Definition is ArmorDefinition armor ? armor.backpackCapacityBonus : 0;
        long requestedCapacity = (long)baseCapacity + bagBonus + Mathf.Max(0, armorBonus);
        if (requestedCapacity > int.MaxValue) return false;
        int capacity = Mathf.Max(1, (int)requestedCapacity);
        if (inventoryContainer == null) return false;
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
