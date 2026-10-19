using System;
using System.Collections.Generic;
using ExtractionShooter.DataPipeline;
using UnityEngine;
using UnityEngine.AI;

// 로딩 시퀀스가 호출하는 단일 진입점. 일반 적과 분대 모두 비활성 생성 → 주입 → 활성화한다.
[DisallowMultipleComponent]
public sealed class StageEnemySpawner : MonoBehaviour
{
    [Serializable]
    private sealed class SpawnArea
    {
        public EnemySpawnTable table;
        public Transform[] spawnPoints;
    }

    [SerializeField] private SpawnArea[] areas = Array.Empty<SpawnArea>();
    [SerializeField, Min(0.01f)] private float navMeshSampleRadius = 1f;

    private CombatDataCompositionRoot dataRoot;
    private Transform playerRoot;
    private Transform targetViewPoint;
    private AimPointProvider targetAimPointProvider;
    private readonly List<EnemySpawnComposition> spawnedGroups = new();

    public bool HasSpawned { get; private set; }
    public IReadOnlyList<EnemySpawnComposition> SpawnedGroups => spawnedGroups;

    public void SetDataRoot(CombatDataCompositionRoot root) => dataRoot = root;

    public void SetTarget(Transform target, Transform viewPoint, AimPointProvider aimPointProvider)
    {
        playerRoot = target;
        targetViewPoint = viewPoint;
        targetAimPointProvider = aimPointProvider;
    }

    public void ClearTarget()
    {
        SetTarget(null, null, null);
        // 소유한 적에게 주입했던 플레이어와 조준 지점을 함께 해제한다.
        foreach (var perception in GetComponentsInChildren<EnemyPerception>(true))
            perception.SetTarget(null, null, null);
    }

    public bool TrySpawn()
    {
        if (HasSpawned) return true;
        if (dataRoot == null || !dataRoot.isReady || playerRoot == null || targetAimPointProvider == null)
        {
            Debug.LogError("적 스폰 전에 데이터 준비와 플레이어 참조 주입을 완료해야 한다.", this);
            return false;
        }
        if (areas == null || areas.Length == 0)
        {
            Debug.LogError("적을 생성할 구역과 스폰 테이블을 연결해야 한다.", this);
            return false;
        }

        var pending = new List<(GameObject Root, EnemySpawnDefinition Definition)>();
        var equipmentRandom = new System.Random();
        try
        {
            foreach (var area in areas)
            {
                if (!ValidateArea(area)) return false;
                var availablePoints = new List<Transform>(area.spawnPoints);
                foreach (var entry in area.table.Entries)
                {
                    if (entry.SpawnChance <= 0f || (entry.SpawnChance < 1f && UnityEngine.Random.value >= entry.SpawnChance))
                        continue;
                    int count = UnityEngine.Random.Range(entry.MinimumCount, entry.MaximumCount + 1);
                    if (count > availablePoints.Count)
                    {
                        Debug.LogError($"{area.table.name}: 생성 수량에 비해 스폰 포인트가 부족하다.", this);
                        return false;
                    }
                    for (int i = 0; i < count; i++)
                    {
                        int pointIndex = UnityEngine.Random.Range(0, availablePoints.Count);
                        Transform point = availablePoints[pointIndex];
                        availablePoints.RemoveAt(pointIndex);
                        GameObject enemy = Instantiate(entry.Definition.Prefab, point.position, point.rotation, transform);
                        EnemySpawnDefinition definition = entry.Definition;
                        pending.Add((enemy, definition));
                        if (!Prepare(enemy, entry.EquipmentTable, equipmentRandom)) return false;
                    }
                }
            }

            // 분대의 내부 참조와 모든 적의 외부 참조를 연결한 뒤 AI의 Awake/OnEnable을 허용한다.
            foreach (var pair in pending) pair.Root.SetActive(true);
            foreach (var pair in pending)
            {
                GameObject enemy = pair.Root;
                foreach (var loadout in enemy.GetComponentsInChildren<EnemyWeaponLoadout>(true))
                    if (!loadout.IsReady)
                    {
                        Debug.LogError($"{enemy.name}: 스폰 장비 초기화에 실패했다.", enemy);
                        return false;
                    }
                foreach (var loadout in enemy.GetComponentsInChildren<EnemyProtectionLoadout>(true))
                    if (!loadout.IsReady) return false;
            }
            // 구성원별 SO는 생성 루트가 보관하며, 전체 성공 후에만 HUD에 공개한다.
            foreach (var pair in pending)
                spawnedGroups.Add(pair.Root.GetComponent<EnemySpawnComposition>());
            HasSpawned = true;
            return true;
        }
        finally
        {
            if (!HasSpawned)
                foreach (var pair in pending)
                    if (pair.Root != null)
                    {
                        pair.Root.SetActive(false);
                        Destroy(pair.Root);
                    }
        }
    }

