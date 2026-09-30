using UnityEngine;

public class EnemyAttackState : EnemyState
{
    private HitPart aimedHitPart;

    public override EnemyStateType StateType => EnemyStateType.Attack;

    public EnemyAttackState(EnemyRangedBrain brain) : base(brain)
    {
    }

    public override void Enter()
    {
        Brain.Motor.Stop();

        SelectNextAimPart();
    }

    public override void Tick()
    {
        Transform target = Brain.Perception.Target;

        if (target == null)
        {
            Brain.ChangeState(EnemyStateType.Investigate);
            return;
        }

        // 플레이어가 시야에서 벗어나 인지 수준이 None으로 떨어졌다면 Chase 상태로 전환
        if (Brain.Perception.CurrentLevel == EnemyPerceptionLevel.None)
        {
            Brain.ChangeState(EnemyStateType.Chase);
            return;
        }

        Vector3 horizontalOffset = Vector3.ProjectOnPlane(target.position - Brain.transform.position, Vector3.up);

        float attackExitDistance = Brain.AttackExitDistance;

        if (horizontalOffset.sqrMagnitude > attackExitDistance * attackExitDistance)
        {
            Brain.ChangeState(EnemyStateType.Chase);
            return;
        }

        AimPointProvider aimPointProvider = Brain.Perception.TargetAimPointProvider;

        Vector3 targetPoint = aimPointProvider.GetAimPosition(aimedHitPart);

        // 아직 플레이어 방향으로 충분히 회전하지 못했다면 공격을 시도하지 못하도록 한다.
        if (!Brain.Motor.TryFaceTowards(targetPoint))
            return;

        // 공격 요청이 성공적으로 이루어지면서 한 번의 점사가 끝났다면 다음 공격 지점을 선택한다.
        if (Brain.AttackController.TryAttack(targetPoint) && !Brain.AttackController.IsBurstActive)
            SelectNextAimPart();
    }

    private void SelectNextAimPart()
    {
        aimedHitPart = Random.value < Brain.HeadAimChance ? HitPart.Head : HitPart.Body;
    }
}
