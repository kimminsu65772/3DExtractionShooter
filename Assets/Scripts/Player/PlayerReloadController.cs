using ExtractionShooter.DataPipeline;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerReloadController : MonoBehaviour
{
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerWeaponHolder weaponHolder;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private CombatDataCompositionRoot dataRoot;
    [SerializeField] private PlayerInputHandler _inputHandler;
    [SerializeField] private PlayerAnimationController animationController;

    private WeaponInstance reloadingWeapon;
    private AmmoDefinition reloadingAmmo;
    private float reloadCompleteTime;
    private PlayerItemUseController itemUse;
    private IAmmoStorage AmmoStorage => inventory != null ? inventory.AmmoStorage : null;

    public bool IsReloading => reloadingWeapon != null;

    private void Awake()
    {
        itemUse = GetComponent<PlayerItemUseController>();
        if (_inputHandler == null) _inputHandler = GetComponent<PlayerInputHandler>();
        if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
        if (weaponHolder == null) weaponHolder = GetComponent<PlayerWeaponHolder>();
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (healthComponent == null) healthComponent = GetComponent<HealthComponent>();
        if (animationController == null) animationController = GetComponent<PlayerAnimationController>();
        if (dataRoot == null && weaponHolder != null) dataRoot = weaponHolder.DataRoot;
    }

    private void OnEnable()
    {
        if (healthComponent != null)
            healthComponent.OnDeath += HandleDeath;
    }

    private void Update()
    {
        if (_inputHandler != null && _inputHandler.ConsumeReloadInput())
            TryStartReload();

        if (IsReloading && Time.time >= reloadCompleteTime)
            CompleteReload();
    }

    private void OnDisable()
    {
        if (healthComponent != null)
            healthComponent.OnDeath -= HandleDeath;

        CancelReload();
    }

    public bool TryStartReload()
    {
        if (itemUse != null && itemUse.IsUsing) return false;
        if (IsReloading 
            || stateMachine == null 
            || stateMachine.CurrentState != PlayerActionState.Free 
            || weaponHolder == null 
            || AmmoStorage == null 
            || dataRoot == null 
            || !dataRoot.isReady)
            return false;

        WeaponInstance weapon = weaponHolder.CurrentWeaponInstance;

        if (weapon == null || weapon.AmmoState.IsFull || !TryResolveReloadAmmo(weapon, out AmmoDefinition ammo))
            return false;

        float reloadDuration = weapon.Definition.reloadDuration;

        if (AmmoStorage.GetCount(ammo.ammoId) <= 0 || !stateMachine.TryChangeState(PlayerActionState.Reload))
            return false;

        animationController?.SetReloadAnimationSpeed(reloadDuration);

        reloadingWeapon = weapon;
        reloadingAmmo = ammo;
        reloadCompleteTime = Time.time + weapon.Definition.reloadDuration;
        return true;
    }

    public void CancelReload()
    {
        reloadingWeapon = null;
        reloadingAmmo = null;
        reloadCompleteTime = 0f;

        if (stateMachine != null && stateMachine.CurrentState == PlayerActionState.Reload)
            stateMachine.TryChangeState(PlayerActionState.Free);
    }

    // 한 발씩 장전하는 총에 탄약이 있다면 공격 요청이 장전을 중단할 수 있게 한다.
    public bool TryInterruptForFire()
    {
        if (!IsReloading || reloadingWeapon.Definition.reloadMode != WeaponReloadMode.SingleRound || !reloadingWeapon.AmmoState.CanFire) return false;
        CancelReload();
        return true;
    }

    private void HandleDeath(DamageInfo damageInfo)
    {
        CancelReload();
    }

    private bool TryResolveReloadAmmo(WeaponInstance weapon, out AmmoDefinition ammo)
    {
        ammo = null;
        AmmoCatalog catalog = dataRoot.AmmoCatalog;
        if (catalog == null) return false;

        if (AmmoStorage == null) return false;

        int loadedAmmoId = weapon.AmmoState.LoadedAmmoId;
        if (loadedAmmoId > 0)
            return catalog.TryGet(loadedAmmoId, out ammo) && AmmoStorage.GetCount(loadedAmmoId) > 0;

        var compatibleAmmo = catalog.GetByType(weapon.AmmoState.CompatibleAmmoType);
        int greatestRemainingCount = 0;

        // IAmmoStorage는 슬롯 순서를 제공하지 않으므로 보유량 동률이면 현재 CSV 등록 순서를 유지한다.
        for (int i = 0; i < compatibleAmmo.Count; i++)
        {
            AmmoDefinition candidate = compatibleAmmo[i];
            int remainingCount = AmmoStorage.GetCount(candidate.ammoId);
            if (remainingCount <= greatestRemainingCount)
                continue;

            greatestRemainingCount = remainingCount;
            ammo = candidate;
        }

        return ammo != null;
    }

    private void CompleteReload()
    {
        // 외부에서 회피나 사망 상태로 바뀐 경우 완료 시간이 지나도 탄약을 이동하지 않는다.
        if (stateMachine == null || stateMachine.CurrentState != PlayerActionState.Reload)
        {
            CancelReload();
            return;
        }
        WeaponInstance weapon = reloadingWeapon;
        AmmoDefinition ammo = reloadingAmmo;
        IAmmoStorage ammoStorage = AmmoStorage;
        bool singleRound = weapon != null && weapon.Definition.reloadMode == WeaponReloadMode.SingleRound;

        if (weapon != null && weaponHolder != null && weapon == weaponHolder.CurrentWeaponInstance 
            && ammo != null && ammoStorage != null)
        {
            int transferAmount = Mathf.Min(weapon.AmmoState.RemainingCapacity, ammoStorage.GetCount(ammo.ammoId));
            if (singleRound) transferAmount = Mathf.Min(transferAmount, 1);
            int committedAmount = 0;
            if (transferAmount > 0)
            {
                // 탄창이 받아들인 수량만 보관소에서 소비하여 별도의 반환을 없앤다.
                int loadedAmount = weapon.AmmoState.Load(ammo, transferAmount);
                if (loadedAmount > 0 && !ammoStorage.TryRemove(ammo.ammoId, loadedAmount))
                {
                    // 제거 실패 시 이번에 장전한 수량만 되돌려 탄약 복제를 막는다.
                    for (int i = 0; i < loadedAmount; i++)
                        weapon.AmmoState.TryConsumeRound(out _);
                }
                else committedAmount = loadedAmount;
            }

            // 한 발이 실제로 옮겨졌고 여분이 남았으면 다음 한 발의 시간을 새로 시작한다.
            // 프레임이 길어져도 밀린 시간을 한꺼번에 처리해 즉시 여러 발을 채우지 않는다.
            if (singleRound && committedAmount > 0 && !weapon.AmmoState.IsFull && ammoStorage.GetCount(ammo.ammoId) > 0)
            {
                reloadCompleteTime = Time.time + weapon.Definition.reloadDuration;
                return;
            }
        }
        CancelReload();
    }
}
