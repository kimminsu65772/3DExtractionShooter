using System;
using System.Collections.Generic;
using UnityEngine;

// 구역에서 생성할 종류와 수량을 보관한다. 실제 포인트와 런타임 참조는 씬의 스포너가 갖는다.
[CreateAssetMenu(menuName = "ExtractionShooter/Enemy/Spawn Table")]
public sealed class EnemySpawnTable : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private EnemySpawnDefinition definition;
        [Tooltip("구역별 장비 표. 비우면 프리팹 각 대원의 표를 사용한다. 분대는 비워 둔다.")]
        [SerializeField] private EnemyEquipmentTable equipmentTable;
        [Tooltip("이 항목을 생성할 확률. 무기 후보끼리 비교하는 가중치가 아니다.")]
        [SerializeField, Range(0f, 1f)] private float spawnChance = 1f;
        [SerializeField, Min(0)] private int minimumCount;
        [SerializeField, Min(0)] private int maximumCount;

        public EnemySpawnDefinition Definition => definition;
        public EnemyEquipmentTable EquipmentTable => equipmentTable;
        public float SpawnChance => spawnChance;
        // 분대 정의에서 수량 1은 대장 1명과 Guard 3명을 묶은 한 분대다.
        public int MinimumCount => minimumCount;
        public int MaximumCount => maximumCount;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();
    public IReadOnlyList<Entry> Entries => entries;
}
