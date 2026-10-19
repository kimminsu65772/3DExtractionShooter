using System;
using System.Collections.Generic;
using UnityEngine;

// 생성 루트에서 실제 캐릭터와 공유 SO를 연결한다. 분대는 4개, 단독 유닛은 1개 항목을 둔다.
[DisallowMultipleComponent]
public sealed class EnemySpawnComposition : MonoBehaviour
{
    [Serializable]
    public sealed class MemberEntry
    {
        [SerializeField] private HealthComponent health;
        [SerializeField] private EnemyDefinition definition;

        public HealthComponent Health => health;
        public EnemyDefinition Definition => definition;
    }

    [SerializeField] private MemberEntry[] members = Array.Empty<MemberEntry>();

    public IReadOnlyList<MemberEntry> Members => members;

    // 비활성 프리팹에서도 검사할 수 있도록 Awake에 의존하지 않는다.
    public bool TryValidate(out string error)
    {
        if (members == null || members.Length == 0)
        {
            error = "캐릭터와 Definition의 연결 목록이 비어 있다.";
            return false;
        }

        var registered = new HashSet<HealthComponent>();
        foreach (var member in members)
        {
            if (member == null || member.Health == null || member.Definition == null)
            {
                error = "구성원에 HealthComponent와 EnemyDefinition을 모두 연결해야 한다.";
                return false;
            }
            if (!member.Health.transform.IsChildOf(transform) || !registered.Add(member.Health))
            {
                error = "구성원은 이 생성 루트 내부의 서로 다른 캐릭터여야 한다.";
                return false;
            }
        }

        foreach (var health in GetComponentsInChildren<HealthComponent>(true))
        {
            if (!registered.Contains(health))
            {
                error = "Definition이 연결되지 않은 캐릭터가 생성 루트에 있다.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
