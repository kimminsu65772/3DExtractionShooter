using UnityEngine;

// 개별 클립에 이벤트를 넣지 않고 공통 Dead 상태에서 재생 완료를 알린다.
public class EnemyDeathAnimationBehaviour : StateMachineBehaviour
{
    private bool hasNotified;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        hasNotified = false;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 교체된 클립을 끝까지 재생하면 한 번만 적 루트에 알린다.
        if (hasNotified || stateInfo.normalizedTime < 1f)
            return;

        hasNotified = true;
        animator.SendMessageUpwards("OnDeathAnimationCompleted", SendMessageOptions.DontRequireReceiver);
    }
}
