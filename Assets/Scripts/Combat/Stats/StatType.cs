public enum StatType
{
    MaxHealth,
    WalkSpeed,
    SprintSpeed,
    AimMoveSpeedMultiplier,
    OutgoingDamageMultiplier,
    HeadshotDamageMultiplier,
    HeadArmorLevel,
    BodyArmorLevel,
    MaxStamina,
    StaminaRecoveryRate,
    StaminaCostMultiplier,
    RecoilControlMultiplier,
    SpreadControlMultiplier
}

public enum StatModifierOperation
{
    Flat,
    AdditivePercent,
    Multiply
}

public enum StatModifierSource
{
    Equipment,
    Buff
}
