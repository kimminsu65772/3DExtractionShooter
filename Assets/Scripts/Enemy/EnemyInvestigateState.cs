using UnityEngine;

public class EnemyInvestigateState : EnemyState
{
    private float waitTimer = 0f;
    public override EnemyStateType StateType => EnemyStateType.Investigate;

    public EnemyInvestigateState(EnemyRangedBrain brain) : base(brain)
    {
    }

    public override void Enter()
    {
        waitTimer = 0f;

        if (!Brain.Perception.HasLastKnownPosition)
        {
            // 마지막으로 플레이어를 확인한 위치가 없으면 Idle 상태로 전환
            Brain.ChangeState(EnemyStateType.Idle);
            return;
        }

        Brain.Motor.TryMoveTo(Brain.Perception.LastKnownPosition, Brain.InvestigateStoppingDistance, true);
    }

    public override void Tick()
    {
        // 조사 중 플레이어를 가까운 시야에서 확인했다면 추적 상태로 전환
        if (Brain.Perception.CurrentLevel == EnemyPerceptionLevel.Confirmed)
        {
            Brain.ChangeState(EnemyStateType.Chase);
            return;
        }

        // 조사를 하다가 더이상 플레이어의 마지막 위치를 알 수 없게 되면 Idle 상태로 전환
        if (!Brain.Perception.HasLastKnownPosition)
        {
            Brain.ChangeState(EnemyStateType.Idle);
            return;
        }

        // 조사 중에 먼 거리에서 플레이어를 확인했다면 마지막 목격 위치를 갱신하도록 한다.
        if (Brain.Perception.CurrentLevel == EnemyPerceptionLevel.Suspicious)
        {
            waitTimer = 0f;

            Brain.Motor.TryMoveTo(Brain.Perception.LastKnownPosition, Brain.InvestigateStoppingDistance);

            return;
        }

        // 단서를 잃었지만 아직 마지막 위치에 도착하지 못했다면 계속 이동하도록 한다.
        if (!Brain.Motor.HasReachedDestination)
        {
            waitTimer = 0f;
            return;
        }

        // 목격 위치에 도착한 뒤에 조사 시간을 계산한다.
        // 만약 정해진 시간 안에 플레이어를 확인하지 못한다면 Idle 상태로 전환한다.
        waitTimer += Time.deltaTime;

        if (waitTimer >= Brain.InvestigationWaitDuration)
        {
            Brain.Perception.ClearLastKnownPosition();
            Brain.ChangeState(EnemyStateType.Idle);
        }
    }

    public override void Exit()
    {
        waitTimer = 0f;
    }
}
