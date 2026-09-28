public enum PlayerLocomotionState
{
    None,
    Idle,
    Move,
    Sprint,
    AimIdle,
    AimMove,
}

public enum HitPart
{
    None,
    Head,
    Body
}

public enum  WeaponType
{
    None,
    Melee,
    Ranged
}

// 무기와 탄약이 서로 호환되는지 구분하는 종류다.
// 실제 탄약 아이템은 Rifle 한 종류부터 만들지만, 이미 존재하는 테스트 무기 데이터가
// 잘못된 Rifle 탄약을 공유하지 않도록 권총과 산탄총 호환 분류도 미리 구분한다.
public enum AmmoType
{
    None = 0,
    Rifle = 1,
    Pistol = 2,
    Shotgun = 3
}

public enum PlayerActionState
{
    Free = 0,
    Reload = 1,
    Dodge = 2,
    Hit = 3,
    Dead = 4
}

// 무기를 들었을 때 사용할 애니메이션 자세의 종류
public enum WeaponAnimationStyle
{
    // 무기를 장착하지 않은 자세
    Unarmed = 0,

    // 한 손 또는 양손으로 권총을 잡는 자세
    Pistol = 1,

    // 양손으로 긴 총을 잡는 자세
    TwoHandedRifle = 2,

    // 한 손 근접 무기 자세
    OneHandedMelee = 3,

    // 양손 근접 무기 자세
    TwoHandedMelee = 4
}

// 적의 상태를 구분하는 용도
public enum  EnemyState
{
    None,
    Idle,
    Investigate,
    Chase,
    Attack,
    Dead
}
