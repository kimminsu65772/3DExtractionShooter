// 장전 코드가 플레이어 전용 Dictionary나 미래의 인벤토리 구현을 직접 알지 않게 만드는 경계다.
public interface IAmmoStorage
{
    int GetCount(int ammoId);
    // 일부만 들어가도 true이며, 원본에서는 addedCount만 차감한다. 실패하면 addedCount는 0이다.
    bool TryAdd(int ammoId, int amount, out int addedCount);
    bool TryRemove(int ammoId, int amount);
}
