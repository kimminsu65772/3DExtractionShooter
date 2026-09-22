using ExtractionShooter.DataPipeline;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerWeaponHolder : MonoBehaviour
{
    [Header("전투 관련 데이터")]
    [SerializeField] private CombatDataCompositionRoot dataRoot;
    [SerializeField] private int initialWeaponId = 1001;
    [SerializeField] private Transform weaponMount;


    private RuntimeWeapon currentWeapon;
    private WeaponView currentWeaponView;

    private PoolLease currentWeaponViewLease;
    private bool hasWeaponViewLease;

    public WeaponView CurrentWeaponView => currentWeaponView;

    public RuntimeWeapon CurrentWeapon => currentWeapon;

    public Transform MuzzleTransform =>
        currentWeaponView != null
        ? currentWeaponView.MuzzleTransform
        : null;

    public bool HasWeapon =>
        currentWeapon != null &&
        currentWeapon.Definition != null &&
        currentWeapon.ProjectileId > 0;

    public bool HasMuzzle =>
        currentWeaponView != null &&
        currentWeaponView.HasMuzzle;

    private void OnEnable()
    {
        if (dataRoot == null)
        {
            Debug.LogError(
                "CombatDataCompositionRoot 참조가 설정되지 않았습니다.",
                this
            );

            return;
        }

        dataRoot.OnInitialized += HandleDataInitialized;

        // 파이프라인이 먼저 준비된 뒤 Player가 활성화된 경우를 처리한다.
        if (dataRoot.isReady)
        {
            TryEquip(initialWeaponId);
        }
    }
    private void OnDisable()
    {
        if (dataRoot != null)
        {
            dataRoot.OnInitialized -= HandleDataInitialized;
        }

        Unequip();
    }

    private void HandleDataInitialized()
    {
        if (!TryEquip(initialWeaponId))
        {
            Debug.LogError(
                $"초기 무기를 장착하지 못했습니다. " +
                $"WeaponId: {initialWeaponId}",
                this
            );
        }
    }

    public bool TryEquip(int weaponId)
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

        WeaponCatalog catalog = dataRoot.WeaponCatalog;

        if (catalog == null)
            {
            Debug.LogError(
                "WeaponCatalog가 준비되지 않았습니다. " +
                "무기를 장착할 수 없습니다.",
                this
            );
            return false;
        }

        if (!catalog.TryGet(weaponId, out RuntimeWeapon runtimeWeapon))
        {
            Debug.LogError(
                $"WeaponCatalog에서 WeaponId {weaponId}에 해당하는 무기를 찾을 수 없습니다.",
                this
            );

            return false;
        }

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

        viewInstance.transform.SetParent(weaponMount, false);
        viewInstance.transform.localPosition = Vector3.zero;
        viewInstance.transform.localRotation = Quaternion.identity;

        dataRoot.Pools.Activate(newLease);

        Unequip(); // 이전 무기 해제

        currentWeapon = runtimeWeapon;
        currentWeaponView = newWeaponView;
        currentWeaponViewLease = newLease;
        hasWeaponViewLease = true;

        return true;
    }

    public void Unequip()
    {
        if (hasWeaponViewLease && dataRoot?.Pools != null)
        {
            dataRoot.Pools.TryReturn(
                currentWeaponViewLease
            );
        }

        currentWeapon = null;
        currentWeaponView = null;
        currentWeaponViewLease = default;
        hasWeaponViewLease = false;
    }
}
