using UnityEngine;
using System;

public class HealthComponent : MonoBehaviour, IDamageable
{
    [SerializeField] private float defaultMaxHealth = 100f;
    [SerializeField] private CharacterStats characterStats;

    private float maxHealth;
    private float currentHealth;
    private bool isInvulnerable;

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

    private void Awake()
    {
        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }

        Initialize(GetConfiguredMaxHealth());
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

        int armorLevel = GetArmorLevel(damageInfo.hitPart);
        float armorMultiplier = ArmorDamageCalculator.GetDamageMultiplier(damageInfo.armorPenetrationLevel, armorLevel);
        float appliedDamage = ArmorDamageCalculator.CalculateDamage(damageInfo.incomingDamage, damageInfo.armorPenetrationLevel, armorLevel);

        if (appliedDamage <= 0f)
            return;

        bool wasAlive = IsAlive;
        currentHealth = Mathf.Max(currentHealth - appliedDamage, 0f);

        damageInfo.appliedDamage = appliedDamage;
        damageInfo.armorLevel = armorLevel;
        damageInfo.armorMultiplier = armorMultiplier;

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

    private int GetArmorLevel(HitPart hitPart)
    {
        if (characterStats == null)
            return 0;

        StatType armorStat = hitPart == HitPart.Head ? StatType.HeadArmorLevel : StatType.BodyArmorLevel;
        return Mathf.Max(0, Mathf.RoundToInt(characterStats.GetValue(armorStat)));
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
