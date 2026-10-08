using UnityEngine;

public class EnemyDeadState : EnemyState
{
    public override EnemyStateType StateType => EnemyStateType.Dead;
    public EnemyDeadState(EnemyRangedBrain brain) : base(brain)
    {
    }

    public override void Enter()
    {
        Brain.Motor.Stop();
        Brain.AttackController.CancelReload();
        
        EnemyLootDropper lootDropper = Brain.GetComponent<EnemyLootDropper>();

        if (lootDropper == null || !lootDropper.TryDropLoot())
            Debug.LogWarning("적 사망 시 전리품을 생성하지 못했다.", Brain);

        // 풀에서 빌린 무기는 충돌 설정을 기억해 반환 때 원래 상태로 복구한다.
        Brain.GetComponent<EnemyWeaponLoadout>()?.DisableWeaponCollisionForDeath();
        Brain.HitPartProvider?.InactiveHitBoxes();

        // 모델과 본은 사망 연출에 남겨두고 총알을 막는 충돌체만 즉시 해제한다.
        foreach (Collider hitCollider in Brain.GetComponentsInChildren<Collider>(true))
            hitCollider.enabled = false;

        // 연결된 Animator가 있으면 연출 완료 후 제거하고, 없으면 기존처럼 즉시 제거한다.
        EnemyAnimationController animationController = Brain.GetComponent<EnemyAnimationController>();
        if (animationController != null && animationController.TryPlayDeath())
            return;

        GameObject.Destroy(Brain.gameObject);
    }

    public override void Tick()
    {
        // 사망 상태에서는 아무 동작도 하지 않음
    }

    public override void Exit()
    {

    }
}
