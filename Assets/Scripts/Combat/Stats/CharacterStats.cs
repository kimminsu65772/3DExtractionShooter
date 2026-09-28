using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterStats : MonoBehaviour
{
    // 인스펙터에 저장된 기본값과 게임 도중 붙고 떨어지는 장비·버프 값을 분리하기 위한 런타임 상태다.
    // 최종값을 CachedValue에 보관하여 스탯을 조회할 때마다 모든 Modifier를 다시 계산하지 않는다.
    private class RuntimeStat
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

    // 캐릭터가 처음 생성될 때 갖는 원본 스탯 목록이다. 레벨업 전의 초기값을 인스펙터에서 설정한다.
    [SerializeField] private CharacterStatDefinition[] baseStats = Array.Empty<CharacterStatDefinition>();

    // StatType을 키로 사용해 필요한 스탯을 배열 순회 없이 바로 찾기 위한 런타임 조회표다.
    private readonly Dictionary<StatType, RuntimeStat> runtimeStats = new Dictionary<StatType, RuntimeStat>();
    private bool isInitialized;

    // UI, HealthComponent처럼 스탯 변화를 알아야 하는 객체가 CharacterStats를 매 프레임 확인하지 않도록 알린다.
    public event Action<StatType, float, float> OnStatChanged;

    private void Awake()
    {
        EnsureInitialized();
    }

    // 요청한 스탯이 실제로 등록되어 있는지까지 호출자가 구분할 수 있게 bool과 값을 함께 제공한다.
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

    // 스탯 누락 여부를 따로 처리할 필요가 없는 곳에서 정해 둔 대체값으로 바로 읽기 위한 편의 메서드다.
    public float GetValue(StatType statType, float fallbackValue = 0f)
    {
        return TryGetValue(statType, out float value) ? value : fallbackValue;
    }

    // 레벨업이나 영구 성장처럼 스탯의 기반 자체가 바뀔 때 사용한다.
    // 장비와 버프 Modifier는 유지한 채 새 기본값으로 최종값을 다시 계산한다.
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

    // 현재 기본값을 직접 조회하고 다시 설정하는 중복 코드를 호출자마다 만들지 않도록 증감 연산을 제공한다.
    public bool AddToBaseValue(StatType statType, float amount)
    {
        EnsureInitialized();

        if (!runtimeStats.TryGetValue(statType, out RuntimeStat stat))
            return false;

        return SetBaseValue(statType, stat.BaseValue + amount);
    }

    // 장비 장착이나 버프 적용처럼 일시적인 변경을 기본값과 섞지 않고 별도 Modifier로 누적한다.
    // Modifier가 바뀔 때만 최종값을 다시 계산하므로 평상시 스탯 조회 비용을 일정하게 유지한다.
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

    // 장비 해제 또는 버프 종료 시, 같은 출처가 여러 스탯에 추가한 Modifier를 한 번에 제거한다.
    // sourceId까지 비교하므로 같은 종류의 장비나 버프가 여러 개 있어도 지정한 하나만 제거할 수 있다.
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

    // 인스펙터의 직렬화 데이터는 조회와 변경에 불편하므로, 최초 사용 시 Dictionary 기반 런타임 상태로 변환한다.
    // Awake 이전에 다른 컴포넌트가 값을 요청해도 초기화되며, 이후 호출에서는 다시 만들지 않는다.
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

    // 기본값, 고정 수치, 합산 배율, 곱연산 배율을 정해진 순서로 합쳐 최종 스탯을 만든다.
    // 계산 순서를 한곳에 고정하여 Modifier가 추가된 순서에 따라 결과가 달라지는 문제를 막는다.
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

    // 계산 결과가 실제로 달라졌을 때만 이벤트를 보내 UI 갱신이나 체력 보정이 불필요하게 반복되지 않게 한다.
    private void NotifyIfChanged(StatType statType, float previousValue, float currentValue)
    {
        if (Mathf.Approximately(previousValue, currentValue))
            return;

        OnStatChanged?.Invoke(statType, previousValue, currentValue);
    }

    // 런타임 스탯 초기화가 아니라, 컴포넌트를 처음 추가하거나 Reset했을 때 인스펙터에 기본 목록을 채우는 편집기용 메서드다.
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
