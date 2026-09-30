using System;
using UnityEngine;

public abstract class EnemyState
{
    protected EnemyRangedBrain Brain { get; }
    public abstract EnemyStateType StateType { get; }

    protected EnemyState(EnemyRangedBrain brain)
    {
        Brain = brain ?? throw new ArgumentNullException(nameof(brain));
    }

    // 상태 진입 시 호출되는 메서드
    public virtual void Enter()
    {
        // 상태 진입 시 필요한 초기화 작업 수행
    }

    // 해당 상태가 유지되는 동안 호출되는 메서드
    // Enter와 Exit은 반드시 필요하지 않아서 virtual로 정의했지만, Tick은 반드시 구현해야 하므로 abstract로 정의
    public abstract void Tick();

    // 상태 종료 시 호출되는 메서드
    public virtual void Exit()
    {
        // 상태 종료 시 필요한 정리 작업 수행
    }
}
