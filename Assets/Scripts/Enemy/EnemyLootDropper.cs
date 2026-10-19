using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyEquipmentLoadout))]
public class EnemyLootDropper : MonoBehaviour
{
    [SerializeField] private EnemyEquipmentLoadout equipmentLoadout;
    [SerializeField] private LootContainer lootPrefab;
    [Tooltip("몸통 히트박스의 중심 Transform. 연결되지 않은 옛 적은 발 위치에서 0.6m 위를 사용한다.")]
    [SerializeField] private Transform dropOrigin;

    private bool hasDropped;

    private void Awake()
    {
        if (equipmentLoadout == null) equipmentLoadout = GetComponent<EnemyEquipmentLoadout>();
        if (equipmentLoadout == null)
            Debug.LogError("EnemyLootDropper에 EnemyEquipmentLoadout이 필요하다.", this);
    }

    public bool TryDropLoot()
    {
        if (hasDropped || equipmentLoadout == null || lootPrefab == null) return false;
        // Trigger는 상호작용에 사용한다. 착지를 위해서는 같은 Rigidbody의 별도 물리 Collider가 필요하다.
        Rigidbody prefabBody = lootPrefab.GetComponent<Rigidbody>();
        bool hasPhysicalCollider = false;
        foreach (Collider collider in lootPrefab.GetComponentsInChildren<Collider>(true))
            if (collider.enabled && !collider.isTrigger && collider.GetComponentInParent<Rigidbody>(true) == prefabBody)
                hasPhysicalCollider = true;
        if (prefabBody == null || !hasPhysicalCollider)
        {
            Debug.LogError("드롭 상자에 Rigidbody와 Trigger가 아닌 물리 Collider를 연결해야 한다.", this);
            return false;
        }
        if (!equipmentLoadout.TryCollectLoot(out IReadOnlyList<IInventoryItem> lootItems)) return false;

        Vector3 position = dropOrigin != null ? dropOrigin.position : transform.position + Vector3.up * 0.6f;
        // 비활성 게임용 프리팹을 사용하여 내용물을 구성한 뒤 상호작용과 물리 낙하를 시작한다.
        GameObject lootBox = Instantiate(lootPrefab.gameObject, position, Quaternion.identity);
        lootBox.SetActive(false);
        LootContainer contents = lootBox.GetComponent<LootContainer>();
        if (contents == null || !contents.TryInitializeContents(lootItems))
        {
            Debug.LogError("드롭 상자의 내용물을 구성하지 못했다.", this);
            Destroy(lootBox);
            return false;
        }

        Rigidbody body = lootBox.GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        lootBox.SetActive(true);
        body.WakeUp();
        // 바닥 높이를 다시 찾거나 순간 이동시키지 않는다. 실제 바닥 Collider와 중력이 착지를 처리한다.
        hasDropped = true;
        return true;
    }
}
