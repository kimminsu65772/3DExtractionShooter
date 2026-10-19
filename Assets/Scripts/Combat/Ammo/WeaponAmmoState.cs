using System;

// 실제 무기 한 자루의 탄창 상태다. Unity 오브젝트의 생명주기와 관계없는 순수 C# 객체로 둔다.
public class WeaponAmmoState
{
    private int loadedAmmoId;
    private int loadedAmmoCount;
    // 빈 탄창 표시용으로 마지막 탄약 ID를 기억한다. 실제 장전 상태와는 구분한다.
    private int lastLoadedAmmoId;

    public AmmoType CompatibleAmmoType { get; }
    public int Capacity { get; }
    public int LoadedAmmoId => loadedAmmoId;
    public int LoadedAmmoCount => loadedAmmoCount;
    public int LastLoadedAmmoId => lastLoadedAmmoId;
    public int RemainingCapacity => Capacity - loadedAmmoCount;
    public bool IsEmpty => loadedAmmoCount <= 0;
    public bool IsFull => loadedAmmoCount >= Capacity;
    public bool CanFire => loadedAmmoCount > 0;

    public WeaponAmmoState(AmmoType compatibleAmmoType, int capacity)
    {
        if (compatibleAmmoType == AmmoType.None)
            throw new ArgumentException("탄창에 사용할 탄약 종류가 지정되지 않았습니다.", nameof(compatibleAmmoType));
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "탄창 용량은 0보다 커야 합니다.");

        CompatibleAmmoType = compatibleAmmoType;
        Capacity = capacity;
    }

    // 이 탄창에 지정한 탄약을 넣을 수 있는지 확인한다.
    public bool CanLoad(AmmoDefinition ammo)
    {
        if (ammo == null || ammo.ammoId <= 0 || ammo.ammoType != CompatibleAmmoType || IsFull)
            return false;

        // 탄창에 총알이 남아 있다면 서로 다른 ammoId를 섞지 않는다.
        return IsEmpty || loadedAmmoId == ammo.ammoId;
    }

    // 요청한 수량 중 실제로 탄창에 들어간 수량을 반환한다.
    public int Load(AmmoDefinition ammo, int amount)
    {
        if (amount <= 0 || !CanLoad(ammo))
            return 0;

        int loadedAmount = Math.Min(amount, RemainingCapacity);
        loadedAmmoId = ammo.ammoId;
        lastLoadedAmmoId = ammo.ammoId;
        loadedAmmoCount += loadedAmount;
        return loadedAmount;
    }

    // 투사체 생성에 성공한 뒤 호출하며, 소비한 탄약 ID도 함께 돌려준다.
    public bool TryConsumeRound(out int consumedAmmoId)
    {
        if (!CanFire)
        {
            consumedAmmoId = 0;
            return false;
        }

        consumedAmmoId = loadedAmmoId;
        loadedAmmoCount--;

        if (loadedAmmoCount == 0)
            loadedAmmoId = 0;

        return true;
    }

    public bool TryRestore(int ammoId, int ammoCount, int lastAmmoId)
    {
        if (ammoCount < 0 || ammoCount > Capacity)
            return false;
        if (ammoCount == 0)
        {
            if (ammoId != 0)
                return false;
        }
        else
        {
            if (ammoId <= 0 || ammoId != lastAmmoId)
                return false;
        }

        if (lastAmmoId < 0)
            return false;

        loadedAmmoId = ammoId;
        loadedAmmoCount = ammoCount;
        lastLoadedAmmoId = lastAmmoId;
        return true;
    }
}