    private bool Prepare(GameObject root, EnemyEquipmentTable equipmentTable, System.Random random)
    {
        EnemySpawnComposition composition = root.GetComponent<EnemySpawnComposition>();
        if (composition == null)
        {
            Debug.LogError($"{root.name}: 생성 루트에 EnemySpawnComposition을 연결해야 한다.", root);
            return false;
        }
        if (!composition.TryValidate(out string compositionError))
        {
            Debug.LogError($"{root.name}: {compositionError}", root);
            return false;
        }

        foreach (var perception in root.GetComponentsInChildren<EnemyPerception>(true))
            perception.SetTarget(playerRoot, targetViewPoint, targetAimPointProvider);
        foreach (var loadout in root.GetComponentsInChildren<EnemyWeaponLoadout>(true)) loadout.SetDataRoot(dataRoot);
        foreach (var attack in root.GetComponentsInChildren<EnemyAttackController>(true)) attack.SetDataRoot(dataRoot);
        foreach (var protection in root.GetComponentsInChildren<EnemyProtectionLoadout>(true)) protection.SetDataRoot(dataRoot);
        foreach (var member in root.GetComponentsInChildren<SquadMemberBrain>(true)) member.SetDataRoot(dataRoot);

        foreach (var equipment in root.GetComponentsInChildren<EnemyEquipmentLoadout>(true))
            if (!equipment.PrepareSpawn(dataRoot, equipmentTable, random)) return false;

        // 분대의 상대 배치를 유지하되 각 대원의 실제 발 위치를 해당 Agent의 NavMesh에 보정한다.
        foreach (var agent in root.GetComponentsInChildren<NavMeshAgent>(true))
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, navMeshSampleRadius, filter))
            {
                Debug.LogError($"{agent.name}: 스폰 위치 근처에 사용할 NavMesh가 없다.", agent);
                return false;
            }
            agent.transform.position = hit.position;
        }
        return true;
    }

    private bool ValidateArea(SpawnArea area)
    {
        if (area == null || area.table == null || area.spawnPoints == null || area.spawnPoints.Length == 0 || area.table.Entries.Count == 0)
        {
            Debug.LogError("스폰 구역에는 비어 있지 않은 테이블과 포인트가 필요하다.", this);
            return false;
        }
        var uniquePoints = new HashSet<Transform>();
        foreach (Transform point in area.spawnPoints)
            if (point == null || !uniquePoints.Add(point))
            {
                Debug.LogError($"{area.table.name}: 비어 있거나 중복된 스폰 포인트가 있다.", this);
                return false;
            }
        foreach (var entry in area.table.Entries)
            if (entry == null || entry.Definition == null || entry.Definition.Prefab == null || entry.Definition.Prefab.activeSelf ||
                entry.MinimumCount < 0 || entry.MaximumCount < entry.MinimumCount)
            {
                Debug.LogError($"{area.table.name}: 비활성 프리팹과 유효한 생성 수량을 설정해야 한다.", this);
                return false;
            }
        return true;
    }
}
