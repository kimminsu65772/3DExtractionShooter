using System;
using ExtractionShooter.DataPipeline;
using UnityEngine;

[Serializable]
public class WorldItemVisualEntry
{
    public ItemType type;
    public int itemId;
    public GameObject prefab;
}

// 게임 로직이나 Collider를 복제하지 않고 실제 메시와 공유 머테리얼만 월드 표현으로 구성한다.
public static class WorldItemVisualFactory
{
    public static void Create(Transform root, IInventoryItem item, WorldItemVisualEntry[] entries, CombatDataCompositionRoot dataRoot)
    {
        if (entries != null)
            foreach (WorldItemVisualEntry entry in entries)
                if (entry != null && entry.type == item.Type && entry.itemId == item.ItemId && entry.prefab != null && CopyMeshes(entry.prefab, root)) return;
        // 무기 외형 풀은 잠시 조회한 뒤 즉시 반환한다. 바닥 물체가 장착용 풀을 계속 점유하지 않는다.
        if (item is WeaponInventoryItem gun && dataRoot != null && dataRoot.isReady && dataRoot.Pools != null && dataRoot.Pools.TryRent(gun.GetWeapon().RuntimeWeapon.WeaponViewId, out PoolLease lease))
        {
            try { if (CopyMeshes(lease.Instance, root)) return; }
            finally { dataRoot.Pools.TryReturn(lease); }
        }
        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallback.name = "Fallback Item Visual";
        fallback.transform.SetParent(root, false);
        fallback.transform.localScale = new Vector3(0.18f, 0.12f, 0.18f);
        Collider collider = fallback.GetComponent<Collider>();
        collider.enabled = false;
        UnityEngine.Object.Destroy(collider);
    }

    private static bool CopyMeshes(GameObject source, Transform parent)
    {
        MeshFilter[] filters = source.GetComponentsInChildren<MeshFilter>(true);
        bool created = false;
        foreach (MeshFilter filter in filters)
        {
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (filter.sharedMesh == null || renderer == null || !renderer.enabled) continue;
            GameObject part = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            Matrix4x4 relative = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            part.transform.localPosition = Vector3.Scale(relative.GetColumn(3), source.transform.localScale);
            part.transform.localRotation = relative.rotation;
            part.transform.localScale = Vector3.Scale(relative.lossyScale, source.transform.localScale);
            part.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            part.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            created = true;
        }
        // 스킨 외형은 현재 자세를 정적인 메시로 굽는다. 뼈대나 Animator까지 복제하지 않는다.
        foreach (SkinnedMeshRenderer renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null || !renderer.enabled) continue;
            Mesh mesh = new Mesh();
            parent.GetComponent<WorldItemPickup>().OwnMesh(mesh);
            renderer.BakeMesh(mesh);
            GameObject part = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            Matrix4x4 relative = source.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            part.transform.localPosition = Vector3.Scale(relative.GetColumn(3), source.transform.localScale);
            part.transform.localRotation = relative.rotation;
            part.transform.localScale = Vector3.Scale(relative.lossyScale, source.transform.localScale);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            created = true;
        }
        return created;
    }
}
