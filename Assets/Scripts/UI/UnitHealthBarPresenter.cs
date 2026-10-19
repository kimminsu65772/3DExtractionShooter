using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UnitHealthBarPresenter : MonoBehaviour
{
    [SerializeField] private UnitHealthBarView view;
    [SerializeField] private RectTransform healthBarRoot;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Vector3 offset = new(0, 2, 0);

    private readonly Dictionary<HealthComponent, UnitHealthBarView> spawnedEnemy = new();
    private readonly Dictionary<HealthComponent, VisibilityTarget> visibilityTargets = new();

    // 분대/단독 생성 단위 모두 같은 경로로, 구성원마다 자신의 SO를 전달한다.
    public void Register(EnemySpawnComposition composition)
    {
        if (composition == null) return;
        if (!composition.TryValidate(out string error))
        {
            Debug.LogError($"체력바 구성 연결 오류: {error}", composition);
            return;
        }

        foreach (var member in composition.Members)
            Register(member.Health, member.Definition);
    }

    public void Register(HealthComponent health, EnemyDefinition enemyDefinition)
    {
        if (health == null) return;

        if (spawnedEnemy.ContainsKey(health)) return;

        if (enemyDefinition == null || view == null || healthBarRoot == null)
        {
            Debug.LogError("체력바 등록 전에 EnemyDefinition, View 프리팹, 부모를 연결해야 한다.", this);
            return;
        }

        UnitHealthBarView healthBar = Instantiate(view, healthBarRoot);
        healthBar.InitializeHealth(health.CurrentHealth, health.MaxHealth, enemyDefinition);
        VisibilityTarget visibility = health.GetComponentInParent<VisibilityTarget>(true);
        visibilityTargets.Add(health, visibility);
        // 최초 탐지 전에도 체력바가 먼저 보이지 않게 한다. FOV 대상이 없는 더미는 표시한다.
        healthBar.gameObject.SetActive(visibility == null || visibility.IsVisible);
        health.OnHealthChanged += healthBar.SetHealthTarget;
        health.OnDestroyed += Unregister;
        spawnedEnemy.Add(health, healthBar);
    }
    public void Unregister(HealthComponent health)
    {
        // Unity가 파괴된 객체를 null로 판정해도 관리 객체와 딕셔너리 키는 남을 수 있다.
        if (ReferenceEquals(health, null) || !spawnedEnemy.TryGetValue(health, out var healthBar)) return;

        // 재호출해도 한 번만 정리되도록 먼저 등록을 제거한다.
        spawnedEnemy.Remove(health);
        visibilityTargets.Remove(health);
        health.OnHealthChanged -= healthBar.SetHealthTarget;
        health.OnDestroyed -= Unregister;

        if (healthBar != null)
            Destroy(healthBar.gameObject);
    }

    private void LateUpdate()
    {
        // 혹시 캔버스의 모드 설정이 ScreenSpaceOverlay가 아니라면, mainCamera를 사용하여 화면 좌표로 변환한다.
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        foreach (var pair in spawnedEnemy)
        {
            HealthComponent health = pair.Key;
            UnitHealthBarView healthBar = pair.Value;
            if (health == null || healthBar == null) continue;
            if (visibilityTargets.TryGetValue(health, out VisibilityTarget visibility) && visibility != null && !visibility.IsVisible)
            {
                if (healthBar.gameObject.activeSelf) healthBar.gameObject.SetActive(false);
                continue;
            }
            Vector3 worldPosition = health.transform.position + offset;
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

            bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(healthBarRoot, new Vector2(screenPosition.x, screenPosition.y), uiCamera, out Vector2 localPosition);
            RectTransform healthBarRect = healthBar.GetComponent<RectTransform>();
            healthBarRect.anchoredPosition = new Vector2(localPosition.x, localPosition.y);
            if (!healthBar.gameObject.activeSelf) healthBar.gameObject.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        // Unregister가 딕셔너리를 변경하므로 종료 때만 키 목록을 복사한다.
        foreach (var health in new List<HealthComponent>(spawnedEnemy.Keys))
            Unregister(health);
    }
}
