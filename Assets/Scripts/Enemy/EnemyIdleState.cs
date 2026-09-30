using UnityEngine;

public class EnemyIdleState : EnemyState
{
    public override EnemyStateType StateType => EnemyStateType.Idle;

    public EnemyIdleState(EnemyRangedBrain brain) : base(brain)
    {
    }

    public override void Enter()
    {
        // 대기 상태에 진입할 때 이전 상태에 사용하던 이동 경로를 제거한다.
        Brain.Motor.Stop();
    }

    public override void Tick()
    {
        switch (Brain.Perception.CurrentLevel)
        {
            case EnemyPerceptionLevel.None:
                // 플레이어를 인식하지 못한 상태에서는 Idle 상태를 유지한다.
                break;
            case EnemyPerceptionLevel.Suspicious:
                // 의심하는 상태로 전환한다.
                Brain.ChangeState(EnemyStateType.Investigate);
                break;
            case EnemyPerceptionLevel.Confirmed:
                // 플레이어를 확인한 상태로 전환합니다.
                Brain.ChangeState(EnemyStateType.Chase);
                break;
        }
    }

    public override void Exit()
    {
        base.Exit();
        // 적이 Idle 상태에서 나갈 때의 로직을 여기에 작성합니다.
        Debug.Log("적이 Idle 상태에서 빠져나옴.");
    }
}
