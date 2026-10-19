using System;
using System.Collections.Generic;
using ExtractionShooter.DataPipeline;
using UnityEngine;

// 로딩 시퀀스가 데이터 준비 후 호출한다. Awake나 Start에서는 생성하지 않는다.
[DisallowMultipleComponent]
public sealed class TargetDummySpawner : MonoBehaviour
{
    [Serializable]
    private struct SpawnEntry
    {
        public Transform spawnPoint;
        [Min(0)] public int armorId;
        [Min(0)] public int helmetId;
    }

    [Header("공통 더미 생성 정의")]
    [Tooltip("비활성 더미 프리팹을 지정한 EnemySpawnDefinition SO를 연결한다.")]
    [SerializeField] private EnemySpawnDefinition dummyDefinition;

    [Header("포인트별 보호 장비 (0은 미장착)")]
    [SerializeField] private SpawnEntry[] spawnEntries =
    {
        new SpawnEntry { armorId = 0, helmetId = 0 },
        new SpawnEntry { armorId = 4001, helmetId = 5001 },
        new SpawnEntry { armorId = 4002, helmetId = 5002 },
        new SpawnEntry { armorId = 4003, helmetId = 5003 }
    };

    private readonly List<EnemySpawnComposition> spawnedGroups = new();

    public bool HasSpawned { get; private set; }
    public IReadOnlyList<EnemySpawnComposition> SpawnedGroups => spawnedGroups;

    public bool TrySpawn(CombatDataCompositionRoot dataRoot)
    {
        // 같은 로딩 절차에서 다시 호출해도 더미를 중복 생성하지 않는다.
        if (HasSpawned)
            return true;

        if (!ValidateSetup(dataRoot))
            return false;

        var pending = new List<GameObject>(spawnEntries.Length);
        try
        {
            foreach (SpawnEntry entry in spawnEntries)
            {
                // 비활성 프리팹을 복제하므로 OnEnable 이전에 참조와 장비를 구성할 수 있다.
                GameObject dummy = Instantiate(
                    dummyDefinition.Prefab,
                    entry.spawnPoint.position,
                    entry.spawnPoint.rotation,
                    transform);

                pending.Add(dummy);
                EnemyProtectionLoadout loadout = dummy.GetComponent<EnemyProtectionLoadout>();
                loadout.SetDataRoot(dataRoot);
                if (!loadout.TryInitialize(entry.armorId, entry.helmetId))
                {
                    Debug.LogError($"더미 보호 장비 초기화 실패: {entry.spawnPoint.name}", this);
                    return false;
                }
            }

            // 모든 더미의 구성이 성공한 뒤 한꺼번에 공개한다.
            foreach (GameObject dummy in pending)
                dummy.SetActive(true);

            foreach (GameObject dummy in pending)
                spawnedGroups.Add(dummy.GetComponent<EnemySpawnComposition>());
            HasSpawned = true;
            return true;
        }
        finally
        {
            // 구성 중 실패하면 비활성 복제본이 남거나 다음 호출에서 중복되지 않게 정리한다.
            if (!HasSpawned)
            {
                foreach (GameObject dummy in pending)
                    if (dummy != null)
                        Destroy(dummy);
            }
        }
    }

    private bool ValidateSetup(CombatDataCompositionRoot dataRoot)
    {
        if (dataRoot == null || !dataRoot.isReady)
        {
            Debug.LogError("더미 생성 전에 전투 데이터 초기화를 완료해야 한다.", this);
            return false;
        }

        if (dummyDefinition == null || dummyDefinition.Prefab == null)
        {
            Debug.LogError("프리팹이 지정된 더미 생성 정의 SO를 연결해야 한다.", this);
            return false;
        }

        GameObject dummyPrefab = dummyDefinition.Prefab;
        if (dummyPrefab.GetComponent<HealthComponent>() == null ||
            dummyPrefab.GetComponent<EnemyProtectionLoadout>() == null)
        {
            Debug.LogError("HealthComponent와 EnemyProtectionLoadout이 있는 더미 프리팹을 연결해야 한다.", this);
            return false;
        }

        if (dummyPrefab.activeSelf)
        {
            Debug.LogError("참조 주입 전에 OnEnable이 실행되지 않도록 더미 프리팹 루트를 비활성화해야 한다.", this);
            return false;
        }

        EnemySpawnComposition composition = dummyPrefab.GetComponent<EnemySpawnComposition>();
        if (composition == null)
        {
            Debug.LogError("더미 프리팹 루트에 EnemySpawnComposition을 연결해야 한다.", this);
            return false;
        }
        if (!composition.TryValidate(out string compositionError))
        {
            Debug.LogError($"더미 표시 데이터 오류: {compositionError}", this);
            return false;
        }

        if (spawnEntries == null || spawnEntries.Length == 0)
        {
            Debug.LogError("더미 스폰 포인트를 하나 이상 연결해야 한다.", this);
            return false;
        }

        for (int i = 0; i < spawnEntries.Length; i++)
        {
            SpawnEntry entry = spawnEntries[i];
            if (entry.spawnPoint == null)
            {
                Debug.LogError($"더미 스폰 항목 {i}에 스폰 포인트가 없다.", this);
                return false;
            }

            if (entry.armorId < 0 || entry.helmetId < 0 ||
                (entry.armorId != 0 && !dataRoot.ArmorCatalog.TryGet(entry.armorId, out _)) ||
                (entry.helmetId != 0 && !dataRoot.HelmetCatalog.TryGet(entry.helmetId, out _)))
            {
                Debug.LogError($"더미 스폰 항목 {i}의 보호 장비 ID가 유효하지 않다. 방어구: {entry.armorId}, 헬멧: {entry.helmetId}", this);
                return false;
            }
        }

        return true;
    }
}
