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
        Brain.HitPartProvider.InactiveHitBoxes();


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
