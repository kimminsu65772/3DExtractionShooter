using UnityEngine;

public static class ArmorDamageCalculator
{
    // 관통 레벨이 방어구 레벨 이상이면 피해를 줄이지 않는다.
    // 방어구 레벨이 더 높으면 레벨 차이가 커질수록 받는 피해를 점차 감소시킨다.
    public static float GetDamageMultiplier(int armorPenetrationLevel, int armorLevel)
    {
        int safePenetrationLevel = Mathf.Max(armorPenetrationLevel, 0);
        int safeArmorLevel = Mathf.Max(armorLevel, 0);
        int levelDifference = safeArmorLevel - safePenetrationLevel;

        return levelDifference <= 0 ? 1f : 2f / (levelDifference + 2f);
    }

    public static float CalculateDamage(float incomingDamage, int armorPenetrationLevel, int armorLevel)
    {
        if (incomingDamage <= 0f || float.IsNaN(incomingDamage) || float.IsInfinity(incomingDamage))
            return 0f;

        return incomingDamage * GetDamageMultiplier(armorPenetrationLevel, armorLevel);
    }
}
