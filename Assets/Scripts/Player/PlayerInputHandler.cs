using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    // 인풋 시스템에 바인딩된 액션들을 참조하긴 위한 변수들
    [Header("Input Actions")]
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private InputActionReference _aimPositionAction;
    [SerializeField] private InputActionReference _sprintAction;
    [SerializeField] private InputActionReference _dodgeAction;
    [SerializeField] private InputActionReference _aimAction;
    [SerializeField] private InputActionReference _attackAction;
    [SerializeField] private InputActionReference _reloadAction;

#if UNITY_EDITOR
    [Header("Input Debug")]
    [SerializeField] private bool _showInputDebugOverlay = true;
    [SerializeField] private bool _logButtonInput = true;
#endif

    public Vector2 MoveInput { get; private set; }
    public Vector2 AimScreenPosition { get; private set; }
    public bool IsSprintHeld { get; private set; }
    public bool IsAimHeld { get; private set; }
    public bool IsAttackHeld { get; private set; }

    private bool _dodgeRequested;
    private bool _attackRequested;
    private bool _reloadRequested;

    private void OnEnable()
    {
        SubscribeInputEvents();
        SyncCurrentInputState();
    }
    private void OnDisable()
    {
        UnsubscribeInputEvents();
        ResetInputState();
    }

    public bool ConsumeDodgeInput()
    {
        if (!_dodgeRequested) return false;
        _dodgeRequested = false;
        return true;
    }

    public bool ConsumeAttackInput()
    {
        if (!_attackRequested) return false;
        _attackRequested = false;
        return true;
    }

    public bool ConsumeReloadInput()
    {
        if (!_reloadRequested) return false;
        _reloadRequested = false;
        return true;
    }

    private void OnMoveInput(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    private void OnAimPositionInput(InputAction.CallbackContext context)
    {
        AimScreenPosition = context.ReadValue<Vector2>();
    }

    private void OnSprintInput(InputAction.CallbackContext context)
    {
        IsSprintHeld = context.ReadValueAsButton();

#if UNITY_EDITOR
        LogButtonInput("Sprint", context.phase, IsSprintHeld);
#endif
    }

    private void OnAimInput(InputAction.CallbackContext context)
    {
        IsAimHeld = context.ReadValueAsButton();

#if UNITY_EDITOR
        LogButtonInput("Aim", context.phase, IsAimHeld);
#endif
    }

    private void OnDodgeInput(InputAction.CallbackContext context)
    {
        _dodgeRequested = true;

#if UNITY_EDITOR
        LogButtonInput("Dodge", context.phase, true);
#endif
    }

    private void OnAttackInput(InputAction.CallbackContext context)
    {
        IsAttackHeld = context.ReadValueAsButton();

        if (context.performed)
            _attackRequested = true;

#if UNITY_EDITOR
        LogButtonInput("Attack", context.phase, IsAttackHeld);
#endif
    }

    private void OnReloadInput(InputAction.CallbackContext context)
    {
        if (context.performed)
            _reloadRequested = true;
#if UNITY_EDITOR
        LogButtonInput("Reload", context.phase, true);
#endif
    }

    private void ResetInputState()
    {
        MoveInput = Vector2.zero;
        AimScreenPosition = Vector2.zero;
        IsSprintHeld = false;
        IsAimHeld = false;
        IsAttackHeld = false;
        _dodgeRequested = false;
        _attackRequested = false;
        _reloadRequested = false;
    }

    private void SubscribeInputEvents()
    {
        _moveAction.action.performed += OnMoveInput;
        _moveAction.action.canceled += OnMoveInput;
        _aimPositionAction.action.performed += OnAimPositionInput;
        _aimPositionAction.action.canceled += OnAimPositionInput;
        _sprintAction.action.performed += OnSprintInput;
        _sprintAction.action.canceled += OnSprintInput;
        _aimAction.action.performed += OnAimInput;
        _aimAction.action.canceled += OnAimInput;
        _attackAction.action.performed += OnAttackInput;
        _attackAction.action.canceled += OnAttackInput;
        _dodgeAction.action.performed += OnDodgeInput;
        _reloadAction.action.performed += OnReloadInput;
    }

    private void UnsubscribeInputEvents()
    {
        _moveAction.action.performed -= OnMoveInput;
        _moveAction.action.canceled -= OnMoveInput;
        _aimPositionAction.action.performed -= OnAimPositionInput;
        _aimPositionAction.action.canceled -= OnAimPositionInput;
        _sprintAction.action.performed -= OnSprintInput;
        _sprintAction.action.canceled -= OnSprintInput;
        _aimAction.action.performed -= OnAimInput;
        _aimAction.action.canceled -= OnAimInput;
        _attackAction.action.performed -= OnAttackInput;
        _attackAction.action.canceled -= OnAttackInput;
        _dodgeAction.action.performed -= OnDodgeInput;
        _reloadAction.action.performed -= OnReloadInput;

    }

    private void SyncCurrentInputState()
    {
        MoveInput = _moveAction.action.ReadValue<Vector2>();
        AimScreenPosition = _aimPositionAction.action.ReadValue<Vector2>();
        IsSprintHeld = _sprintAction.action.IsPressed();
        IsAimHeld = _aimAction.action.IsPressed();
        IsAttackHeld = _attackAction.action.IsPressed();

        _dodgeRequested = false;
        _attackRequested = false;
        _reloadRequested = false;
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!_showInputDebugOverlay)
            return;

        GUILayout.BeginArea(new Rect(16f, 16f, 340f, 190f), GUI.skin.box);
        GUILayout.Label("Player Input Debug");
        GUILayout.Label($"Move: {MoveInput:F2}");
        GUILayout.Label($"Aim Screen Position: {AimScreenPosition:F0}");
        GUILayout.Label($"Sprint Held: {IsSprintHeld}");
        GUILayout.Label($"Aim Held: {IsAimHeld}");
        GUILayout.Label($"Attack Held: {IsAttackHeld}");
        GUILayout.Label($"Dodge Requested: {_dodgeRequested}");
        GUILayout.Label($"Attack Requested: {_attackRequested}");
        GUILayout.Label($"Reload Requested: {_reloadRequested}");
        GUILayout.EndArea();
    }

    private void LogButtonInput(string actionName, InputActionPhase phase, bool isPressed)
    {
        if (!_logButtonInput)
            return;

        Debug.Log($"[PlayerInput] {actionName} | Phase: {phase} | Pressed: {isPressed}", this);
    }
#endif
}
