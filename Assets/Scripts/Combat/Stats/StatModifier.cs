using System;

public readonly struct StatModifier
{
    public StatType StatType { get; }
    public StatModifierOperation Operation { get; }
    public StatModifierSource Source { get; }
    public int SourceId { get; }
    public float Value { get; }

    public StatModifier(StatType statType, StatModifierOperation operation, float value, StatModifierSource source, int sourceId)
    {
        if (sourceId == 0)
            throw new ArgumentOutOfRangeException(nameof(sourceId), "스탯 변경 출처 ID로 0을 사용할 수 없습니다.");

        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), "스탯 변경값은 유한한 숫자여야 합니다.");

        StatType = statType;
        Operation = operation;
        Value = value;
        Source = source;
        SourceId = sourceId;
    }
}
