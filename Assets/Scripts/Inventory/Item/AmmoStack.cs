using System;

// 인벤토리를 구성하는 아이템 중 탄약을 나타내는 클래스.
public class AmmoStack : IInventoryItem
{
    private readonly AmmoDefinition ammo;
    private int currentCount;

    public ItemType Type => ItemType.Ammo;
    public int ItemId => ammo.ammoId;

    public AmmoStack(AmmoDefinition ammo, int count)
    {
        if (ammo == null)
        {
            throw new ArgumentNullException(nameof(ammo), "AmmoDefinition은 null이면 안 됩니다..");
        }
        this.ammo = ammo;
        if (count > ammo.maxStackCount)
        {
            throw new ArgumentOutOfRangeException(nameof(count), $"count는 ammo.maxStackCount({ammo.maxStackCount})를 초과할 수 없습니다.");
        }
        else if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count는 0이하일 수 없습니다.");
        }
        
        currentCount = count;
    }

    public int GetCurrentCount()
    {
        return currentCount;
    }

}
