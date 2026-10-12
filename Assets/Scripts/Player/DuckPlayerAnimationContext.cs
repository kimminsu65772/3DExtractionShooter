using UnityEngine;

// 오리 복사본의 자세 문맥만 전달하고 장비 소유권이나 전투 규칙을 변경하지 않는다.
[DisallowMultipleComponent]
public sealed class DuckPlayerAnimationContext : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerWeaponHolder weaponHolder;
    private static readonly int HasWeaponHash = Animator.StringToHash("HasWeapon");

    private void LateUpdate()
    {
        // 빈손의 조심스러운 걷기 입력은 유지하되 총을 드는 상체 자세는 적용하지 않는다.
        animator.SetBool(HasWeaponHash, weaponHolder.CurrentWeaponInstance != null);
    }
}
