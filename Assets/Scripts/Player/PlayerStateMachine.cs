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
        if (CurrentState == nextState)
        {
            return false; // 이미 같은 상태라면 변경하지 않음
        }
        
        PlayerActionState playerActionState = CurrentState;

        CurrentState = nextState;

        OnStateChanged?.Invoke(playerActionState, CurrentState);

        return true;
    }
}
