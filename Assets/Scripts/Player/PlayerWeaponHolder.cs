using UnityEngine;

[DisallowMultipleComponent]
public class PlayerWeaponHolder : MonoBehaviour
{
    [Header("현재 장착 무기")]
    [SerializeField] private WeaponDefinition currentWeapon;
    [SerializeField] private WeaponView currentWeaponView;

    public WeaponDefinition CurrentWeapon => currentWeapon;
    public WeaponView CurrentWeaponView => currentWeaponView;
    public Transform MuzzleTransform => currentWeaponView != null ? currentWeaponView.MuzzleTransform : null;

    public bool HasWeapon =>
        currentWeapon != null &&
        currentWeapon.baseDamage > 0f &&
        currentWeapon.projectileSpeed > 0f &&
        currentWeapon.projectileRadius > 0f &&
        currentWeapon.minDamageRange > 0f;
    public bool HasMuzzle => currentWeaponView != null && currentWeaponView.HasMuzzle;

    public void Equip(WeaponDefinition weapon, WeaponView weaponView)
    {
        currentWeapon = weapon;
        currentWeaponView = weaponView;
    }
}
