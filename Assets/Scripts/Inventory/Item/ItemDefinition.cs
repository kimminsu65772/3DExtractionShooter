using System;
using ExtractionShooter.DataPipeline;

// 회복 아이템과 보호 장비가 공유하는 변하지 않는 정보다. 현재 수량은 저장하지 않는다.
[Serializable]
public abstract class ItemDefinition
{
    public int itemId;
    public string displayName;
    public float unitWeight;
    public int maxStackCount = 1;
    [CsvColumn(Optional = true)] public string iconAddress;
    public abstract ItemType Type { get; }
}
