using System;
using ExtractionShooter.DataPipeline;

[Serializable]
public class AmmoDefinition
{
    // 탄약 데이터를 구분하는 고유 번호다.
    public int ammoId;

    // UI에서 보여줄 탄약 이름이다.
    public string displayName;

    // 나중에 UI가 Addressables에서 아이콘을 불러올 때 사용할 주소다.
    [CsvColumn(Optional = true)] public string iconAddress;

    // 어떤 종류의 무기에 장전할 수 있는지 나타낸다.
    public AmmoType ammoType;

    // 방어구 등급과 비교할 관통 등급이다.
    public int armorPenetrationLevel;

    // 무기의 기본 데미지에 곱할 탄약 보정값이다.
    public float damageMultiplier;

    // 인벤토리 한 칸에 쌓을 수 있는 최대 탄약 수다.
    public int maxStackCount;

    // 탄약 한 발이 인벤토리 무게에 더하는 값이다.
    public float unitWeight;
}
