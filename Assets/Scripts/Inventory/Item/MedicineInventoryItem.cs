using System;

// 키트 한 개의 잔여 사용량을 보관한다. 다른 키트와 Definition만 공유한다.
public class MedicineInventoryItem : DefinitionInventoryItem
{
    public float RemainingUseRatio { get; private set; } = 1f;
    public bool IsDepleted => RemainingUseRatio <= 0f;

    public MedicineInventoryItem(MedicineDefinition definition) : base(definition)
    {
        if (!definition.useCostRatio.HasValue || float.IsNaN(definition.useCostRatio.Value) || float.IsInfinity(definition.useCostRatio.Value) || definition.useCostRatio.Value <= 0f || definition.useCostRatio.Value > 1f || definition.maxStackCount != 1)
            throw new ArgumentException("키트는 유효한 소비 비율을 지정하고 한 칸에 하나씩 보관한다.", nameof(definition));
    }

    // 부족한 체력과 잔여 사용량을 함께 제한하고 실제 회복량에 비례해 소비한다.
    public float ConsumeForHealing(float missingHealth)
    {
        if (Count <= 0 || IsDepleted || float.IsNaN(missingHealth) || float.IsInfinity(missingHealth) || missingHealth <= 0f) return 0f;
        var medicine = (MedicineDefinition)Definition;
        float cost = medicine.useCostRatio.Value;
        float availableHeal = medicine.healAmount * Math.Min(1f, RemainingUseRatio / cost);
        float actualHeal = Math.Min(missingHealth, availableHeal);
        if (actualHeal <= 0f) return 0f;
        float consumed = cost * (actualHeal / medicine.healAmount);
        // 이번 사용으로 잔여량 전체를 회복에 사용했다면 계산 오차 없이 정확히 0으로 만든다.
        RemainingUseRatio = missingHealth >= availableHeal && RemainingUseRatio <= cost ? 0f : Math.Max(0f, RemainingUseRatio - consumed);
        return actualHeal;
    }

    public bool TryRestoreRemainingUseRatio(float ratio)
    {
        if (float.IsNaN(ratio) || float.IsInfinity(ratio) || ratio <= 0f || ratio > 1) 
            return false;

        RemainingUseRatio = ratio;

        return true;
    }
}
