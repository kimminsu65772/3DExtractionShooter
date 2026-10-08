using UnityEngine;
using ExtractionShooter.DataPipeline;
using System.Collections.Generic;

public class LootContainer : MonoBehaviour
{
    [SerializeField] private LootContainerSize containerSize = LootContainerSize.Small;
    [SerializeField] private LootSeedEntry[] initialItems;

    private InventoryContainer inventoryContainer;
    private bool isInitialized;

    public InventoryContainer InventoryContainer => inventoryContainer;

    // 처음 열 때만 목록을 구성한다. 잘못된 항목이 있으면 미완성 목록을 공개하지 않는다.
    public bool TryInitializeContents(CombatDataCompositionRoot dataRoot)
    {
        if (isInitialized) return true;
        if (inventoryContainer == null) return false;
        if (initialItems == null || initialItems.Length == 0)
        {
            isInitialized = true;
            return true;
        }
        if (dataRoot == null || !dataRoot.isReady) return false;
        InventoryContainer candidate = new InventoryContainer(inventoryContainer.SlotCount);
        foreach (LootSeedEntry seed in initialItems)
        {
            if (seed == null || seed.Count <= 0) return ReportInvalidSeed();
            if (seed.ItemType == ItemType.Ammo)
            {
                if (dataRoot.AmmoCatalog == null || !dataRoot.AmmoCatalog.TryGet(seed.ItemId, out AmmoDefinition ammo)) return ReportInvalidSeed();
                if (candidate.AddAmmoUpTo(ammo, seed.Count) != seed.Count) return ReportInvalidSeed();
            }
            else if (seed.ItemType == ItemType.Weapon)
            {
                if (seed.Count > candidate.SlotCount || dataRoot.WeaponCatalog == null || !dataRoot.WeaponCatalog.TryGet(seed.ItemId, out RuntimeWeapon weapon) || weapon.Definition.weaponType != WeaponType.Ranged) return ReportInvalidSeed();
                // 같은 종류의 총 여러 자루는 각각 별개의 탄창 상태를 갖는다.
                for (int i = 0; i < seed.Count; i++)
                    if (!candidate.TryPlaceInEmptySlot(new WeaponInventoryItem(new WeaponInstance(weapon)), out _)) return ReportInvalidSeed();
            }
            else
            {
                ItemDefinition definition = null;
                if (seed.ItemType == ItemType.Meds && dataRoot.MedicineCatalog != null && dataRoot.MedicineCatalog.TryGet(seed.ItemId, out MedicineDefinition medicine)) definition = medicine;
                if (seed.ItemType == ItemType.Armor && dataRoot.ArmorCatalog != null && dataRoot.ArmorCatalog.TryGet(seed.ItemId, out ArmorDefinition armor)) definition = armor;
                if (seed.ItemType == ItemType.Helmet && dataRoot.HelmetCatalog != null && dataRoot.HelmetCatalog.TryGet(seed.ItemId, out HelmetDefinition helmet)) definition = helmet;
                // 일부만 채워진 슬롯이 생기지 않도록, 시드가 완료되지 않은 경우에는 아예 지급하지 않는다.
                if (definition == null || candidate.AddItemUpTo(definition, seed.Count) != seed.Count) return ReportInvalidSeed();
            }
        }
        inventoryContainer = candidate;
        isInitialized = true;
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
