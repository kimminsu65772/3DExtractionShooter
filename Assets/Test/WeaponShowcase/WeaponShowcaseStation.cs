using UnityEngine;

// Animator와 같은 오브젝트에서 사격 프레임을 받고 구역의 고정 자세를 적용한다.
public sealed class WeaponShowcaseStation : MonoBehaviour
{
    public WeaponShowcase showcase;
    public Animator animator;
    public Transform mount;
    public int pose;

    public void ApplyPose()
    {
        animator.SetBool("HasWeapon", true);
        animator.SetBool("IsAiming", pose >= 2);
        animator.SetBool("IsSprinting", pose == 1);
        animator.SetBool("IsFiring", false);
        animator.SetInteger("ActionState", 0);
        animator.SetFloat("MoveX", 0);
        animator.SetFloat("MoveY", pose == 1 ? 1 : 0);
        animator.SetFloat("MoveAmount", pose == 1 ? 1 : 0);
        animator.ResetTrigger("Fire");
    }

    public void OnFireFrame() { if (pose == 3) showcase.OnShotFrame(); }
}
