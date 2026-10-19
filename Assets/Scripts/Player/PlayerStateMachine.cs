using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStateMachine : MonoBehaviour
{
    // 게임을 시작했을 때 최초 행동 상태
    [SerializeField] private PlayerActionState initialState = PlayerActionState.Free;

    public PlayerActionState CurrentState { get; private set; }

    // 플레이어의 행동 상태가 변경될 때 이전 상태와 새로운 상태를 전달하는 이벤트
    public event Action<PlayerActionState, PlayerActionState> OnStateChanged;

    private void Awake()
    {
        CurrentState = initialState;
    }

    public bool TryChangeState(PlayerActionState nextState)
    {
        if (CurrentState == PlayerActionState.Dead || CurrentState == nextState)
        {
            return false; // 사망 후 동작 종료 알림이 Free로 되돌리는 것도 막는다.
        }
        
        PlayerActionState playerActionState = CurrentState;

        CurrentState = nextState;

        OnStateChanged?.Invoke(playerActionState, CurrentState);

        return true;
    }
}
