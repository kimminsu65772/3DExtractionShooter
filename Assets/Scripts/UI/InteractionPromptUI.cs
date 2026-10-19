using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UI-01 / WORLD-02: 대상 선택은 PlayerInteractionController에 맡기고 화면 표시만 갱신한다.
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class InteractionPromptUI : MonoBehaviour
{

    private PlayerInteractionController source;
    private PlayerInputHandler playerInput;
    private Camera worldCamera;
    private Canvas canvas;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private TMP_Text actionText;
    private IInteractable target;
    private Component targetComponent;
    private Transform anchor;
    private Renderer[] targetRenderers;
    private Collider[] targetColliders;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null || visualRoot == null || actionText == null)
        {
            Debug.LogError("상호작용 HUD의 Canvas/표시 루트/문구 참조가 필요합니다.", this);
            enabled = false;
            return;
        }
        Hide();
    }

    public void SetSource(PlayerInteractionController interaction, Camera camera)
    {
        if (source != null) source.OnTargetChanged -= HandleTargetChanged;
        source = interaction;
        worldCamera = camera;
        playerInput = source != null ? source.GetComponent<PlayerInputHandler>() : null;
        if (source != null && isActiveAndEnabled) source.OnTargetChanged += HandleTargetChanged;
        HandleTargetChanged(source != null ? source.CurrentTarget : null);
        Hide();
    }

    private void OnEnable()
    {
        if (source != null)
        {
            source.OnTargetChanged += HandleTargetChanged;
            HandleTargetChanged(source.CurrentTarget);
        }
    }

    private void OnDisable()
    {
        if (source != null) source.OnTargetChanged -= HandleTargetChanged;
        Hide();
    }

    private void HandleTargetChanged(IInteractable current)
    {
        target = current;
        targetComponent = current as Component;
        anchor = targetComponent != null ? targetComponent.transform.Find("InteractionAnchor") : null;
        // 대상이 바뀔 때만 배열을 확보하고, 같은 물체를 따라갈 때는 다시 수집하지 않는다.
        targetRenderers = targetComponent != null ? targetComponent.GetComponentsInChildren<Renderer>() : null;
        targetColliders = targetComponent != null ? targetComponent.GetComponentsInChildren<Collider>() : null;
    }

    private void LateUpdate()
    {
        if (visualRoot == null || actionText == null || source == null || !source.isActiveAndEnabled ||
            UIManager.IsLoading() || (UIManager.Instance != null && UIManager.Instance.HasOpenPopups) ||
            (playerInput != null && playerInput.IsGameplayInputBlocked))
        {
            Hide();
            return;
        }

        // 재활성화나 실행 순서로 이벤트를 놓쳤어도 현재 선택 대상과 대조해 복구한다.
        if (!ReferenceEquals(target, source.CurrentTarget)) HandleTargetChanged(source.CurrentTarget);
        if (targetComponent == null || !targetComponent.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }
        if (!TryGetCanvasPosition(out Vector2 local))
        {
            Hide();
            return;
        }
        visualRoot.anchoredPosition = local;
        string label = target.InteractionLabel;
        if (actionText.text != label) actionText.text = label;
        visualRoot.gameObject.SetActive(true);
    }

    // 월드 카메라 투영과 Canvas 변환을 한 단계로 묶어 표시 루프에서 분리한다.
    private bool TryGetCanvasPosition(out Vector2 local)
    {
        local = default;
        if (worldCamera == null || !worldCamera.isActiveAndEnabled) worldCamera = Camera.main;
        if (worldCamera == null) return false;

        Vector3 screen = worldCamera.WorldToScreenPoint(GetTargetCenter());
        if (screen.z <= 0 || !worldCamera.pixelRect.Contains(new Vector2(screen.x, screen.y)))
        {
            return false;
        }
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, uiCamera, out local))
        {
            return false;
        }
        return true;
    }

    private Vector3 GetTargetCenter()
    {
        if (anchor != null) return anchor.position;
        Bounds bounds = default;
        bool found = false;
        foreach (Renderer renderer in targetRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is ParticleSystemRenderer ||
                !ReferenceEquals(renderer.GetComponentInParent<IInteractable>(), target)) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (found) return bounds.center;
        foreach (Collider collider in targetColliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                !ReferenceEquals(collider.GetComponentInParent<IInteractable>(), target)) continue;
            if (!found) { bounds = collider.bounds; found = true; }
            else bounds.Encapsulate(collider.bounds);
        }
        return found ? bounds.center : targetComponent.transform.position;
    }

    private void Hide()
    {
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
    }
}
