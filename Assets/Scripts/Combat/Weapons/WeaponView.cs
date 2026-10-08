using UnityEngine;
using ExtractionShooter.DataPipeline;

[DisallowMultipleComponent]
public class WeaponView : MonoBehaviour, IPoolLifecycle
{
    [SerializeField] private Transform muzzleTransform;

    // 무기 손잡이에 둔 기준점이다. 비워두면 기존처럼 무기 루트를 홀더에 맞춘다.
    [SerializeField] private Transform gripTransform;

    // WeaponView가 자식에 붙어 있어도 풀에서 대여한 무기 전체의 크기를 보관한다.
    private Transform pooledRoot;
    private Vector3 originalLocalScale;

    public Transform MuzzleTransform => muzzleTransform;
    public bool HasMuzzle => muzzleTransform != null;

    public void OnRent(PoolLease lease, IPoolReturner owner)
    {
        // 최초 대여 시 부모에 붙이기 전의 원래 크기를 한 번만 저장한다.
        if (pooledRoot == null)
        {
            pooledRoot = lease.Instance.transform;
            originalLocalScale = pooledRoot.localScale;
        }

        // 이전 캐릭터에게 장착했을 때의 보정값을 다음 대여에 넘기지 않는다.
        pooledRoot.localScale = originalLocalScale;
    }

    public void OnReturn()
    {
        // 반환할 때도 원래 크기로 복구해 풀 내부에 축소된 크기가 남지 않게 한다.
        if (pooledRoot != null) pooledRoot.localScale = originalLocalScale;
    }

    public bool TryAttachTo(Transform mount)
    {
        if (mount == null || pooledRoot == null) return false;

        // 다른 물건의 기준점을 연결하면 잘못된 위치로 이동하므로 장착 전에 확인한다.
        if (gripTransform != null && !gripTransform.IsChildOf(pooledRoot))
        {
            Debug.LogWarning("Grip Transform은 대여한 무기 루트 또는 그 자식이어야 한다.", this);
            return false;
        }

        // 로컬 Scale이 1이어도 조상 뼈에 배율이 있을 수 있으므로 최종 배율을 읽는다.
        Vector3 parentScale = mount.lossyScale;

        // 크기가 0인 축은 나눌 수 없으므로 기존 장착을 유지하도록 실패를 반환한다.
        if (Mathf.Abs(parentScale.x) < 0.000001f || Mathf.Abs(parentScale.y) < 0.000001f || Mathf.Abs(parentScale.z) < 0.000001f)
        {
            Debug.LogWarning("무기 홀더의 크기에 0에 가까운 축이 있어 장착할 수 없다.", mount);
            return false;
        }

        // 위치와 회전은 기존처럼 홀더에 맞춘다.
        pooledRoot.SetParent(mount, false);
        pooledRoot.localPosition = Vector3.zero;
        pooledRoot.localRotation = Quaternion.identity;

        // 부모가 100배면 무기를 100분의 1로 줄여 원래 무기 크기가 되도록 한다.
        pooledRoot.localScale = new Vector3(originalLocalScale.x / parentScale.x, originalLocalScale.y / parentScale.y, originalLocalScale.z / parentScale.z);

        if (gripTransform != null)
        {
            // 크기를 먼저 맞춘 뒤 그랩과 홀더 사이의 실제 위치 차이를 구한다.
            Vector3 positionCorrection = mount.position - gripTransform.position;

            // 무기 전체를 같은 거리만큼 옮겨 손잡이 기준점이 홀더와 겹치게 한다.
            pooledRoot.position += positionCorrection;
        }
        return true;
    }

    private void Reset()
    {
        muzzleTransform = transform.Find("MuzzlePoint");
    }
}
