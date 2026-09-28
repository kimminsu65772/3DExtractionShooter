using ExtractionShooter.DataPipeline;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerReloadController : MonoBehaviour
{
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerWeaponHolder weaponHolder;
    [SerializeField] private PlayerAmmoStorage ammoStorage;
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private CombatDataCompositionRoot dataRoot;
    [SerializeField] private PlayerInputHandler _inputHandler;
    [SerializeField] private PlayerAnimationController animationController;

    private WeaponInstance reloadingWeapon;
    private AmmoDefinition reloadingAmmo;
    private float reloadCompleteTime;

    public bool IsReloading => reloadingWeapon != null;

    private void Awake()
    {
        if (_inputHandler == null) _inputHandler = GetComponent<PlayerInputHandler>();
        if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
        if (weaponHolder == null) weaponHolder = GetComponent<PlayerWeaponHolder>();
        if (ammoStorage == null) ammoStorage = GetComponent<PlayerAmmoStorage>();
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
        if (IsReloading 
            || stateMachine == null 
            || stateMachine.CurrentState != PlayerActionState.Free 
            || weaponHolder == null 
            || ammoStorage == null 
            || dataRoot == null 
            || !dataRoot.isReady)
            return false;

        WeaponInstance weapon = weaponHolder.CurrentWeaponInstance;

        if (weapon == null || weapon.AmmoState.IsFull || !TryResolveReloadAmmo(weapon, out AmmoDefinition ammo))
            return false;

        float reloadDuration = weapon.Definition.reloadDuration;

        if (ammoStorage.GetCount(ammo.ammoId) <= 0 || !stateMachine.TryChangeState(PlayerActionState.Reload))
            return false;

        animationController.SetReloadAnimationSpeed(reloadDuration);

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

    private void HandleDeath(DamageInfo damageInfo)
    {
        CancelReload();
    }

    private bool TryResolveReloadAmmo(WeaponInstance weapon, out AmmoDefinition ammo)
    {
        ammo = null;
        AmmoCatalog catalog = dataRoot.AmmoCatalog;
        if (catalog == null) return false;

        int loadedAmmoId = weapon.AmmoState.LoadedAmmoId;
        if (loadedAmmoId > 0)
            return catalog.TryGet(loadedAmmoId, out ammo) && ammoStorage.GetCount(loadedAmmoId) > 0;

        var compatibleAmmo = catalog.GetByType(weapon.AmmoState.CompatibleAmmoType);
        int greatestRemainingCount = 0;

        // 현재 PlayerAmmoStorage에는 인벤토리 슬롯 순서가 없으므로 보유량 동률이면 CSV 등록 순서를 유지한다.
        for (int i = 0; i < compatibleAmmo.Count; i++)
        {
            AmmoDefinition candidate = compatibleAmmo[i];
            int remainingCount = ammoStorage.GetCount(candidate.ammoId);
            if (remainingCount <= greatestRemainingCount)
                continue;

            greatestRemainingCount = remainingCount;
            ammo = candidate;
        }

        return ammo != null;
    }

    private void CompleteReload()
    {
        WeaponInstance weapon = reloadingWeapon;
        AmmoDefinition ammo = reloadingAmmo;
        reloadingWeapon = null;
        reloadingAmmo = null;
        reloadCompleteTime = 0f;

        if (weapon != null && weapon == weaponHolder.CurrentWeaponInstance && ammo != null)
        {
            int transferAmount = Mathf.Min(weapon.AmmoState.RemainingCapacity, ammoStorage.GetCount(ammo.ammoId));
            if (transferAmount > 0 && ammoStorage.TryRemove(ammo.ammoId, transferAmount))
            {
                int loadedAmount = weapon.AmmoState.Load(ammo, transferAmount);
                if (loadedAmount < transferAmount)
                    ammoStorage.TryAdd(ammo.ammoId, transferAmount - loadedAmount);
            }
        }

        if (stateMachine != null && stateMachine.CurrentState == PlayerActionState.Reload)
            stateMachine.TryChangeState(PlayerActionState.Free);
    }
}
