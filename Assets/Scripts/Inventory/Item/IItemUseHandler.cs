// 아이템 종류마다 다른 사용 동작을 공통 창구에서 요청할 수 있게 한다.
public interface IItemUseHandler
{
    // 사용이 끝났는지 공통 창구와 전투 기능에서 확인한다.
    bool IsUsing { get; }

    // 현재 사용 가능 여부가 아니라 이 담당자가 처리하는 아이템 종류인지 확인한다.
    bool CanHandle(IInventoryItem item);

    // 사용 조건을 검사하고 실제 사용 시작을 시도한다.
    bool TryUse(IInventoryItem item);

    // 진행 중인 사용을 취소한다.
    void CancelUse();
}
