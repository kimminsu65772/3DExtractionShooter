using UnityEngine;

[DisallowMultipleComponent]
public sealed class VisibilityTarget : MonoBehaviour
{
    [SerializeField] private Transform visibilityPoint;

    [Header("표시 대상")]
    [SerializeField] private Renderer[] controlledRenderers;

    public Vector3 ViewPoint =>
        visibilityPoint != null
            ? visibilityPoint.position
            : transform.position;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        if (controlledRenderers == null ||
            controlledRenderers.Length == 0)
        {
            controlledRenderers = GetComponentsInChildren<Renderer>(true);
        }

        // FOV가 처음 탐지하기 전에는 숨긴 상태로 시작한다.
        IsVisible = false;
        ApplyVisibility();
    }

    public void RefreshRenderers()
    {
        controlledRenderers = GetComponentsInChildren<Renderer>(true);

        ApplyVisibility();
    }

    public void SetVisible(bool visible)
    {
        if (IsVisible == visible)
            return;

        IsVisible = visible;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (controlledRenderers == null)
            return;

        for (int i = 0; i < controlledRenderers.Length; i++)
        {
            Renderer targetRenderer =
                controlledRenderers[i];

            if (targetRenderer == null)
                continue;

            // 보이지 않는 상태에서는 렌더링을 강제로 끈다.
            targetRenderer.forceRenderingOff = !IsVisible;
        }
    }
}