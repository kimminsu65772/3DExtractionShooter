using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterStats : MonoBehaviour
{
    private sealed class RuntimeStat
    {
        public float BaseValue;
        public float CachedValue;
        public readonly List<StatModifier> Modifiers = new List<StatModifier>();

        public RuntimeStat(float baseValue)
        {
            BaseValue = baseValue;
            CachedValue = baseValue;
        }
    }

    [SerializeField] private CharacterStatDefinition[] baseStats = Array.Empty<CharacterStatDefinition>();

    private readonly Dictionary<StatType, RuntimeStat> runtimeStats = new Dictionary<StatType, RuntimeStat>();
    private bool isInitialized;

    public event Action<StatType, float, float> OnStatChanged;

    private void Awake()
    {
        EnsureInitialized();
    }

    public bool TryGetValue(StatType statType, out float value)
    {
        EnsureInitialized();

        if (runtimeStats.TryGetValue(statType, out RuntimeStat stat))
        {
            value = stat.CachedValue;
            return true;
        }

        value = 0f;
        return false;
    }

    public float GetValue(StatType statType, float fallbackValue = 0f)
    {
        return TryGetValue(statType, out float value) ? value : fallbackValue;
    }

    public bool SetBaseValue(StatType statType, float baseValue)
    {
        EnsureInitialized();

        if (!runtimeStats.TryGetValue(statType, out RuntimeStat stat))
            return false;

        float previousValue = stat.CachedValue;
        stat.BaseValue = baseValue;
        stat.CachedValue = CalculateValue(stat);
        NotifyIfChanged(statType, previousValue, stat.CachedValue);
        return true;
    }

    public bool AddToBaseValue(StatType statType, float amount)
    {
        EnsureInitialized();

        if (!runtimeStats.TryGetValue(statType, out RuntimeStat stat))
            return false;

        return SetBaseValue(statType, stat.BaseValue + amount);
    }

    public bool AddModifier(StatModifier modifier)
    {
        EnsureInitialized();

        if (!runtimeStats.TryGetValue(modifier.StatType, out RuntimeStat stat))
            return false;

        float previousValue = stat.CachedValue;
        stat.Modifiers.Add(modifier);
        stat.CachedValue = CalculateValue(stat);
        NotifyIfChanged(modifier.StatType, previousValue, stat.CachedValue);
        return true;
    }

    public int RemoveModifiers(StatModifierSource source, int sourceId)
    {
        EnsureInitialized();

        int removedCount = 0;

        foreach (KeyValuePair<StatType, RuntimeStat> pair in runtimeStats)
        {
            RuntimeStat stat = pair.Value;
            float previousValue = stat.CachedValue;
            int removedFromStat = stat.Modifiers.RemoveAll(modifier => modifier.Source == source && modifier.SourceId == sourceId);

            if (removedFromStat == 0)
                continue;

            removedCount += removedFromStat;
            stat.CachedValue = CalculateValue(stat);
            NotifyIfChanged(pair.Key, previousValue, stat.CachedValue);
        }

        return removedCount;
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
            return;

        runtimeStats.Clear();

        foreach (CharacterStatDefinition definition in baseStats)
        {
            if (runtimeStats.ContainsKey(definition.statType))
            {
                Debug.LogError($"{definition.statType} 기본 스탯이 중복 등록되었습니다.", this);
                continue;
            }

            runtimeStats.Add(definition.statType, new RuntimeStat(definition.baseValue));
        }

        isInitialized = true;
    }

    private static float CalculateValue(RuntimeStat stat)
    {
        float flatTotal = 0f;
        float additivePercentTotal = 0f;
        float multiplyTotal = 1f;

        foreach (StatModifier modifier in stat.Modifiers)
        {
            switch (modifier.Operation)
            {
                case StatModifierOperation.Flat:
                    flatTotal += modifier.Value;
                    break;
                case StatModifierOperation.AdditivePercent:
                    additivePercentTotal += modifier.Value;
                    break;
                case StatModifierOperation.Multiply:
                    multiplyTotal *= modifier.Value;
                    break;
            }
        }

        return (stat.BaseValue + flatTotal) * (1f + additivePercentTotal) * multiplyTotal;
    }

    private void NotifyIfChanged(StatType statType, float previousValue, float currentValue)
    {
        if (Mathf.Approximately(previousValue, currentValue))
            return;

        OnStatChanged?.Invoke(statType, previousValue, currentValue);
    }

    private void Reset()
    {
        baseStats = new[]
        {
            new CharacterStatDefinition(StatType.MaxHealth, 100f),
            new CharacterStatDefinition(StatType.WalkSpeed, 5f),
            new CharacterStatDefinition(StatType.SprintSpeed, 8f),
            new CharacterStatDefinition(StatType.AimMoveSpeedMultiplier, 0.6f),
            new CharacterStatDefinition(StatType.OutgoingDamageMultiplier, 1f),
            new CharacterStatDefinition(StatType.HeadshotDamageMultiplier, 1f),
            new CharacterStatDefinition(StatType.HeadArmorLevel, 0f),
            new CharacterStatDefinition(StatType.BodyArmorLevel, 0f),
            new CharacterStatDefinition(StatType.MaxStamina, 100f),
            new CharacterStatDefinition(StatType.StaminaRecoveryRate, 15f),
            new CharacterStatDefinition(StatType.StaminaCostMultiplier, 1f),
            new CharacterStatDefinition(StatType.RecoilControlMultiplier, 1f),
            new CharacterStatDefinition(StatType.SpreadControlMultiplier, 1f)
        };
    }
}
