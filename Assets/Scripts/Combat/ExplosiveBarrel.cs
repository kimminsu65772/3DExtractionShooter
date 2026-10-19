using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HealthComponent))]
public sealed class ExplosiveBarrel : MonoBehaviour
{
    [Header("폭발 피해")]
    [SerializeField, Min(0.1f)] private float explosionRadius = 3.6f;
    [SerializeField, Min(0f)] private float explosionDamage = 45f;
    [SerializeField, Min(0)] private int armorPenetrationLevel = 1;
    [SerializeField] private Vector3 explosionCenterOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField] private LayerMask unitTargetMask;
    [SerializeField] private LayerMask barrelTargetMask;
    [SerializeField] private bool allowChainReaction = true;

    [Header("외형과 효과")]
    [SerializeField, Min(0f)] private float dangerHealth = 15f;
    [SerializeField] private GameObject normalVisuals;
    [SerializeField] private GameObject dangerVisuals;
    [SerializeField] private GameObject brokenVisuals;
    [SerializeField] private GameObject dangerEffectPrefab;
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private Collider[] barrelColliders = System.Array.Empty<Collider>();

    private HealthComponent health;
    private bool isDanger;
    private bool hasExploded;

    public bool HasExploded => hasExploded;

    private void Awake()
    {
        health = GetComponent<HealthComponent>();
        if (barrelColliders == null || barrelColliders.Length == 0)
            barrelColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        health.OnHealthChanged += HandleHealthChanged;
        health.OnDeath += HandleDeath;
    }

    private void Start()
    {
        // HealthComponent의 Awake 실행 순서와 관계없이 초기 체력으로 외형을 정한다.
        HandleHealthChanged(health.CurrentHealth, health.MaxHealth);
    }

    private void OnDisable()
    {
        if (health == null)
            return;

        health.OnHealthChanged -= HandleHealthChanged;
        health.OnDeath -= HandleDeath;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        // 사망 시 외형과 피해는 OnDeath에서 한 번만 처리한다.
        if (hasExploded || currentHealth <= 0f)
            return;

        bool nextDanger = currentHealth <= dangerHealth;
        bool enteredDanger = nextDanger && !isDanger;
        isDanger = nextDanger;

        SetVisuals(false);
        if (enteredDanger)
            SpawnEffect(dangerEffectPrefab);
    }

    private void HandleDeath(DamageInfo damageInfo)
    {
        if (hasExploded)
            return;

        // 다른 통의 사망 이벤트가 중첩 호출돼도 같은 통이 다시 폭발하지 않게 먼저 확정한다.
        hasExploded = true;
        foreach (Collider barrelCollider in barrelColliders)
        {
            if (barrelCollider != null)
                barrelCollider.enabled = false;
        }

        SetVisuals(true);
        SpawnEffect(explosionEffectPrefab);
        ApplyExplosionDamage();
    }

    private void ApplyExplosionDamage()
    {
        Vector3 origin = transform.TransformPoint(explosionCenterOffset);
        int targetMask = unitTargetMask.value;
        if (allowChainReaction)
            targetMask |= barrelTargetMask.value;

        // 일회성 폭발에서만 배열을 만든다. 고정 버퍼 초과로 피해 대상이 누락되지 않게 한다.
        Collider[] overlaps = Physics.OverlapSphere(
            origin, explosionRadius, targetMask, QueryTriggerInteraction.Collide);

        // 머리·몸통·이동 충돌체가 겹쳐도 체력 컴포넌트당 한 번만 피해를 준다.
        // 실제 피해 전에 수집을 끝내 연쇄 폭발이 현재 대상 순회를 바꾸지 않게 한다.
        var targets = new Dictionary<HealthComponent, Vector3>();
        foreach (Collider candidate in overlaps)
        {
            HealthComponent target = candidate.GetComponentInParent<HealthComponent>();
            if (target == null || target == health || !target.isActiveAndEnabled || target.IsDead)
                continue;

            if (targets.ContainsKey(target))
                continue;

            ExplosiveBarrel targetBarrel = target.GetComponent<ExplosiveBarrel>();
            if (targetBarrel != null)
            {
                if (!allowChainReaction || !targetBarrel.isActiveAndEnabled || targetBarrel.HasExploded)
                    continue;
            }
            else if ((unitTargetMask.value & (1 << candidate.gameObject.layer)) == 0)
            {
                // 통 탐색용 레이어에 있는 엄폐물 등은 연쇄 폭발 대상으로 취급하지 않는다.
                continue;
            }

            // 벽 차폐 없이 반경 안의 유닛과 폭발통에 피해를 준다.
            targets.Add(target, candidate.ClosestPoint(origin));
        }

        foreach (KeyValuePair<HealthComponent, Vector3> target in targets)
        {
            // 앞선 연쇄 폭발에서 이미 죽거나 제거된 대상은 다시 처리하지 않는다.
            if (target.Key == null || !target.Key.isActiveAndEnabled || target.Key.IsDead)
                continue;

            Vector3 offset = target.Value - origin;
            target.Key.TakeDamage(new DamageInfo
            {
                incomingDamage = explosionDamage,
                armorPenetrationLevel = armorPenetrationLevel,
                ammoDamageMultiplier = 1f,
                hitPoint = target.Value,
                hitDirection = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.up,
                hitPart = HitPart.Body,
                // 피격 반응·회피가 총을 쏜 사람 대신 실제 폭발 지점을 기준으로 작동한다.
                attacker = gameObject,
                aimDistance = offset.magnitude,
                baseDamage = explosionDamage,
                rangeMultiplier = 1f,
                hitPartMultiplier = 1f
            });
        }
    }

    private void SetVisuals(bool broken)
    {
        if (normalVisuals != null)
            normalVisuals.SetActive(!broken && !isDanger);
        if (dangerVisuals != null)
            dangerVisuals.SetActive(!broken && isDanger);
        if (brokenVisuals != null)
            brokenVisuals.SetActive(broken);
    }

    private void SpawnEffect(GameObject prefab)
    {
        if (prefab != null)
            Instantiate(prefab, transform.TransformPoint(explosionCenterOffset), Quaternion.identity);
    }
}
