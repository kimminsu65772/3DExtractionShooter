using System;

[Serializable]
public struct CharacterStatDefinition
{
    public StatType statType;
    public float baseValue;

    public CharacterStatDefinition(StatType statType, float baseValue)
    {
        this.statType = statType;
        this.baseValue = baseValue;
    }
}
