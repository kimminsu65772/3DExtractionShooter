// 장전 코드가 플레이어 전용 Dictionary나 미래의 인벤토리 구현을 직접 알지 않게 만드는 경계다.
public interface IAmmoStorage
{
    int GetCount(int ammoId);
    bool TryAdd(int ammoId, int amount);
    bool TryRemove(int ammoId, int amount);
}
