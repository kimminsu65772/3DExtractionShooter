using UnityEngine;

// Animation Event는 Animator가 붙은 GameObject에서만 같은 이름의 메서드를 찾는다.
// 교체 가능한 캐릭터 모델에서 발생한 이벤트를 PlayerRoot의 실제 기능 컴포넌트로 전달한다.
[DisallowMultipleComponent]
public sealed class PlayerAnimationEventRelay : MonoBehaviour
{
    private PlayerAttackController attackController;

    public void Initialize(PlayerAttackController target)
    {
        attackController = target;
    }

    // firing rifle 애니메이션의 OnFireFrame 이벤트가 호출하는 메서드다.
    public void OnFireFrame()
    {
        if (attackController == null)
        {
            attackController = GetComponentInParent<PlayerAttackController>();
        }

        attackController?.OnFireFrame();
    }
}
