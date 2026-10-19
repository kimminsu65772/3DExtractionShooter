using System;
using System.Collections.Generic;
using ExtractionShooter.DataPipeline;
using UnityEngine;

// 탐험 로딩 중 명시적으로 호출한다. Awake/Start나 상자 열기에 의해 다시 추첨하지 않는다.
public sealed class StageLootInitializer : MonoBehaviour
{
    [SerializeField] private LootContainer[] containers = Array.Empty<LootContainer>();
    private bool isInitialized;
    public bool IsInitialized => isInitialized;

    public bool TryInitialize(CombatDataCompositionRoot root, System.Random random = null)
    {
        if (isInitialized) return true;
        if (root == null || !root.isReady) return false;
        random ??= new System.Random();
        var unique = new HashSet<LootContainer>();
        var pending = new List<(LootContainer container, InventoryContainer inventory)>();
        foreach (LootContainer container in containers)
        {
            if (container == null || !unique.Add(container)) return ReportFailure("상자 참조가 없거나 중복되었다.");
            if (!container.gameObject.activeInHierarchy || container.IsInitialized) continue;
            // 시작 상자의 기존 고정 목록도 변경하지 않고 같은 로딩 단계에서 준비한다.
            if (container.RandomTable == null)
            {
                if (!container.TryPrepareFixedContents(root, out InventoryContainer fixedContents)) return ReportFailure($"'{container.name}'의 고정 목록을 담을 수 없다.");
                pending.Add((container, fixedContents));
                continue;
            }
            if (!container.RandomTable.Validate(root, out string error)) return ReportFailure($"'{container.name}': {error}");
        }

        var hidden = new List<GameObject>();
        foreach (LootContainer container in containers)
        {
            // 원래 비활성인 다른 구역·제외 그룹은 이번 탐험에서도 활성화하지 않는다.
            if (!container.gameObject.activeInHierarchy || container.IsInitialized) continue;
            LootTable table = container.RandomTable;
            if (table == null) continue;
            if (random.NextDouble() >= table.activeChance) { hidden.Add(container.gameObject); continue; }
            if (!table.TryCreateContents(root, random, out InventoryContainer contents)) return ReportFailure($"'{container.name}'의 생성 결과를 담을 수 없다.");
            pending.Add((container, contents));
        }

        // 일부 상자 생성 실패로 미완성 내용물을 공개하지 않도록, 전부 준비된 뒤에만 반영한다.
        foreach (var entry in pending) entry.container.InitializePreparedContents(entry.inventory);
        foreach (GameObject box in hidden) box.SetActive(false);
        isInitialized = true;
        return true;
    }

    private bool ReportFailure(string message)
    {
        Debug.LogError($"StageLootInitializer: {message}", this);
        return false;
    }
}
