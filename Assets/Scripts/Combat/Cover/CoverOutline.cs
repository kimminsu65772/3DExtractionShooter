using UnityEngine;

public class CoverOutline : MonoBehaviour
{
    [SerializeField] private DestructibleCover ownerCover;
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private float outlineWidth = 5f;

    // 셰이더 속성 이름을 정수 ID로 캐싱
    private static readonly int OutlineSizeId = Shader.PropertyToID("_OutlineSize");

    // 두께 변경에 사용할 값을 담고 재사용하기 위해 MaterialPropertyBlock을 사용
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (ownerCover != null)
        {
            ownerCover.OnCoverStateChanged += HandleCoverStateChanged;

            // 구독 직후 현재 상황을 반영한다.
            HandleCoverStateChanged(ownerCover.IsAvailable && ownerCover.CoveredPlayer != null);
        }
    }

    private void OnDisable()
    {
        if (ownerCover != null)
        {
            ownerCover.OnCoverStateChanged -= HandleCoverStateChanged;

            // 컴포넌트가 꺼져도 외곽선이 남지 않도록 해제한다.
            HandleCoverStateChanged(false);
        }
    }

    private void HandleCoverStateChanged(bool isCovered)
    {
        float width = isCovered ? outlineWidth : 0f;

        foreach (Renderer target in targetRenderers)
        {
            if (target != null)
            {
                // 기존 색상 등의 속성을 유지하면서 두께만 변경하기 위해 MaterialPropertyBlock을 사용
                target.GetPropertyBlock(propertyBlock);

                // 외각선 두께 설정
                propertyBlock.SetFloat(OutlineSizeId, width);

                // 변경된 속성을 렌더러에 적용
                target.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
