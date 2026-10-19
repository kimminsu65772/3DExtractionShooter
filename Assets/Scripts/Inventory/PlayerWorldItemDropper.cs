using System;
using System.Collections.Generic;
using UnityEngine;
using ExtractionShooter.DataPipeline;

// 아이템 객체를 새로 만들지 않고 실제 외형과 F키 직접 획득을 가진 바닥 물체로 넘긴다.
public class PlayerWorldItemDropper : MonoBehaviour
{
    [SerializeField] private WorldItemVisualEntry[] itemVisuals;
    [SerializeField] private CombatDataCompositionRoot dataRoot;
    [SerializeField] private LayerMask groundMask = 1 << 6;
    [SerializeField, Min(0f)] private float dropDistance = 0.4f;
    [SerializeField, Min(0.01f)] private float groundProbeHeight = 2f;
    [SerializeField, Min(0.01f)] private float groundProbeDistance = 10f;
    [SerializeField, Min(0f)] private float groundClearance = 0.01f;

    public void SetDataRoot(CombatDataCompositionRoot dataRoot)
    {
        this.dataRoot = dataRoot;
    }

    public bool TryPrepare(IReadOnlyList<IInventoryItem> items, out PreparedDrop prepared)
    {
        prepared = null;
        if (items == null || items.Count == 0) return false;
        if (dataRoot == null) dataRoot = GetComponent<PlayerWeaponHolder>()?.DataRoot;

        GameObject staging = new GameObject("PreparedItemDrop");
        staging.SetActive(false);
        List<WorldItemPickup> pickups = new();
        HashSet<object> identities = new();
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) 
                    throw new ArgumentException("드롭할 아이템은 null일 수 없다.");

                object identity = items[i] is WeaponInventoryItem gun ? gun.GetWeapon() : items[i] is ArmorInventoryItem armor ? armor.Armor : items[i];

                if (!identities.Add(identity)) 
                    throw new ArgumentException("하나의 런타임 객체를 두 번 드롭할 수 없다.");

                GameObject dropped = new GameObject("Dropped Item");
                dropped.transform.SetParent(staging.transform, false);
                dropped.transform.position = transform.position + transform.forward * dropDistance + transform.right * (i % 3 - 1) * 0.25f + transform.forward * (i / 3) * 0.2f;
                WorldItemPickup pickup = dropped.AddComponent<WorldItemPickup>();

                if (!pickup.TryInitialize(items[i])) 
                    throw new InvalidOperationException("바닥 아이템 초기화에 실패했다.");

                WorldItemVisualFactory.Create(dropped.transform, items[i], itemVisuals, dataRoot);
                // 상호작용만 감지한다. 바닥 아이템은 플레이어나 총알을 물리적으로 막지 않는다.
                SphereCollider trigger = dropped.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 0.15f;
                pickups.Add(pickup);
            }
            prepared = new PreparedDrop(this, staging, pickups);
            return true;
        }
        catch (Exception exception)
        {
            // 아직 공개되지 않은 후보만 제거한다. 호출자는 소유 슬롯을 그대로 유지한다.
            Destroy(staging);
            Debug.LogWarning($"아이템 드롭 준비 실패: {exception.Message}", this);
            return false;
        }
    }

    private void PlaceOnGround(WorldItemPickup pickup)
    {
        Physics.SyncTransforms();
        Renderer[] renderers = pickup.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(pickup.transform.position, Vector3.zero);
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        // 모델 원점이 외형에서 벗어나 있어도 실제 외형 중심에서 상호작용을 감지한다.
        SphereCollider trigger = pickup.GetComponent<SphereCollider>();
        trigger.center = pickup.transform.InverseTransformPoint(bounds.center);
        trigger.radius = Mathf.Max(0.15f, Mathf.Max(bounds.extents.x, bounds.extents.z));
        Vector3 origin = new Vector3(bounds.center.x, Mathf.Max(transform.position.y, bounds.max.y) + groundProbeHeight, bounds.center.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundProbeDistance + groundProbeHeight, groundMask, QueryTriggerInteraction.Ignore))
            pickup.transform.position += Vector3.up * (hit.point.y + groundClearance - bounds.min.y);
    }

    // 준비와 공개를 분리한다. 슬롯 이동이 확정된 뒤에만 바닥 물체를 상호작용 대상으로 노출한다.
    public class PreparedDrop
    {
        private readonly PlayerWorldItemDropper owner;
        private readonly GameObject staging;
        private readonly List<WorldItemPickup> pickups;
        private bool finished;

        internal PreparedDrop(PlayerWorldItemDropper owner, GameObject staging, List<WorldItemPickup> pickups)
        {
            this.owner = owner;
            this.staging = staging;
            this.pickups = pickups;
        }

        public void Commit()
        {
            if (finished) return;
            finished = true;
            foreach (WorldItemPickup pickup in pickups)
            {
                pickup.transform.SetParent(null, true);
                owner.PlaceOnGround(pickup);
            }
            Destroy(staging);
        }

        public void Cancel()
        {
            if (finished) return;
            finished = true;
            Destroy(staging);
        }
    }
}
