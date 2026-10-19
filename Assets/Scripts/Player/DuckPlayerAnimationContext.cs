using UnityEngine;

// 오리 복사본의 자세 문맥만 전달하고 장비 소유권이나 전투 규칙을 변경하지 않는다.
[DisallowMultipleComponent]
public sealed class DuckPlayerAnimationContext : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerWeaponHolder weaponHolder;
    private static readonly int HasWeaponHash = Animator.StringToHash("HasWeapon");
    private static readonly int BasePoseHash = Animator.StringToHash("Arms.BasePose");
    private static readonly int FreePoseHash = Animator.StringToHash("FullBodyAction.Free");
    private int armsLayerIndex;
    private int fullBodyLayerIndex;

    private void Awake()
    {
        armsLayerIndex = animator.GetLayerIndex("Arms");
        fullBodyLayerIndex = animator.GetLayerIndex("FullBodyAction");
    }

    private void LateUpdate()
    {
        // 빈손의 조심스러운 걷기 입력은 유지하되 총을 드는 상체 자세는 적용하지 않는다.
        animator.SetBool(HasWeaponHash, weaponHolder.CurrentWeaponInstance != null);

        // 비조준 전환이 끝나면 상체 덮기를 해제해 하위 레이어의 걷기·달리기까지 복원한다.
        // 전환 중에는 가중치를 유지해 Aim·Fire에서 기본 자세로 부드럽게 돌아간다.
        if (armsLayerIndex >= 0)
        {
            bool isBasePose = animator.GetCurrentAnimatorStateInfo(armsLayerIndex).fullPathHash == BasePoseHash;
            bool isTransitioning = animator.IsInTransition(armsLayerIndex);
            animator.SetLayerWeight(armsLayerIndex, isBasePose && !isTransitioning ? 0f : 1f);
        }

        // 빈 Free 상태에서는 마지막 회피 자세가 남으므로 전신 덮기를 해제한다.
        // 회피와 복귀 전환 중에는 가중치를 유지해 하위 레이어로 부드럽게 돌아간다.
        if (fullBodyLayerIndex >= 0)
        {
            bool isFreePose = animator.GetCurrentAnimatorStateInfo(fullBodyLayerIndex).fullPathHash == FreePoseHash;
            bool isTransitioning = animator.IsInTransition(fullBodyLayerIndex);
            animator.SetLayerWeight(fullBodyLayerIndex, isFreePose && !isTransitioning ? 0f : 1f);
        }
    }
}
