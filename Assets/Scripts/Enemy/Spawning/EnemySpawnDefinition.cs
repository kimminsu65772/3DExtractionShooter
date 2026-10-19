using UnityEngine;

// 생성 단위만 정의한다. 장비 인스턴스와 생성 여부는 각 실행 오브젝트가 소유한다.
[CreateAssetMenu(menuName = "ExtractionShooter/Enemy/Spawn Definition")]
public sealed class EnemySpawnDefinition : ScriptableObject
{
    [Tooltip("참조를 주입한 뒤 활성화할 비활성 루트 프리팹. 분대는 대장과 대원을 포함한 그룹 프리팹을 지정한다.")]
    [SerializeField] private GameObject prefab;

    // Brain 타입에 종속되지 않아 이후 근접 적도 같은 생성 설정을 사용할 수 있다.
    public GameObject Prefab => prefab;
}
