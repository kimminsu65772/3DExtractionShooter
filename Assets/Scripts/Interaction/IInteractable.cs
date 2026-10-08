// 상자와 바닥 아이템처럼 서로 다른 물체를 같은 방법으로 확인하고 사용한다.
// 구현 컴포넌트는 감지용 Collider와 같은 오브젝트 또는 그 부모에 배치한다.
public interface IInteractable
{
    // 대상 안내 UI에서 표시할 동작 이름을 제공한다.
    string InteractionLabel { get; }

    // 현재 플레이어가 이 물체를 사용할 수 있는지 확인한다. 여기에서는 물품이나 상태를 변경하지 않는다.
    bool CanInteract(PlayerInteractionController interactor);

    // 실제 동작을 시도한다. 사용할 수 없는 상태가 됐다면 변경하지 않고 false를 반환한다.
    bool TryInteract(PlayerInteractionController interactor);
}
