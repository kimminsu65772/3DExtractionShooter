using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerAimCursor : MonoBehaviour
{
    [Header("참조 설정")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private PlayerRecoilController recoilController;

    [Header("커서 설정")]
    [SerializeField] private float aimSensitivity = 1f;
    [SerializeField] private bool hideSystemCursor = true;
    [SerializeField] private bool confineSystemCursor = true;

    private Vector2 baseAimScreenPosition;
    private Vector2 previousRawMousePosition;
    private bool isInitialized;
    private int lastRefreshFrame = -1;

    public Vector2 BaseAimScreenPosition => baseAimScreenPosition;
    public Vector2 RecoilScreenOffset => recoilController != null ? recoilController.CurrentScreenRecoilOffset : Vector2.zero;
    public Vector2 AimScreenPosition => ClampScreenPosition(baseAimScreenPosition + RecoilScreenOffset);

    private void Awake()
    {
        ResolveReferences();
        InitializeAimPosition();
    }

    private void OnEnable()
    {
        if (hideSystemCursor)
        {
            Cursor.visible = false;
        }

        if (confineSystemCursor)
        {
            Cursor.lockState = CursorLockMode.Confined;
        }
    }

    private void OnDisable()
    {
        if (hideSystemCursor)
        {
            Cursor.visible = true;
        }

        if (confineSystemCursor && Cursor.lockState == CursorLockMode.Confined)
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void Update()
    {
        RefreshCursor();
    }

    public void RefreshCursor()
    {
        if (lastRefreshFrame == Time.frameCount)
            return;

        lastRefreshFrame = Time.frameCount;

        ResolveReferences();

        if (!isInitialized)
        {
            InitializeAimPosition();
        }

        Vector2 aimDelta = ReadAimDelta() * aimSensitivity;

        if (recoilController != null)
        {
            aimDelta = recoilController.ConsumeScreenRecoil(aimDelta);
        }

        baseAimScreenPosition = ClampScreenPosition(baseAimScreenPosition + aimDelta);
    }

    private Vector2 ReadAimDelta()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.delta.ReadValue();
        }

        if (inputHandler == null)
            return Vector2.zero;

        Vector2 currentRawMousePosition = inputHandler.AimScreenPosition;
        Vector2 delta = currentRawMousePosition - previousRawMousePosition;
        previousRawMousePosition = currentRawMousePosition;

        return delta;
    }

    private void InitializeAimPosition()
    {
        ResolveReferences();

        if (inputHandler != null && inputHandler.AimScreenPosition != Vector2.zero)
        {
            baseAimScreenPosition = ClampScreenPosition(inputHandler.AimScreenPosition);
            previousRawMousePosition = inputHandler.AimScreenPosition;
        }
        else
        {
            baseAimScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            previousRawMousePosition = baseAimScreenPosition;
        }

        isInitialized = true;
    }

    private Vector2 ClampScreenPosition(Vector2 screenPosition)
    {
        screenPosition.x = Mathf.Clamp(screenPosition.x, 0f, Screen.width);
        screenPosition.y = Mathf.Clamp(screenPosition.y, 0f, Screen.height);

        return screenPosition;
    }

    private void ResolveReferences()
    {
        if (inputHandler == null)
        {
            inputHandler = GetComponent<PlayerInputHandler>();
        }

        if (recoilController == null)
        {
            recoilController = GetComponent<PlayerRecoilController>();
        }
    }
}
