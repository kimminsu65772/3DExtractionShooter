using System.Collections.Generic;
using UnityEngine;

public class EnemyLootDropper : MonoBehaviour
{
    // 적이 실제로 사용하던 무기와 탄약을 읽는다.
    [SerializeField] private EnemyWeaponLoadout loadout;

    // 사망 위치에 생성할 전리품 상자의 원본을 지정한다.
    [SerializeField] private LootContainer lootPrefab;

    // 상자 대신 실제 바닥만 찾도록 Ground 레이어를 기본 대상으로 지정한다.
    [SerializeField] private LayerMask groundMask = 1 << 6;

    // 적의 위치보다 위에서 바닥을 찾기 시작해 바닥 안에서 검사를 시작하지 않도록 한다.
    [SerializeField, Min(0.01f)] private float groundProbeHeight = 2f;

    // 바닥을 찾을 최대 거리를 정한다.
    [SerializeField, Min(0.01f)] private float groundProbeDistance = 10f;

    // 상자 밑면이 바닥과 겹치지 않도록 아주 작은 간격을 둔다.
    [SerializeField, Min(0f)] private float groundClearance = 0.01f;

    // 같은 적이 전리품을 여러 번 생성하지 않도록 기록한다.
    private bool hasDropped;

    private void Awake()
    {
        if (loadout == null) loadout = GetComponent<EnemyWeaponLoadout>();
        if (loadout == null) 
            Debug.LogError("EnemyLootDropper requires an EnemyWeaponLoadout component.");
    }

    public bool TryDropLoot()
    {
        // 이미 전리품을 생성했거나, 필요한 참조가 없으면 실패한다.
        if (hasDropped) return false;
        if (loadout == null || lootPrefab == null) return false;
        if (loadout.CurrentWeapon == null || loadout.CurrentAmmo == null) return false;

        List<IInventoryItem> lootItems = new List<IInventoryItem>();
        WeaponInventoryItem lootWeapon = loadout.CurrentWeaponInstance != null ? new WeaponInventoryItem(loadout.CurrentWeaponInstance) : null;

        if (lootWeapon != null)
        {
            lootItems.Add(lootWeapon);
        }

        int maxDropCount = Mathf.Min(19, loadout.CurrentAmmo.maxStackCount);
        int randomAmmoCount = Random.Range(0, maxDropCount + 1);

        if (randomAmmoCount > 0)
        {
            AmmoStack ammo = new AmmoStack(loadout.CurrentAmmo, randomAmmoCount);
            lootItems.Add(ammo);
        }

        GameObject lootBox = Instantiate(lootPrefab.gameObject, transform.position, Quaternion.identity);
        lootBox.transform.SetParent(null);

        LootContainer lootContainer = lootBox.GetComponent<LootContainer>();
        
        if (lootContainer == null)
        {
            Debug.LogError("Loot prefab does not have a LootContainer component.");
            Destroy(lootBox);
            return false;
        }

        if (!lootContainer.TryInitializeContents(lootItems))
        {
            Debug.LogError("Failed to initialize loot container contents.");
            Destroy(lootBox);
            return false;
        }

        hasDropped = true;
        PlaceOnGround(lootBox);
        return true;

    }

    private void PlaceOnGround(GameObject lootBox)
    {
        // 루트의 상자 Collider를 우선 사용하고, 없으면 자식에서 찾는다.
        Collider boxCollider = lootBox.GetComponent<Collider>();
        if (boxCollider == null) boxCollider = lootBox.GetComponentInChildren<Collider>();
        if (boxCollider == null || !boxCollider.enabled || !boxCollider.gameObject.activeInHierarchy)
        {
            // 밑면을 알 수 없으면 아이템을 버리지 않고 원래 생성 위치에 남긴다.
            Debug.LogWarning("전리품 상자의 활성 Collider가 없어 바닥 높이를 보정하지 못했다.", lootBox);
            return;
        }

        // 방금 생성한 상자의 위치와 크기를 물리 검사에 반영한다. 생성 시 한 번만 실행한다.
        Physics.SyncTransforms();

        // Collider가 차지하는 공간에서 중심과 가장 낮은 높이를 읽는다.
        Bounds boxBounds = boxCollider.bounds;

        // 상자의 중심 바로 위에서 아래로 검사해 상자 아래에 있는 바닥을 찾는다.
        Vector3 rayOrigin = new Vector3(boxBounds.center.x, Mathf.Max(transform.position.y, boxBounds.max.y) + groundProbeHeight, boxBounds.center.z);
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            // 바닥을 못 찾았을 때 상자를 임의의 높이로 옮기지 않는다.
            Debug.LogWarning("전리품 상자 아래의 바닥을 찾지 못했다. Ground Mask와 검사 거리를 확인한다.", lootBox);
            return;
        }

        // 바닥 높이에서 상자 밑면 높이를 빼면 상자를 올리거나 내릴 거리가 나온다.
        float heightCorrection = hit.point.y + groundClearance - boxBounds.min.y;

        // 가로 위치와 회전은 유지하고, 상자 밑면이 바닥에 닿도록 높이만 바꾼다.
        lootBox.transform.position += Vector3.up * heightCorrection;
    }
}
