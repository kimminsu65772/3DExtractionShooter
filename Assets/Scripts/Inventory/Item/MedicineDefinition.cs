using System;
using ExtractionShooter.DataPipeline;

// 회복량과 사용 시간을 정의하고, 키트는 최대 사용량 대비 소비 비율을 지정한다.
[Serializable]
public class MedicineDefinition : ItemDefinition
{
    public float healAmount;
    public float useDuration = 2f;
    // 빈 값은 붕대처럼 개수를 소비한다는 뜻이다. 0.25는 기본 회복량을 회복할 때 25%를 소비한다.
    [CsvColumn(Optional = true)] public float? useCostRatio;
    public override ItemType Type => ItemType.Meds;
}
