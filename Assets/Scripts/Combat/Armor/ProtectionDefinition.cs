using ExtractionShooter.DataPipeline;

public abstract class ProtectionDefinition : ItemDefinition
{
    public int armorLevel;
    // 기존 CSV와 호환되는 초기값을 제공한다. 아이템별 값은 선택 열로 덮어쓴다.
    [CsvColumn(Optional = true)] public float maxDurability = 100f;
    [CsvColumn(Optional = true)] public float durabilityDamageMultiplier = 1f;
}
