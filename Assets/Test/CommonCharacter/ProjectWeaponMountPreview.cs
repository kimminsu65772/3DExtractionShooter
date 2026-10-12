using ExtractionShooter.DataPipeline;
using UnityEngine;

// 외형 부착 시험만 수행한다. 실제 게임의 카탈로그와 풀은 구성하지 않는다.
[DefaultExecutionOrder(210)]
public sealed class ProjectWeaponMountPreview : MonoBehaviour
{
    public WeaponView weaponView;
    public Transform weaponMount;

    private void Start()
    {
        // 풀 대여 콜백과 같은 초기화를 수행한 뒤 실제 게임의 장착 메서드를 호출한다.
        // 이 대여권은 시험 전용이며 풀에 등록하거나 반환하지 않는다.
        weaponView.OnRent(new PoolLease(1, weaponView.gameObject, 1), null);
        if (!weaponView.TryAttachTo(weaponMount))
            Debug.LogError("시험 무기 부착에 실패했다.", this);
    }
}
