using UnityEngine;

public static class ArmorDamageCalculator
{
    // 방어와 관통의 차이로 피해 비율을 구한다. 내구도는 방어 수치가 아닌 보호 효과에 적용한다.
    public static float GetDamageMultiplier(int armorPenetrationLevel, float armorLevel, float protectionFactor = 1f)
    {
        int safePenetrationLevel = Mathf.Max(armorPenetrationLevel, 0);
        float safeArmorLevel = Mathf.Max(armorLevel, 0f);
        float levelDifference = Mathf.Max(safeArmorLevel - safePenetrationLevel, 0f);

        // 관통이 방어 이상이면 차이는 0이므로 원래 피해가 그대로 적용된다.
        float normalMultiplier = 2f / (levelDifference + 2f);
        float safeFactor = float.IsNaN(protectionFactor) || float.IsInfinity(protectionFactor) ? 0f : Mathf.Clamp01(protectionFactor);
        return 1f - (1f - normalMultiplier) * safeFactor;
    }

    public static float CalculateDamage(float incomingDamage, int armorPenetrationLevel, float armorLevel, float protectionFactor = 1f)
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
