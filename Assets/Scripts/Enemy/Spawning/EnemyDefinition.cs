using UnityEngine;

// 개별 캐릭터의 공유 표시 데이터. 생성 단위와 실행 중 체력은 보관하지 않는다.
[CreateAssetMenu(menuName = "ExtractionShooter/Enemy/Character Definition")]
public sealed class EnemyDefinition : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private bool isNamed;

    public string DisplayName => displayName;
    public bool IsNamed => isNamed;
}
