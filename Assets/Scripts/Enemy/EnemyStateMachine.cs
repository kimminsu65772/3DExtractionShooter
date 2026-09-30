using System;
using UnityEngine;
using UnityEngine.XR;

public class EnemyStateMachine
{
    private EnemyState currentState;

    public EnemyStateType CurrentStateType => currentState != null ? currentState.StateType : EnemyStateType.None;

    public event Action<EnemyStateType, EnemyStateType> OnStateChanged;

    // 초기 상태를 설정하는 메서드
    public void Initialize(EnemyState initialState)
    {
        if (initialState == null)
            throw new ArgumentNullException(nameof(initialState));

        if (currentState != null)
            throw new InvalidOperationException("EnemyStateMachine은 이미 초기화되었습니다.");

        TryChangeState(initialState);
    }

    // 적의 상태 전환을 시도하는 메서드
    public bool TryChangeState(EnemyState nextState)
    {
        if (nextState == null)
            throw new ArgumentNullException(nameof(nextState));
        if (currentState != null && currentState.StateType == nextState.StateType)
            return false;
        
        EnemyStateType previousStateType = CurrentStateType;

        currentState?.Exit();

        currentState = nextState;

        currentState.Enter();

        OnStateChanged?.Invoke(previousStateType, CurrentStateType);

        return true;
    }

    // 매 프레임마다 해당 상태의 Tick 메서드를 호출하는 메서드
    public void Tick()
    {
        currentState?.Tick();
    }
}
