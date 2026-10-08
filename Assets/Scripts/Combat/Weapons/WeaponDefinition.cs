using ExtractionShooter.DataPipeline;
using System;
using UnityEngine;

// 탄창 전체를 채우는 방식과 한 발씩 넣는 방식을 구분한다.
public enum WeaponReloadMode { Magazine, SingleRound }

[Serializable]
public class WeaponDefinition
{
    [Header("총기 기본 설정")]
    public int weaponId;
    public string displayName;
    public WeaponType weaponType;
    public WeaponAnimationStyle animationStyle;
    // 같은 이름의 총도 각각 독립된 물건이므로 최대 중첩 수량은 1로 유지한다.
    [CsvColumn(Optional = true)] public int maxStackCount = 1;
    [CsvColumn(Optional = true)] public float unitWeight;
    [CsvColumn(Optional = true)] public string iconAddress;

    [Header("탄약과 탄창")]
    public AmmoType ammoType;
    public int magazineCapacity;
    public float reloadDuration;
    // SingleRound에서는 reloadDuration이 한 발을 넣는 시간이다.
    [CsvColumn(Optional = true)] public WeaponReloadMode reloadMode;
    [CsvColumn(Optional = true)] public float adsDuration;
    [CsvColumn(Optional = true)] public float movementSpeedMultiplier = 1f;
    [CsvColumn(Optional = true)] public float adsMovementSpeedMultiplier = 1f;

    [Header("연사 속도")]
    public float fireRate;

    [Header("반동")]
    public float verticalRecoilPerShot;
    public float horizontalRecoilPerShot;
    public float maxRecoilOffset;
    public float recoilRecoverySpeed;
    public float aimRecoilMultiplier;

    [Header("탄퍼짐")]
    public float baseSpreadAngle;
    public float maxSpreadAngle;
    public float spreadIncreasePerShot;
    public float spreadRecoverySpeed;
    public float aimSpreadMultiplier;
    public float moveSpreadMultiplier;
    // 음수 기본값은 이전 CSV가 독립적인 ADS 설정을 제공하지 않았음을 뜻한다.
    [CsvColumn(Optional = true)] public float adsBaseSpreadAngle = -1f;
    [CsvColumn(Optional = true)] public float adsMaxSpreadAngle = -1f;
    [CsvColumn(Optional = true)] public float adsSpreadIncreasePerShot = -1f;
    [CsvColumn(Optional = true)] public float adsSpreadRecoverySpeed = -1f;

    [Header("기본 스펙")]
    public float baseDamage;
    public float headMultiplier = 2f;
    public float bodyMultiplier = 1f;

    [Header("유효 사거리")]
    public float optimalRange;
    public float effectiveRange;    
    public float minDamageRange;

    public float optimalRangeMultiplier = 1f;
    public float effectiveRangeMultiplier = 0.55f;
    public float minDamageMultiplier = 0.25f;

    [Header("탄환 설정")]
    public float projectileSpeed;
    public float projectileRadius;
    public string projectileAddress;
    public int projectilePrewarmSize;
    // 한 번 발사할 때 생성하는 탄환 수다. 탄약 소비는 여전히 한 발이다.
    [CsvColumn(Optional = true)] public int bulletsPerShot = 1;
    // 중심에서 벗어날 수 있는 최대 각도다. 16이면 전체 범위는 32도다.
    [CsvColumn(Optional = true)] public float singleShotAngle;

    [Header("AI 소리 감지")]
    public float shotNoiseRadius;

    [Header("무기 외형 설정")]
    [CsvColumn(Optional = true)]
    public string weaponViewAddress;

    [CsvColumn(Optional = true)]
    public int weaponViewPrewarmSize;

    [Header("피드백 관련 설정")]
    [CsvColumn(Optional = true)]
    public string muzzleVfxAddress;
    [CsvColumn(Optional = true)]
    public string hitVfxAddress;

    [CsvColumn(Optional = true)]
    public string fireSoundAddress;

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
        bool hasValidRangedData = weaponType != WeaponType.Ranged ||
                                  (ammoType != AmmoType.None && magazineCapacity > 0 && reloadDuration > 0f);

        return weaponId != 0 &&
               baseDamage > 0f &&
               projectileSpeed > 0f &&
               projectileRadius > 0f &&
               minDamageRange > 0f &&
               !string.IsNullOrWhiteSpace(projectileAddress) &&
               hasValidRangedData;
    }
}
