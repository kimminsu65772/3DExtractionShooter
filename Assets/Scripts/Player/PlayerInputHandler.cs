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
    // 참조를 생략하면 기존 Player 액션 맵의 Interact를 찾아 사용한다.
    [SerializeField] private InputActionReference _interactAction;
    private InputAction interactInput;
    private bool enabledInteractInput;

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
    public bool IsUIInputBlocked { get; private set; }
    private bool waitForAttackRelease;

    // UI에 마우스 사용권을 넘긴다. 이동과 회피 입력은 그대로 유지한다.
    public void SetUIInputBlocked(bool blocked)
    {
        if (IsUIInputBlocked == blocked) return;
        IsUIInputBlocked = blocked;
        IsAttackHeld = false;
        IsAimHeld = false;
        _attackRequested = false;
        // 창을 닫는 클릭이나 누른 채로 있던 클릭이 곧바로 총을 발사하지 않게 한다.
        waitForAttackRelease = _attackAction != null && _attackAction.action.IsPressed();
    }

    private bool _dodgeRequested;
    private bool _attackRequested;
    private bool _reloadRequested;
    private bool _interactRequested;

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

    // 한 번 누른 상호작용 입력을 한 번만 소비한다. 길게 누르는 동안 반복 실행하지 않는다.
    public bool ConsumeInteractInput()
    {
        if (!_interactRequested) return false;
        _interactRequested = false;
        return true;
    }

    private void OnInteractInput(InputAction.CallbackContext context)
    {
        if (context.performed) _interactRequested = true;
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
        IsAimHeld = !IsUIInputBlocked && context.ReadValueAsButton();

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
        bool pressed = context.ReadValueAsButton();
        if (!pressed) waitForAttackRelease = false;
        IsAttackHeld = !IsUIInputBlocked && !waitForAttackRelease && pressed;

        if (context.performed && IsAttackHeld)
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
        _interactRequested = false;
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
        // 기존 액션을 공유한다. 씬의 InputActionReference를 새로 연결하지 않아도 같은 맵에서 찾는다.
        interactInput = _interactAction != null ? _interactAction.action : _moveAction.action.actionMap?.FindAction("Interact", false);
        if (interactInput != null)
        {
            interactInput.performed += OnInteractInput;
            enabledInteractInput = !interactInput.enabled;
            if (enabledInteractInput) interactInput.Enable();
        }
        else Debug.LogWarning("PlayerInputHandler에 Interact 액션이 없다. Player 액션 맵 또는 참조를 확인한다.", this);
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
        if (interactInput != null)
        {
            interactInput.performed -= OnInteractInput;
            // 다른 입력 컴포넌트가 이미 켠 액션은 이 컴포넌트가 끄지 않는다.
            if (enabledInteractInput) interactInput.Disable();
            interactInput = null;
            enabledInteractInput = false;
        }

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
        _interactRequested = false;
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
