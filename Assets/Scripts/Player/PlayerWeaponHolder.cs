using ExtractionShooter.DataPipeline;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerWeaponHolder : MonoBehaviour
{
    [Header("전투 관련 데이터")]
    [SerializeField] private CombatDataCompositionRoot dataRoot;
    [SerializeField] private Transform weaponMount;


    private WeaponInstance currentWeaponInstance;
    private WeaponView currentWeaponView;

    private PoolLease currentWeaponViewLease;
    private bool hasWeaponViewLease;

    public WeaponView CurrentWeaponView => currentWeaponView;

    // 기존 사격 코드는 RuntimeWeapon을 계속 읽을 수 있게 유지한다.
    public RuntimeWeapon CurrentWeapon => currentWeaponInstance?.RuntimeWeapon;
    public WeaponInstance CurrentWeaponInstance => currentWeaponInstance;
    public WeaponAmmoState CurrentAmmoState => currentWeaponInstance?.AmmoState;
    public CombatDataCompositionRoot DataRoot => dataRoot;

    public Transform MuzzleTransform =>
        currentWeaponView != null
        ? currentWeaponView.MuzzleTransform
        : null;

    public bool HasWeapon =>
        CurrentWeapon != null &&
        CurrentWeapon.Definition != null &&
        CurrentWeapon.ProjectileId > 0;

    public bool HasMuzzle =>
        currentWeaponView != null &&
        currentWeaponView.HasMuzzle;

    private void OnDisable()
    {
        Unequip();
    }

    public bool TryEquip(WeaponInstance weapon)
    {
        if (dataRoot == null || !dataRoot.isReady)
        {
            Debug.LogWarning(
                "CombatDataCompositionRoot가 준비되지 않았습니다. " +
                "무기를 장착할 수 없습니다.",
                this
            );
            return false;
        }

        if (weapon == null)
        {
            Debug.LogWarning(
                "장착할 무기 인스턴스가 null입니다.",
                this
            );
            return false;
        }

        RuntimeWeapon runtimeWeapon = weapon.RuntimeWeapon;

        int weaponId = weapon.WeaponId;


        if (runtimeWeapon.WeaponViewId <= 0)
        {
            Debug.LogError(
                $"WeaponId {weaponId}에 해당하는 무기의 WeaponViewId가 유효하지 않습니다.",
                this
            );
            return false;
        }

        if (!dataRoot.Pools.TryRent(runtimeWeapon.WeaponViewId, out PoolLease newLease))
        {
            return false;
        }

        GameObject viewInstance = newLease.Instance;

        WeaponView newWeaponView = viewInstance.GetComponentInChildren<WeaponView>(includeInactive: true);

        if (newWeaponView == null || !newWeaponView.HasMuzzle)
        {
            Debug.LogError(
                $"WeaponId {weaponId}에 해당하는 WeaponView가 유효하지 않거나 MuzzleTransform이 없습니다.",
                this
            );

            dataRoot.Pools.TryReturn(newLease);
            return false;
        }

        if (weaponMount == null)
        {
            dataRoot.Pools.TryReturn(newLease);
            Debug.LogError("WeaponMount가 설정되지 않았습니다.", this);
            return false;
        }

        // 홀더의 크기 배율을 상쇄하며 장착하고, 실패하면 새 외형만 반환한다.
        if (!newWeaponView.TryAttachTo(weaponMount))
        {
            dataRoot.Pools.TryReturn(newLease);
            return false;
        }

        dataRoot.Pools.Activate(newLease);

        Unequip(); // 이전 무기 해제

        currentWeaponInstance = weapon;
        currentWeaponView = newWeaponView;
        currentWeaponViewLease = newLease;
        hasWeaponViewLease = true;

        return true;
    }
    public bool TryEquip(int weaponId)
    {
        if (dataRoot == null || !dataRoot.isReady)
            return false;

        WeaponCatalog catalog = dataRoot.WeaponCatalog;
        if (catalog == null || !catalog.TryGet(weaponId, out RuntimeWeapon runtimeWeapon))
            return false;

        if (runtimeWeapon.Definition.weaponType != WeaponType.Ranged)
            return false;

        WeaponInstance weapon = new WeaponInstance(runtimeWeapon);

        // 외형 준비와 실제 장착 처리는 위에서 변경한 메서드에 맡깁니다.
        return TryEquip(weapon);
    }

    public void Unequip()
    {
        if (hasWeaponViewLease && dataRoot?.Pools != null)
        {
            dataRoot.Pools.TryReturn(
                currentWeaponViewLease
            );
        }

        currentWeaponInstance = null;
        currentWeaponView = null;
        currentWeaponViewLease = default;
        hasWeaponViewLease = false;
    }
}
