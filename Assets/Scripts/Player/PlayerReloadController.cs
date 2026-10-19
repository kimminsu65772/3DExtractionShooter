using System;
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
    private float reloadStartTime;
    private PlayerItemUseController itemUse;
    private bool exchangeRequested;
    private IAmmoStorage AmmoStorage => inventory != null ? inventory.AmmoStorage : null;

    public bool IsReloading => reloadingWeapon != null;
    public bool IsReloadingWeapon(WeaponInstance weapon) => IsReloading && ReferenceEquals(reloadingWeapon, weapon);
    public float ReloadRemainingTime => IsReloading ? Mathf.Max(0f, reloadCompleteTime - Time.time) : 0f;
    // 한 발씩 장전하는 무기는 현재 한 발의 시작/완료 시간을 기준으로 반복한다.
    public float ReloadProgress => !IsReloading ? 0f : reloadCompleteTime <= reloadStartTime ? 1f :
        Mathf.Clamp01((Time.time - reloadStartTime) / (reloadCompleteTime - reloadStartTime));
    public event Action<bool> OnReloadStateChanged;
    public event Action<float> OnReloadProgressChanged;

    public void SetDataRoot(CombatDataCompositionRoot dataRoot)
    {
        this.dataRoot = dataRoot;
    }

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

        if (!IsReloading) return;
        OnReloadProgressChanged?.Invoke(ReloadProgress);
        if (Time.time >= reloadCompleteTime)
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
        return TryStartReload(0);
    }

    // 0은 기존 보충 규칙, 양수는 T 선택 화면에서 확정한 탄약이다.
    public bool TryStartReload(int selectedAmmoId)
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

        if (weapon == null) return false;
        AmmoDefinition ammo;
        if (selectedAmmoId > 0)
        {
            if (dataRoot.AmmoCatalog == null || !dataRoot.AmmoCatalog.TryGet(selectedAmmoId, out ammo) ||
                ammo.ammoType != weapon.AmmoState.CompatibleAmmoType) return false;
        }
        else if (!TryResolveReloadAmmo(weapon, out ammo)) return false;
        bool exchange = !weapon.AmmoState.IsEmpty && weapon.AmmoState.LoadedAmmoId != ammo.ammoId;
        if (weapon.AmmoState.IsFull && !exchange) return false;

        float reloadDuration = weapon.Definition.reloadDuration;

        if (AmmoStorage.GetCount(ammo.ammoId) <= 0 || !stateMachine.TryChangeState(PlayerActionState.Reload))
            return false;

        animationController?.SetReloadAnimationSpeed(reloadDuration);

        reloadingWeapon = weapon;
        reloadingAmmo = ammo;
        exchangeRequested = exchange;
        BeginReloadCycle(reloadDuration);
        OnReloadStateChanged?.Invoke(true);
        OnReloadProgressChanged?.Invoke(0f);
        return true;
    }

    private void BeginReloadCycle(float duration)
    {
        reloadStartTime = Time.time;
        reloadCompleteTime = reloadStartTime + duration;
    }

    public void CancelReload()
    {
        bool wasReloading = IsReloading;
        reloadingWeapon = null;
        reloadingAmmo = null;
        exchangeRequested = false;
        reloadCompleteTime = 0f;
        reloadStartTime = 0f;

        if (stateMachine != null && stateMachine.CurrentState == PlayerActionState.Reload)
            stateMachine.TryChangeState(PlayerActionState.Free);
        if (wasReloading)
        {
            OnReloadStateChanged?.Invoke(false);
            OnReloadProgressChanged?.Invoke(0f);
        }
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
        int firstSlot = int.MaxValue;

        // 보유량 동률은 실제 가방의 앞 슬롯을 우선한다. 장전 요청 때만 조회한다.
        for (int i = 0; i < compatibleAmmo.Count; i++)
        {
            AmmoDefinition candidate = compatibleAmmo[i];
            int remainingCount = AmmoStorage.GetCount(candidate.ammoId);
            int candidateSlot = FindFirstAmmoSlot(candidate.ammoId);
            if (remainingCount <= 0 || remainingCount < greatestRemainingCount ||
                remainingCount == greatestRemainingCount && candidateSlot >= firstSlot)
                continue;

            greatestRemainingCount = remainingCount;
            firstSlot = candidateSlot;
            ammo = candidate;
        }

        return ammo != null;
    }

    private int FindFirstAmmoSlot(int ammoId)
    {
        InventoryContainer bag = inventory?.InventoryContainer;
        if (bag != null)
            for (int i = 0; i < bag.SlotCount; i++)
                if (bag.GetSlot(i).Item is AmmoStack ammo && ammo.ItemId == ammoId) return i;
        return int.MaxValue;
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
            int committedAmount = 0;
            if (exchangeRequested)
            {
                // 완료 전까지 총·가방을 바꾸지 않는다. 드롭 준비 실패도 기존 상태를 보존한다.
                if (!inventory.TryCompleteAmmoExchange(weapon, ammo.ammoId, out committedAmount))
                {
                    CancelReload();
                    return;
                }
                exchangeRequested = false;
            }
            else
            {
                int transferAmount = Mathf.Min(weapon.AmmoState.RemainingCapacity, ammoStorage.GetCount(ammo.ammoId));
                if (singleRound) transferAmount = Mathf.Min(transferAmount, 1);
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

                // [DATA-01 변경] 탄창·가방의 이동과 소진 슬롯 정리가 모두 끝난 뒤 통지한다.
                if (committedAmount > 0) inventory.RefreshOwnedItemReferences();
            }

            // 한 발이 실제로 옮겨졌고 여분이 남았으면 다음 한 발의 시간을 새로 시작한다.
            // 프레임이 길어져도 밀린 시간을 한꺼번에 처리해 즉시 여러 발을 채우지 않는다.
            if (singleRound && committedAmount > 0 && !weapon.AmmoState.IsFull && ammoStorage.GetCount(ammo.ammoId) > 0)
            {
                BeginReloadCycle(weapon.Definition.reloadDuration);
                OnReloadProgressChanged?.Invoke(0f);
                return;
            }
        }
        CancelReload();
    }
}
