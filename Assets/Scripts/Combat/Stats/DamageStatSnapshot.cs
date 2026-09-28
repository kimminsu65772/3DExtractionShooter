// 이미 날아간 투사체의 피해량이 변경되는 것을 방지하기 위해, 투사체가 발사될 때의 피해량을 스냅샷으로 저장한다.
public readonly struct DamageStatSnapshot
{
    public static DamageStatSnapshot Identity => new DamageStatSnapshot(1f, 1f);

    public float OutgoingDamageMultiplier { get; }
    public float HeadshotDamageMultiplier { get; }

    public DamageStatSnapshot(float outgoingDamageMultiplier, float headshotDamageMultiplier)
    {
        OutgoingDamageMultiplier = outgoingDamageMultiplier > 0f ? outgoingDamageMultiplier : 0f;
        HeadshotDamageMultiplier = headshotDamageMultiplier > 0f ? headshotDamageMultiplier : 0f;
    }
}
