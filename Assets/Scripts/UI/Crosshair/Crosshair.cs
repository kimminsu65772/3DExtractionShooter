using UnityEngine;

public class Crosshair : MonoBehaviour
{
    [SerializeField] private PlayerAimCursor aimCursor;
    [SerializeField] private RectTransform crosshairRect;

    private void Awake()
    {
        if (crosshairRect == null)
        {
            crosshairRect = TryGetComponent<RectTransform>(out var rectTransform) ? rectTransform : null;

            if (crosshairRect == null)
            {
                Debug.LogError("RectTransform이 없는 오브젝트에 컴포넌트가 부착되어 있습니다.");
                return;
            }
        }

        if (aimCursor == null)
        {
            Debug.LogError("PlayerAimCursor 참조가 설정되지 않았습니다.", this);
        }
    }

    private void Update()
    {
        UpdateCrosshairPos();
    }

    private void UpdateCrosshairPos()
    {
        if (aimCursor == null)
        {
            return;
        }

        aimCursor.RefreshCursor();
        crosshairRect.position = aimCursor.AimScreenPosition;
    }
}
