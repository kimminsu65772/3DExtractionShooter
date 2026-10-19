using DamageNumbersPro;
using UnityEngine;

// ACT-07 / CORE-03: 피해 결과를 Canvas 좌표로 옮기고 DNP 내장 풀에 표시를 요청한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class DamageTextDisplay : MonoBehaviour
{
    [SerializeField] private DamageNumberGUI damageNumberPrefab;
    [SerializeField] private RectTransform damageTextRoot;
    [Tooltip("실제 월드를 렌더링하는 카메라. 비어 있으면 MainCamera를 사용한다.")]
    [SerializeField] private Camera worldCamera;
    [SerializeField, Min(0f)] private float worldHeightOffset = 0.3f;

    public static DamageTextDisplay Instance { get; private set; }

    private Canvas targetCanvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        Instance = null;
    }

    private void Awake()
    {
        targetCanvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        // 런타임에 생성되는 적도 개별 프리팹의 씬 참조 없이 같은 표시 진입점을 사용한다.
        Instance = this;
    }

    private void Start()
    {
        if (damageNumberPrefab == null || damageTextRoot == null)
        {
            Debug.LogError("DamageTextDisplay에 UI용 피해 숫자 프리팹과 표시 Root를 연결해야 합니다.", this);
            return;
        }

        // 대여와 반환은 에셋이 소유한다. 프리웜도 에셋의 공개 메서드로 한 번 요청한다.
        damageNumberPrefab.PrewarmPool();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowDamage(float appliedDamage, Vector3 hitPoint)
    {
        if (appliedDamage <= 0f || damageNumberPrefab == null || damageTextRoot == null)
            return;

        // 카메라가 나중에 생성되거나 씬 전환으로 교체되는 경우에도 현재 카메라를 사용한다.
        if (worldCamera == null || !worldCamera.isActiveAndEnabled)
            worldCamera = Camera.main;

        if (worldCamera == null)
            return;

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(hitPoint + Vector3.up * worldHeightOffset);
        if (screenPoint.z <= 0f)
            return;

        // Overlay의 좌표 변환에는 월드 카메라가 아니라 null을 전달한다.
        Camera canvasCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                damageTextRoot, screenPoint, canvasCamera, out Vector2 localPoint))
            return;

        // Decimals의 앵커는 중앙이다. 부모의 Pivot이 중앙이 아니어도 같은 피격점을 가리킨다.
        Vector2 anchoredPosition = localPoint - damageTextRoot.rect.center;
        damageNumberPrefab.SpawnGUI(damageTextRoot, anchoredPosition, appliedDamage);
    }
}
