using UnityEngine;
using System;

public class HealthComponent : MonoBehaviour, IDamageable
{
    [SerializeField] private float defaultMaxHealth = 100f;
    [SerializeField] private CharacterStats characterStats;
    [Header("보호 장비 손상 설정")]
    [SerializeField, Min(0f)] private float durabilityWearCoefficient = 0.1f;
    [SerializeField, Min(0f)] private float penetrationWearPerLevel = 0.25f;

    private IProtectionProvider protectionProvider;

    private float maxHealth;
    private float currentHealth;
    private bool isInvulnerable;

    private bool isEnemy;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0f;  
    public bool IsAlive => !IsDead;
    public bool IsInvulnerable => isInvulnerable;

    // UI 체력 변경 전용 이벤트
    public event Action<float, float> OnHealthChanged;

    // 데미지를 받거나 사망했을 때 상태 변경을 가능하도록 하기 위한 이벤트들
    public event Action<DamageInfo> OnDamageTaken;
    public event Action<DamageInfo> OnDeath;

    // 사망 후 복구하는 더미와 실제 제거를 구분하는 생명주기 알림이다.
    public event Action<HealthComponent> OnDestroyed;

    private void Awake()
    {
        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }

        // 플레이어의 장비 구조 대신 보호 장비 제공 계약만 참조한다.
        protectionProvider = GetComponent<IProtectionProvider>();
        Initialize(GetConfiguredMaxHealth());

        if (gameObject.layer == LayerMask.NameToLayer("Enemy"))
            isEnemy = true;
    }

    private void OnEnable()
    {
        if (characterStats != null)
        {
            characterStats.OnStatChanged += HandleStatChanged;
        }
    }

    private void OnDisable()
    {
        if (characterStats != null)
        {
            characterStats.OnStatChanged -= HandleStatChanged;
        }

        isInvulnerable = false;
    }

    private void OnDestroy()
    {
        OnDestroyed?.Invoke(this);
        OnDestroyed = null;
    }

    public void Initialize(float newMaxHealth)
    {
        maxHealth = Mathf.Max(newMaxHealth, 1f);
        currentHealth = maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (IsDead) return;

        if (IsInvulnerable) return;
        
        if (damageInfo.incomingDamage <= 0f) return;

        ArmorInstance protection = protectionProvider?.GetProtection(damageInfo.hitPart);
        // 장비 제공자가 있는 캐릭터는 빈 장비 칸에서 스탯 방어 등급으로 되돌아가지 않는다.
        // 아직 장비 모델을 사용하지 않는 적은 기존 스탯 기반 보호를 사용한다.
        float armorLevel = protectionProvider != null ? protection?.Definition.armorLevel ?? 0f : GetArmorLevel(damageInfo.hitPart);
        float protectionFactor = protection != null ? protection.ProtectionFactor : 1f;
        float armorMultiplier = ArmorDamageCalculator.GetDamageMultiplier(damageInfo.armorPenetrationLevel, armorLevel, protectionFactor);
        float appliedDamage = ArmorDamageCalculator.CalculateDamage(damageInfo.incomingDamage, damageInfo.armorPenetrationLevel, armorLevel, protectionFactor);

        if (appliedDamage <= 0f)
            return;

        bool wasAlive = IsAlive;
        currentHealth = Mathf.Max(currentHealth - appliedDamage, 0f);

        // 이번 피해는 피격 전 내구도로 계산한다. 이후 손상은 다음 피격부터 반영한다.
        if (protection != null)
            protection.DamageDurability(ArmorDamageCalculator.CalculateDurabilityDamage(damageInfo.incomingDamage, damageInfo.armorPenetrationLevel, durabilityWearCoefficient, penetrationWearPerLevel, protection.Definition.durabilityDamageMultiplier));

        damageInfo.appliedDamage = appliedDamage;
        damageInfo.armorLevel = armorLevel;
        damageInfo.armorMultiplier = armorMultiplier;

        // 텍스트 디스플레이를 요청할 객체의 레이어를 확인한다. 만약에 적 레이어가 아니라면, UI에 표시하지 않는다.
        if (gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            DamageTextDisplay.Instance?.ShowDamage(damageInfo.appliedDamage, damageInfo.hitPoint);
        }

        if(isEnemy)
        {
            // 피해 계산이 끝난 결과를 표시 담당 공개 메서드로 직접 전달한다.
            DamageTextDisplay.Instance?.ShowDamage(damageInfo.appliedDamage, damageInfo.hitPoint);
        }

        OnDamageTaken?.Invoke(damageInfo);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (wasAlive && IsDead)
        {
            OnDeath?.Invoke(damageInfo);
        }
    }

    public void Restore(float amount)
    {
        if (IsDead)
            return;

        if (amount <= 0f)
            return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void SetInvulnerable(bool value)
    {
        isInvulnerable = value;
    }

    private float GetArmorLevel(HitPart hitPart)
    {
        if (characterStats == null)
            return 0;

        StatType armorStat = hitPart == HitPart.Head ? StatType.HeadArmorLevel : StatType.BodyArmorLevel;
        return Mathf.Max(0f, characterStats.GetValue(armorStat));
    }

    private float GetConfiguredMaxHealth()
    {
        return characterStats != null ? characterStats.GetValue(StatType.MaxHealth, defaultMaxHealth) : defaultMaxHealth;
    }

    private void HandleStatChanged(StatType statType, float previousValue, float currentValue)
    {
        if (statType != StatType.MaxHealth)
            return;

        float previousMaxHealth = maxHealth;
        float healthRatio = previousMaxHealth > 0f ? currentHealth / previousMaxHealth : 1f;
        maxHealth = Mathf.Max(currentValue, 1f);
        currentHealth = Mathf.Clamp(maxHealth * healthRatio, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
