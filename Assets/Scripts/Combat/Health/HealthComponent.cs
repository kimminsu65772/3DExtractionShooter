using UnityEngine;
using System;

public class HealthComponent : MonoBehaviour, IDamageable
{
    [SerializeField] private float defaultMaxHealth = 100f;
    [SerializeField] private bool showDebugLog = true;

    private float maxHealth;
    private float currentHealth;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0f;  
    public bool IsAlive => !IsDead;

    // UI 체력 변경 전용 이벤트
    public event Action<float, float> OnHealthChanged;

    // 데미지를 받거나 사망했을 때 상태 변경을 가능하도록 하기 위한 이벤트들
    public event Action<DamageInfo> OnDamageTaken;
    public event Action<DamageInfo> OnDeath;

    private void Awake()
    {
        Initialize(defaultMaxHealth);
    }

    public void Initialize(float newMaxHealth)
    {
        maxHealth = newMaxHealth;
        currentHealth = maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (IsDead) return;
        
        if (damageInfo.incomingDamage <= 0f) return;

        bool wasAlive = IsAlive;

        currentHealth = Mathf.Max(currentHealth - damageInfo.incomingDamage, 0f);

        OnDamageTaken?.Invoke(damageInfo);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (showDebugLog)
        {
            Debug.Log(
            $"[{name}] Damage: {damageInfo.incomingDamage} / " +
            $"Part: {damageInfo.hitPart} / " +
            $"Weapon: {damageInfo.weaponId} / " +
            $"HP: {currentHealth}/{maxHealth}"
        );
        }

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
}
