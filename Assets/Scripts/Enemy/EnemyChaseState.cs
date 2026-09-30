using UnityEngine;

public class EnemyChaseState : EnemyState
{
    private float targetLostTimer;

    public override EnemyStateType StateType => EnemyStateType.Chase;

    public EnemyChaseState(EnemyRangedBrain brain) : base(brain)
    {
    }

    public override void Enter()
    {
        targetLostTimer = 0f;
    }

    public override void Tick()
    {
        if (Brain.Perception.CurrentLevel == EnemyPerceptionLevel.None)
        {
            targetLostTimer += Time.deltaTime;

            if (targetLostTimer >= Brain.TargetLostGraceDuration)
            {
                Brain.ChangeState(EnemyStateType.Investigate);
                return;
            }
        }
        else
        {
            targetLostTimer = 0f;
        }

        // 추적 상태에서는 Brain에게 목표 위치를 전달하여 이동하도록 한다.
        Transform target = Brain.Perception.Target;

        if (target == null)
        {
            Brain.ChangeState(EnemyStateType.Investigate);
            return;
        }

        Vector3 targetPosition = target.position;

        // 플레이어와 적 사이의 수평 거리만 계산한다.
        Vector3 HorizontalOffset = Vector3.ProjectOnPlane(targetPosition - Brain.transform.position, Vector3.up);

        // 만약 적이 플레이어를 확인했고, 수평 거리가 공격 진입 거리보다 작거나 같다면 공격 상태로 전환한다.
        if (Brain.Perception.IsConfirmed && 
            HorizontalOffset.sqrMagnitude <= Brain.AttackEnterDistance * Brain.AttackEnterDistance)
        {
            Brain.ChangeState(EnemyStateType.Attack);
            return;
        }

        Brain.Motor.TryMoveTo(targetPosition, Brain.AttackEnterDistance);
    }

    public override void Exit()
    {
        targetLostTimer = 0f;
    }
}
