using System;
using UnityEngine;

// UI 간격만 담당한다. 탄퍼짐 각도를 UI 거리로 변환하는 작업은 호출 측에서 수행한다.
[ExecuteAlways]
[DisallowMultipleComponent]
public class CrosshairSpreadView : MonoBehaviour
{
    [Serializable]
    private struct Part
    {
        public RectTransform rect;
        public Vector2 closedPosition;
        public Vector2 outwardDirection;
    }

    [SerializeField] private Part[] parts;
    [SerializeField, Min(0f)] private float spread;

    public void SetSpread(float distance)
    {
        spread = Mathf.Max(0f, distance);
        ApplySpread();
    }

    private void OnEnable() => ApplySpread();
    private void OnValidate() => ApplySpread();

    private void ApplySpread()
    {
        if (parts == null) return;

        foreach (Part part in parts)
        {
            if (part.rect != null)
                part.rect.anchoredPosition =
                    part.closedPosition + part.outwardDirection.normalized * spread;
        }
    }
}
