using UnityEngine;

public static class ArmorDamageCalculator
{
    // 내구도 효과는 막아 주던 피해량에 곱한다. 등급 자체를 변경하지 않는다.
    public static float GetDamageMultiplier(int armorPenetrationLevel, int armorLevel, float protectionFactor = 1f)
    {
        int safePenetrationLevel = Mathf.Max(armorPenetrationLevel, 0);
        int safeArmorLevel = Mathf.Max(armorLevel, 0);
        int levelDifference = safeArmorLevel - safePenetrationLevel;

        if (safeArmorLevel == 0) return 1f;
        float normalMultiplier = levelDifference > 0 ? 2f / (levelDifference + 2f) : levelDifference == 0 ? 0.9f : levelDifference == -1 ? 0.95f : 0.975f;
        float safeFactor = float.IsNaN(protectionFactor) || float.IsInfinity(protectionFactor) ? 0f : Mathf.Clamp01(protectionFactor);
        return 1f - (1f - normalMultiplier) * safeFactor;
    }

    public static float CalculateDamage(float incomingDamage, int armorPenetrationLevel, int armorLevel, float protectionFactor = 1f)
    {
        if (incomingDamage <= 0f || float.IsNaN(incomingDamage) || float.IsInfinity(incomingDamage))
            return 0f;

        return incomingDamage * GetDamageMultiplier(armorPenetrationLevel, armorLevel, protectionFactor);
    }

    // 체력에 적용한 피해가 아닌 방어 전 피해를 기준으로 장비를 손상시킨다.
    public static float CalculateDurabilityDamage(float incomingDamage, int penetrationLevel, float wearCoefficient, float penetrationWearPerLevel, float itemMultiplier)
    {
        if (!IsNonNegativeFinite(incomingDamage) || !IsNonNegativeFinite(wearCoefficient) || !IsNonNegativeFinite(penetrationWearPerLevel) || !IsNonNegativeFinite(itemMultiplier)) return 0f;
        float amount = incomingDamage * wearCoefficient * (1f + Mathf.Max(0, penetrationLevel) * penetrationWearPerLevel) * itemMultiplier;
        return float.IsInfinity(amount) || float.IsNaN(amount) ? 0f : amount;
    }

    private static bool IsNonNegativeFinite(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}
