using System.Collections.Generic;
using UnityEngine;

// 개별 장비 담당자를 모아 전리품 요청에 하나의 진입점을 제공한다.
[DisallowMultipleComponent]
public class EnemyEquipmentLoadout : MonoBehaviour
{
    [SerializeField] private EnemyWeaponLoadout weaponLoadout;
    [SerializeField] private EnemyProtectionLoadout protectionLoadout;
    [SerializeField] private EnemyEquipmentTable spawnEquipmentTable;
    private bool spawnSelectionPrepared;

    public EnemyEquipmentTable SpawnEquipmentTable => spawnEquipmentTable;

    // 활성화 전에 추첨 결과의 ID만 전달한다. OnEnable에서 기존 경로로 실제 장비를 만든다.
    public bool PrepareSpawn(ExtractionShooter.DataPipeline.CombatDataCompositionRoot root,
        EnemyEquipmentTable tableOverride, System.Random random)
    {
        if (spawnSelectionPrepared) return true;
        EnemyEquipmentTable table = tableOverride != null ? tableOverride : spawnEquipmentTable;
        if (table == null) return true; // 기존 고정 장비 적·테스트 프리팹도 지원한다.
        if (gameObject.activeInHierarchy) return false;
        if (weaponLoadout == null) weaponLoadout = GetComponent<EnemyWeaponLoadout>();
        if (protectionLoadout == null) protectionLoadout = GetComponent<EnemyProtectionLoadout>();
        if (weaponLoadout == null || protectionLoadout == null || weaponLoadout.IsReady || protectionLoadout.IsReady) return false;
        if (!table.Validate(root, out string error))
        { Debug.LogError($"{table.name}: {error}", this); return false; }
        if (!table.TryRoll(root, random, out var selected)) return false;
        weaponLoadout.ConfigureSpawn(selected.WeaponId, selected.AmmoId);
        protectionLoadout.ConfigureSpawn(selected.ArmorId, selected.HelmetId);
        spawnSelectionPrepared = true;
        return true;
    }

    private void Awake()
    {
        if (weaponLoadout == null) weaponLoadout = GetComponent<EnemyWeaponLoadout>();
        if (protectionLoadout == null) protectionLoadout = GetComponent<EnemyProtectionLoadout>();
    }

    public bool TryCollectLoot(out IReadOnlyList<IInventoryItem> items)
    {
        items = null;
        if (protectionLoadout != null && !protectionLoadout.IsReady) return false;
        var candidate = new List<IInventoryItem>(4);
        // 인벤토리 래퍼만 만들며 탄창을 가진 기존 무기 인스턴스는 그대로 넘긴다.
        if (weaponLoadout?.CurrentWeaponInstance != null)
            candidate.Add(new WeaponInventoryItem(weaponLoadout.CurrentWeaponInstance));
        var ammo = weaponLoadout?.CurrentAmmo;
        if (ammo != null)
        {
            int count = Random.Range(0, Mathf.Min(19, ammo.maxStackCount) + 1);
            if (count > 0) candidate.Add(new AmmoStack(ammo, count));
        }
        // 손상된 보호 장비도 같은 아이템과 같은 ArmorInstance를 전달한다.
        if (protectionLoadout?.Armor != null) candidate.Add(protectionLoadout.Armor);
        if (protectionLoadout?.Helmet != null) candidate.Add(protectionLoadout.Helmet);
        if (candidate.Count == 0) return false;
        items = candidate;
        return true;
    }
}
