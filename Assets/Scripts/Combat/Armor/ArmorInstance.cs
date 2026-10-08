using System;

// 개별 보호 장비의 내구도를 보관한다. 공통 정의는 변경하지 않는다.
public class ArmorInstance
{
    public ProtectionDefinition Definition { get; }
    public float CurrentDurability { get; private set; }
    public float DurabilityRatio => CurrentDurability / Definition.maxDurability;
    public float ProtectionFactor => CurrentDurability <= 0f ? 0f : DurabilityRatio < 0.5f ? 0.5f : 1f;

    public ArmorInstance(ProtectionDefinition definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (definition.Type != ItemType.Armor && definition.Type != ItemType.Helmet) 
            throw new ArgumentException("방어구 또는 헬멧의 데이터 정의가 필요하다.", nameof(definition));
        if (!IsPositiveFinite(definition.maxDurability) || !IsPositiveFinite(definition.durabilityDamageMultiplier) || definition.maxStackCount != 1) 
            throw new ArgumentException("내구도와 손상 배율은 양수이고 최대 스택은 1이어야 한다.", nameof(definition));
        Definition = definition;
        CurrentDurability = definition.maxDurability;
    }

    // 실제로 깎인 양을 반환한다. 소진된 장비도 객체 자체는 유지한다.
    public float DamageDurability(float amount)
    {
        if (!IsPositiveFinite(amount)) return 0f;
        float removed = Math.Min(CurrentDurability, amount);
        CurrentDurability -= removed;
        return removed;
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}
