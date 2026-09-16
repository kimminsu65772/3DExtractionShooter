using UnityEngine;
using System;

[Serializable]
public class WeaponDefinition
{
    [Header("무기 기본 정보")]
    public int weaponId;
    public string displayName;
    public WeaponType weaponType;

    [Header("연사 속도")]
    public float fireRate;

    [Header("데미지 부분")]
    public float baseDamage;
    public float headMultiplier = 2f;
    public float bodyMultiplier = 1f;

    [Header("사거리")]
    public float optimalRange;
    public float effectiveRange;    
    public float minDamageRange;

    public float optimalRangeMultiplier = 1f;
    public float effectiveRangeMultiplier = 0.55f;
    public float minDamageMultiplier = 0.25f;

    [Header("투사체")]
    public float projectileSpeed;
    public float projectileRadius;
    public string projectileKey;
    public int projectilePrewarmSize;

    [Header("피드백 리소스")]
    public string muzzleVfxKey;
    public string hitVfxKey;
    public string fireSoundKey;

    public float GetRangeMultiplier(float aimDistance)
    {
        if (aimDistance <= optimalRange)
            return optimalRangeMultiplier;

        if (aimDistance <= effectiveRange)
            return effectiveRangeMultiplier;

        if (aimDistance <= minDamageRange)
            return minDamageMultiplier;

        return 0f;
    }

    public float GetHitPartMultiplier(HitPart hitPart)
    {
        return hitPart switch
        {
            HitPart.Head => headMultiplier,
            HitPart.Body => bodyMultiplier,
            HitPart.None => bodyMultiplier,
            _ => bodyMultiplier
        };
    }

    public float CalculateIncomingDamage(float aimDistance, HitPart hitPart)
    {
        return baseDamage *
               GetRangeMultiplier(aimDistance) *
               GetHitPartMultiplier(hitPart);
    }

    public bool IsValid()
    {
        return weaponId != 0 &&
               baseDamage > 0f &&
               projectileSpeed > 0f &&
               projectileRadius > 0f &&
               minDamageRange > 0f &&
               !string.IsNullOrWhiteSpace(projectileKey);
    }
}
