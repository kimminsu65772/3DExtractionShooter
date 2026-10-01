using System;
public class WeaponInventoryItem : IInventoryItem
{
    private readonly WeaponInstance weapon;

    public ItemType Type => ItemType.Weapon;
    public int ItemId => weapon.WeaponId;

    public WeaponInventoryItem(WeaponInstance weapon)
    {
        if (weapon == null)
        {
            throw new ArgumentNullException(nameof(weapon), "WeaponInstance는 null이면 안 됩니다.");
        }
        this.weapon = weapon;
    }

    public WeaponInstance GetWeapon()
    {
        return weapon;
    }
}
