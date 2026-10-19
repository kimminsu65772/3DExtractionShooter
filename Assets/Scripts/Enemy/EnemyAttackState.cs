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
        Brain.Motor.Stop(); // [USEC 리뷰] 기존 동작이다. 공격 진입 시 추적 이동을 끊으므로 추가 이동 기능이 없는 일반 적은 여기서 정지한다.
        Brain.CombatMovement?.ResetMovement(); // [USEC 리뷰] 이동 사격 기능이 있는 적만 전투 목적지를 초기화한다. null이면 호출하지 않는다.

        SelectNextAimPart();
    }

    public override void Tick()
    {
        Transform target = Brain.Perception.Target;

        if (Brain.CombatMovement != null && !Brain.Perception.IsConfirmed) // [USEC 리뷰] USEC은 직접 시야 확인을 잃으면 실제 플레이어 좌표로 계속 이동·사격하지 않는다.
        {
            Brain.ChangeState(EnemyStateType.Investigate); // [USEC 리뷰] 개인 Perception의 마지막 단서를 사용하는 기존 조사 상태로 넘긴다.
            return; // [USEC 리뷰] 이번 프레임의 전투 이동과 발사 요청을 모두 건너뛴다.
        }

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

        if (Brain.CombatMovement != null) // [USEC 리뷰] 핵심 분기다. 컴포넌트가 없거나 비활성이면 일반 적은 이동 사격 코드에 들어오지 않는다.
        {
            Brain.Motor.SetCombatMovement(true); // [USEC 리뷰] 기존 기본 이동속도에 전투 이동 배율을 적용한다. 이동을 시작시키는 명령은 아니다.
            Brain.CombatMovement.Tick(target.position); // [USEC 리뷰] 접근·후퇴·측면 목적지를 고르고 Motor에 이동을 요청한다. 아래의 발사 코드는 계속 실행된다.
        }

        // 아직 플레이어 방향으로 충분히 회전하지 못했다면 공격을 시도하지 못하도록 한다.
        if (!Brain.Motor.TryFaceTowards(targetPoint)) // [USEC 리뷰] 기존 조준 코드다. Agent의 경로 방향 회전을 끄고 이동 중에도 몸은 목표를 바라보게 한다.
            return; // [USEC 리뷰] 아직 조준 방향이 안 맞으면 이번 발사만 생략한다. 이미 요청한 Agent 이동은 취소하지 않는다.

        // 공격 요청이 성공적으로 이루어지면서 한 번의 점사가 끝났다면 다음 공격 지점을 선택한다.
        if (Brain.AttackController.TryAttack(targetPoint) && !Brain.AttackController.IsBurstActive) // [USEC 리뷰] 기존 발사 코드다. 이동 노드의 종료를 기다리지 않으므로 같은 Attack 상태에서 이동과 사격이 병행된다.
            SelectNextAimPart();
    }

    private void SelectNextAimPart()
    {
        aimedHitPart = Random.value < Brain.HeadAimChance ? HitPart.Head : HitPart.Body;
    }

    public override void Exit() // [USEC 리뷰] 이동 사격이 켜진 적이 공격 상태를 벗어날 때만 추가 설정을 복구한다.
    {
        if (Brain.CombatMovement == null) return; // [USEC 리뷰] 이 기능을 쓰지 않는 일반 적의 기존 상태 전환에는 추가 처리를 하지 않는다.
        Brain.CombatMovement.ResetMovement(); // [USEC 리뷰] 다음 공격 진입에서 이전 전투 목적지를 재사용하지 않는다.
        Brain.Motor.SetCombatMovement(false); // [USEC 리뷰] 추적·조사에서는 전투 감속이 남지 않도록 기본 속도로 복원한다.
        Brain.Motor.SetAutomaticRotation(true); // [USEC 리뷰] 공격 중 별도로 조준하던 회전을 다시 Agent 이동 방향에 맡긴다.
    }
}
