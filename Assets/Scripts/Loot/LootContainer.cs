using UnityEngine;
using ExtractionShooter.DataPipeline;
using System.Collections.Generic;

public class LootContainer : MonoBehaviour
{
    [SerializeField] private LootContainerSize containerSize = LootContainerSize.Small;
    [SerializeField] private LootSeedEntry[] initialItems;
    [SerializeField] private LootTable randomTable;

    private InventoryContainer inventoryContainer;
    private bool isInitialized;

    public InventoryContainer InventoryContainer => inventoryContainer;
    public LootTable RandomTable => randomTable;
    public bool IsInitialized => isInitialized;

    internal void InitializePreparedContents(InventoryContainer contents)
    {
        inventoryContainer = contents;
        isInitialized = true;
    }

    // 이미 로딩에서 준비한 상자는 그대로 반환하고, 이전 고정 상자의 열기 초기화도 지원한다.
    public bool TryInitializeContents(CombatDataCompositionRoot dataRoot)
    {
        if (isInitialized) return true;
        // 무작위 상자는 탐험 로딩에서 준비한다. 열기를 반복해 재추첨하지 않는다.
        if (randomTable != null) return false;
        if (!TryPrepareFixedContents(dataRoot, out InventoryContainer candidate)) return false;
        InitializePreparedContents(candidate);
        return true;
    }

    internal bool TryPrepareFixedContents(CombatDataCompositionRoot dataRoot, out InventoryContainer candidate)
    {
        candidate = null;
        int slotCount = inventoryContainer != null ? inventoryContainer.SlotCount : (int)containerSize;
        if (slotCount <= 0) return false;
        candidate = new InventoryContainer(slotCount);
        if (initialItems == null || initialItems.Length == 0)
            return true;
        if (dataRoot == null || !dataRoot.isReady) return false;
        foreach (LootSeedEntry seed in initialItems)
        {
            if (seed == null || seed.Count <= 0) return ReportInvalidSeed();
            if (!LootItemFactory.TryAdd(candidate, dataRoot, seed.ItemType, seed.ItemId, seed.Count)) return ReportInvalidSeed();
        }
        return true;
    }

    public bool TryInitializeContents(IReadOnlyList<IInventoryItem> items)
    {
        if (isInitialized) return false;
        // 비활성 상태에서 드롭을 준비할 때는 Awake 이전에도 내용물을 구성할 수 있어야 한다.
        if (inventoryContainer == null && containerSize != LootContainerSize.None) inventoryContainer = new InventoryContainer((int)containerSize);
        if (inventoryContainer == null) return false;
        if (items == null) return false;

        InventoryContainer candidate = new InventoryContainer(inventoryContainer.SlotCount);

        // 상자 빈 공간에 아이템을 넣는다. 만약 하나라도 실패하면 초기화에 실패한다.
        foreach (IInventoryItem item in items)
        {
            if (item == null) return false;

            if (!candidate.TryPlaceInEmptySlot(item, out _))
            {
                Debug.LogWarning($"상자 '{name}'에 아이템 '{item.ItemId}'를 추가할 수 없습니다. 슬롯이 부족하거나 잘못된 아이템입니다.", this);
                return false;
            }
        }

        // 모든 아이템을 성공적으로 추가했으면 초기화를 완료하고
        // 임시 후보를 실제 상자 컨테이너로 승격시킨다.
        inventoryContainer = candidate;
        isInitialized = true;
        return true;
    }

    public bool TryApplyRestoredSlots(IInventoryItem[] restoredSlots)
    {
        if (restoredSlots == null || inventoryContainer == null)
            return false;

        // 이미 초기화된 상자는 슬롯을 덮어쓰지 않도록 한다
        if (isInitialized)
            return false; 

        //슬롯 수 일치 여부를 확인한다.
        if (restoredSlots.Length != inventoryContainer.SlotCount)
            return false;

        inventoryContainer.ApplyResize(restoredSlots);

        isInitialized = true;

        return true;
    }

    private bool ReportInvalidSeed()
    {
        Debug.LogWarning($"상자 '{name}'의 아이템 ID, 종류, 수량 또는 칸 수를 확인한다. 내용물을 지급하지 않는다.", this);
        return false;
    }

    private void Awake()
    {
        if (containerSize == LootContainerSize.None)
        {
            Debug.LogWarning($"LootContainer '{gameObject.name}'의 containerSize가 None으로 설정되어 있습니다. 적절한 사이즈를 설정하세요.", this);
            return;
        }

        if (inventoryContainer == null) inventoryContainer = new InventoryContainer((int)containerSize);
    }
}
