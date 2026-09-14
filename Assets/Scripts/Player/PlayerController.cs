using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private const float MoveInputThreshold = 0.01f;

    [Header("참조 설정")]
    [SerializeField] private PlayerInputHandler _inputHandler;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform cameraTransform;

    [Header("플레이어 이동 속도 설정")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float gravity = -9.81f;

    [Header("플레이어 상태 변화 확인")]
    [SerializeField] private float currentMoveSpeed;
    [SerializeField] private float _verticalVelocity;
    [SerializeField] private PlayerLocomotionState playerLocomotionState = PlayerLocomotionState.None;


    private void Awake()
    {
        if (_inputHandler == null)
        {
            Debug.LogError("인풋 핸들러의 참조 설정이 완료되지 않았습니다.");
            return;
        }
            

        if (characterController == null)
        {
            Debug.LogError("캐릭터 컨트롤러의 참조 설정이 완료되지 않았습니다.");
            return;
        }

        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        UpdateLocomotionState();
        MoveCharacter();
    }

    private void UpdateLocomotionState()
    {
        bool hasMoveInput = _inputHandler.MoveInput.sqrMagnitude > MoveInputThreshold * MoveInputThreshold;

        if (!hasMoveInput)
        {
            playerLocomotionState = PlayerLocomotionState.Idle;
            return;
        }
        else
        {
            if (_inputHandler.IsSprintHeld)
            {
                playerLocomotionState = PlayerLocomotionState.Sprint;
                return;
            }
            else
            {
                playerLocomotionState = PlayerLocomotionState.Move;
            }
        }
    }

    private Vector3 CalculateMoveDirection()
    {
        Vector3 moveDirection = Vector3.zero;

        bool hasMoveInput = _inputHandler.MoveInput.sqrMagnitude > MoveInputThreshold * MoveInputThreshold;

        if (!hasMoveInput) return moveDirection;

        // Vector3.ProjectOnPlane를 사용하여 카메라의 forward와 right 벡터를 y축(수직 방향)으로 투영하여
        // 평면상에서의 이동 방향을 계산한다.
        Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        moveDirection = cameraForward * _inputHandler.MoveInput.y + 
            cameraRight * _inputHandler.MoveInput.x;

        return Vector3.ClampMagnitude(moveDirection, 1f);
    }

    private float GetCurrentMoveSpeed()
    {
        switch (playerLocomotionState)
        {
            case PlayerLocomotionState.Idle:
                return 0f;
            case PlayerLocomotionState.Move:
                return moveSpeed;
            case PlayerLocomotionState.Sprint:
                return sprintSpeed;
            default:
                return 0f;
        }
    }

    private void MoveCharacter()
    {
        Vector3 moveDirection = CalculateMoveDirection();
        currentMoveSpeed = GetCurrentMoveSpeed();

        if (characterController.isGrounded && _verticalVelocity < 0f)
        {
            // 접촉을 유지하기 위해 약간의 음수 값이 계속 수직 속도에 적용되도록 한다.
            _verticalVelocity = -2f;
        }
        else
        {
            _verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 velocity = moveDirection * currentMoveSpeed + Vector3.up * _verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }
}
