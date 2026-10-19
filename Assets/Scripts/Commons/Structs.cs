using UnityEngine;

public struct DamageInfo
{
    // 데미지를 발생시킨 무기의 ID
    public int weaponId;

    // 피해를 발생시킨 탄약과 방어구 관통 정보
    public int ammoId;
    public int armorPenetrationLevel;
    public float ammoDamageMultiplier;

    // 실제로 주려는 데미지
    public float incomingDamage;

    // 피격자의 방어구까지 계산한 뒤 실제 체력에서 차감한 데미지
    public float appliedDamage;

    // 피격자가 사용한 소수 방어 수치와 그 결과로 적용된 데미지 배율
    public float armorLevel;
    public float armorMultiplier;

    // 발사 위치와 마우스 커서 위치 사이의 거리
    public float aimDistance;

    // 실제 충돌 위치
    public Vector3 hitPoint;

    // 투사체 진행 방향
    public Vector3 hitDirection;

    // 피격 부위
    public HitPart hitPart;

    // 공격자
    public GameObject attacker;

    /*
     * 디버그용 정보
     */
    // 해당 무기의 기본 데미지
    public float baseDamage;
    // 사거리 구간에 따른 데미지 배율
    public float rangeMultiplier;
    // 피격 부위에 따른 데미지 배율
    public float hitPartMultiplier;
}
