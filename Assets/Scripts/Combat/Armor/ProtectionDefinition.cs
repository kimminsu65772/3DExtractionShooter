using ExtractionShooter.DataPipeline;

public abstract class ProtectionDefinition : ItemDefinition
{
    // 슬롯 프레임 등에 사용할 원본 quality 값이다. 방어 수치와 별개로 보관한다.
    [CsvColumn(Optional = true)] public int rarity;
    // 기존 CSV 열 이름을 유지하되 3.3 같은 실제 방어 수치를 보존한다.
    public float armorLevel;
    // 기존 CSV와 호환되는 초기값을 제공한다. 아이템별 값은 선택 열로 덮어쓴다.
    [CsvColumn(Optional = true)] public float maxDurability = 100f;
    [CsvColumn(Optional = true)] public float durabilityDamageMultiplier = 1f;
}
