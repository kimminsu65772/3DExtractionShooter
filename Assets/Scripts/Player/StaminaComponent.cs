using UnityEngine;
using System;

[DisallowMultipleComponent]
public class StaminaComponent : MonoBehaviour
{
    [Header("참조할 스탯 데이터")]
    [SerializeField] private CharacterStats characterStats;

    [Header("스태미너 회복")]
    [SerializeField, Min(0.1f)] private float staminaRegenDelay = 1.5f; // 스태미너 회복 시작 전 지연 시간
    [SerializeField, Range(0.1f, 1f)] private float sprintResumeStaminaRatio = 0.2f;

    private float maxStamina;
    private float currentStamina;
    private float staminaRegenRate;
    private float staminaCostMultiplier;
    private float lastStaminaUseTime;
    private bool isSprintLockedByStamina = false;

    public float MaxStamina => maxStamina;
    public float CurrentStamina => currentStamina;
    public bool IsStaminaFull => currentStamina >= maxStamina;
    public bool CanSprint => !isSprintLockedByStamina;

    public event Action<float, float> OnStaminaChanged;

    private void Awake()
    {
        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }
        if (characterStats == null)
        {
            Debug.LogError("CharacterStats 컴포넌트를 찾을 수 없습니다. StaminaComponent가 올바르게 작동하려면 CharacterStats가 필요합니다.");
            enabled = false;
            return;
        }

        // 최대 스태미너는 한 번 고정되면 변경되지 않으므로 Awake에서 한 번만 가져온다.
        maxStamina = characterStats.GetValue(StatType.MaxStamina, 100f);
        currentStamina = maxStamina;
        UpdateStaminaStats();

    }

    private void OnEnable()
    {
        if (characterStats != null)
        {
            characterStats.OnStatChanged -= HandleStatChanged;
            characterStats.OnStatChanged += HandleStatChanged;
        }
    }

    private void OnDisable()
    {
        if (characterStats != null)
        {
            characterStats.OnStatChanged -= HandleStatChanged;
        }
    }

    private void Start()
    {
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    private void HandleStatChanged(StatType stat, float previousStat, float newStat)
    {
        if (stat == StatType.StaminaRecoveryRate || stat == StatType.StaminaCostMultiplier)
        {
            UpdateStaminaStats();
            currentStamina = Mathf.Min(currentStamina, maxStamina);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }
    private void Update()
    {
        RegenStamina();
    }

    private void RegenStamina()
    {
        if (Time.time - lastStaminaUseTime >= staminaRegenDelay)
        {
            // 이미 최대 스태미너에 도달했거나 회복률이 0 이하이면 회복을 시도하지 않는다.
            if (currentStamina >= maxStamina || staminaRegenRate <= 0f)
                return;

            // Mathf.Min을 사용하여 다음 스태미너 값을 미리 계산한다.
            float nextStmina = Mathf.Min(currentStamina + staminaRegenRate * Time.deltaTime, maxStamina);

            // 미리 계산한 다음 스태미너 값이 현재 스태미너보다 작거나 같으면 회복을 하지 않도록 하여 불필요한 이벤트 호출을 방지한다.
            if (nextStmina <= currentStamina)
                return;

            // 정상적으로 회복이 이루어졌다면 현재 스태미너를 업데이트하고 이벤트를 호출한다.
            currentStamina = nextStmina;
            if (currentStamina >= maxStamina * sprintResumeStaminaRatio)
            {
                isSprintLockedByStamina = false;
            }
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }

    // 스태미너 사용을 시도한다. 이때, 최종 스태미너 사용량은 staminaCostMultiplier에 의해 조정된다.
    public bool TryUseStamina(float amount)
    {
        if (amount <= 0f)
        {
            Debug.LogWarning("스태미너 사용량은 0보다 커야 합니다.");
            return false;
        }

        float adjustedAmount = amount * staminaCostMultiplier;
        if (currentStamina >= adjustedAmount)
        {
            currentStamina = Mathf.Max(currentStamina - adjustedAmount, 0f);
            lastStaminaUseTime = Time.time;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);

            if (currentStamina <= 0f)
                LockSprintUntilRegen();

            return true;
        }
        return false;
    }

    public bool TryConsumeSprintStamina(float amount)
    {
        if (amount <= 0f)
        {
            Debug.LogWarning("스태미너 사용량은 0보다 커야 합니다.");
            return false;
        }

        if (isSprintLockedByStamina)
        {
            return false;
        }

        float adjustStamina = amount * staminaCostMultiplier;
        bool paidFully = currentStamina >= adjustStamina;
        if (paidFully)
        {
            currentStamina = Mathf.Max(currentStamina - adjustStamina, 0f);
            lastStaminaUseTime = Time.time;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            if (currentStamina <= 0f)
                LockSprintUntilRegen();

            return true;
        }
        else
        {
            currentStamina = 0f;
            lastStaminaUseTime = Time.time;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            LockSprintUntilRegen();
            return false;
        }
    }

    private void LockSprintUntilRegen()
    {
        isSprintLockedByStamina = true;
    }

    // 스태미너 밸런스는 스태미너 회복률과 스태미너 사용량에 영향을 주는 두 가지 주요 스탯을 기반으로 한다.
    private void UpdateStaminaStats()
    {
        staminaRegenRate = characterStats.GetValue(StatType.StaminaRecoveryRate, 5f);
        staminaCostMultiplier = characterStats.GetValue(StatType.StaminaCostMultiplier, 1f);
    }

}
