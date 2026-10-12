using UnityEngine;

public interface ICover
{
    // 거리 비교와 보호 방향 판정에 사용할 실제 몸체 충돌체를 제공한다.
    Collider CoverCollider { get; }

    // 파괴·비활성화 여부를 포함해 현재 보호 기능을 사용할 수 있는지 제공한다.
    bool IsAvailable { get; }

    // 해당 캐릭터가 이 엄폐물의 보호 대상으로 등록되어 있는지 확인한다.
    bool IsCovering(GameObject character);
}
