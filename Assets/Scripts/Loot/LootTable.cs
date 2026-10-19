using System;
using ExtractionShooter.DataPipeline;
using UnityEngine;

// 공유되는 추첨 설정만 보관한다. 상자별 내용물이나 추첨 완료 여부는 저장하지 않는다.
[CreateAssetMenu(menuName = "Loot/Loot Table")]
public sealed class LootTable : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public ItemType itemType;
        [Min(1)] public int itemId;
        [Min(0)] public float weight = 1f;
        [Min(1)] public int minCount = 1;
        [Min(1)] public int maxCount = 1;
    }

    [Range(0f, 1f)] public float activeChance = 1f;
    [Min(1)] public int inventorySize = 8;
    [Min(0)] public int minRolls = 1;
    [Min(0)] public int maxRolls = 3;
    [Range(0f, 1f)] public float emptyRollChance;
    public Entry[] entries = Array.Empty<Entry>();
    [Range(0f, 1f)] public float fixedItemSpawnChance = 1f;
    public Entry[] fixedItems = Array.Empty<Entry>();
    [TextArea] public string sourceNote;

    public bool Validate(CombatDataCompositionRoot root, out string error)
    {
        error = null;
        if (!IsChance(activeChance) || !IsChance(fixedItemSpawnChance) || !IsChance(emptyRollChance) || inventorySize < 1 || minRolls < 0 || maxRolls < minRolls)
            error = "생성 확률, 칸 수 또는 추첨 횟수가 유효하지 않다.";
        else if (entries == null || fixedItems == null || (maxRolls > 0 && entries.Length == 0 && emptyRollChance < 1f))
            error = "추첨할 후보가 없거나 배열이 비어 있다.";
        if (error != null) return false;

        double total = 0;
        foreach (Entry entry in entries)
        {
            if (!ValidateEntry(entry, root)) { error = "지원하지 않는 아이템 ID, 수량 또는 가중치가 있다."; return false; }
            total += entry.weight;
        }
        if (maxRolls > 0 && emptyRollChance < 1f && (total <= 0 || double.IsInfinity(total)))
        { error = "무작위 후보의 가중치 합은 유한한 양수여야 한다."; return false; }
        foreach (Entry entry in fixedItems)
            if (!ValidateEntry(entry, root)) { error = "고정 지급 항목이 유효하지 않다."; return false; }
        return true;
    }

    public bool TryCreateContents(CombatDataCompositionRoot root, System.Random random, out InventoryContainer contents)
    {
        contents = null;
        if (random == null || !Validate(root, out _)) return false;
        // 원본은 정수 경계 바깥 0.5부터 뽑아 반올림한다. 최솟값/최댓값의 구간 길이도 동일하다.
        int rolls = minRolls == maxRolls ? minRolls : Mathf.Clamp(Mathf.RoundToInt((float)(minRolls - 0.5 + random.NextDouble() * (maxRolls - minRolls + 1))), minRolls, maxRolls);
        bool addFixed = fixedItems.Length > 0 && random.NextDouble() < fixedItemSpawnChance;
        // 모든 추첨 결과가 별도 칸을 요구해도 버리지 않도록 최악의 필요 칸 수를 확보한다.
        long requiredSlots = 0;
        foreach (Entry entry in entries) requiredSlots = Math.Max(requiredSlots, LootItemFactory.GetRequiredSlots(root, entry.itemType, entry.itemId, entry.maxCount));
        requiredSlots *= rolls;
        if (addFixed) foreach (Entry entry in fixedItems) requiredSlots += LootItemFactory.GetRequiredSlots(root, entry.itemType, entry.itemId, entry.maxCount);
        if (requiredSlots > int.MaxValue) return false;
        var candidate = new InventoryContainer(Math.Max(inventorySize, (int)requiredSlots));
        if (addFixed)
            foreach (Entry entry in fixedItems)
                if (!TryAdd(candidate, entry, root, random)) return false;
        double totalWeight = 0;
        foreach (Entry entry in entries) totalWeight += entry.weight;
        for (int i = 0; i < rolls; i++)
        {
            // 제외된 원본 후보가 차지하던 구간은 물건을 생성하지 않고 이번 추첨을 끝낸다.
            double unitRoll = random.NextDouble();
            if (unitRoll < emptyRollChance) continue;
            // 남은 구간만 지원 후보의 누적 가중치로 환산한다. 빈 결과를 다시 뽑지 않는다.
            double roll = (unitRoll - emptyRollChance) / (1d - emptyRollChance) * totalWeight;
            Entry selected = null;
            foreach (Entry entry in entries)
            {
                if (entry.weight <= 0) continue;
                selected = entry;
                roll -= entry.weight;
                if (roll < 0) break;
            }
            if (selected == null || !TryAdd(candidate, selected, root, random)) return false;
        }
        contents = candidate;
        return true;
    }

    private static bool TryAdd(InventoryContainer inventory, Entry entry, CombatDataCompositionRoot root, System.Random random)
    {
        int count = entry.minCount == entry.maxCount ? entry.minCount : (int)(entry.minCount + random.NextDouble() * ((long)entry.maxCount - entry.minCount + 1));
        return LootItemFactory.TryAdd(inventory, root, entry.itemType, entry.itemId, count);
    }

    private static bool ValidateEntry(Entry entry, CombatDataCompositionRoot root)
    {
        return entry != null && !float.IsNaN(entry.weight) && !float.IsInfinity(entry.weight) && entry.weight >= 0
            && entry.minCount > 0 && entry.maxCount >= entry.minCount && LootItemFactory.IsAvailable(root, entry.itemType, entry.itemId);
    }

    private static bool IsChance(float value) => !float.IsNaN(value) && value >= 0f && value <= 1f;
}
