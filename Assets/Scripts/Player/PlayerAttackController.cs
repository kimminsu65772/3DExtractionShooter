using ExtractionShooter.DataPipeline;
using UnityEngine;

public class PlayerAttackController : MonoBehaviour
{
    [Header("참조 설정")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private PlayerAimController aimController;
    [SerializeField] private PlayerWeaponHolder weaponHolder;
    [SerializeField] private PlayerRecoilController recoilController;
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerReloadController reloadController;
    [SerializeField] private CombatDataCompositionRoot dataRoot;


    [Header("투사체 설정")]
    [SerializeField] private LayerMask projectileHitMask;

    [Header("디버그")]
    [SerializeField] private bool showDebugLog = true;

    private readonly ProjectileSpawnService projectileSpawnService = new ProjectileSpawnService();

    private float nextFireTime;

    private bool isFirePending;
    private PlayerItemUseController itemUse;
    // [ACT-03·DATA-01 변경] 기본은 정상 소비다. 로비 부트스트랩만 사격 시 차감을 비활성화한다.
    private bool consumeAmmoOnFire = true;

    public void SetConsumeAmmoOnFire(bool enabled)
    {
        consumeAmmoOnFire = enabled;
    }

    public void SetDataRoot(CombatDataCompositionRoot dataRoot)
    {
        this.dataRoot = dataRoot;
    }

    private void Awake()
    {
        itemUse = GetComponent<PlayerItemUseController>();
        ResolveReferences();
    }

    private void Update()
    {
        // 공격을 누르면 이미 한 발 이상 넣은 샷건의 장전만 중단한다. 빈 탄창은 계속 장전한다.
        if (inputHandler != null && !inputHandler.IsUIInputBlocked && inputHandler.IsAttackHeld && stateMachine != null && stateMachine.CurrentState == PlayerActionState.Reload)
            reloadController?.TryInterruptForFire();
        // 뭔가를 하고 있는 상태에서는 공격을 할 수 없도록 한다.
        bool canAttack = stateMachine != null && stateMachine.CurrentState == PlayerActionState.Free && inputHandler != null && !inputHandler.IsUIInputBlocked && (itemUse == null || !itemUse.IsUsing);

        if (recoilController != null && inputHandler != null)
        {
            // 입력 유지 여부는 탄퍼짐에만 전달한다. 반동 회복 지연은 실제 발사에서 시작한다.
            recoilController.SetSpreadRecoveryBlocked(inputHandler.IsAttackHeld && canAttack);
        }

        // 발사 애니메이션을 요청한 직후 회피가 시작될 수도 있다.
        // 이때 남아 있던 Animation Event가 회피 도중 투사체를 만들지 않도록 예약을 취소한다.
        if (!canAttack)
        {
            isFirePending = false;
            return;
        }

        if (inputHandler != null && inputHandler.IsAttackHeld && animationController)
        {
            TryRequestFire();
        }
    }

    private void TryRequestFire()
    {
        if (isFirePending)
            return;

        if (!CanFire())
            return;

        if (!HasLoadedRound())
        {
            reloadController?.TryStartReload();
            return;
        }

        if (Time.time < nextFireTime)
            return;

        aimController.RefreshAim();

        if (!aimController.HasValidAimDirection)
        {
            LogWarning("[PlayerAttackController]: 발사 방향이 적절하지 않아 발사할 수 없습니다.");
            return;
        }

        WeaponDefinition definition = weaponHolder.CurrentWeapon.Definition;

        // 이제 Animation Event가 들어오기 전까지 추가 발사 요청을 받지 않는다.
        isFirePending = true;

        nextFireTime = Time.time + 1f / definition.fireRate;

        // 총을 발사하기 위한 애니메이션을 재생한다.
        animationController.PlayFireAnimation();
    }

    public void ExecutePendingFire()
    {
        if (!isFirePending)
            return;

        isFirePending = false;

        if (!CanFire())
            return;


        RuntimeWeapon runtimeWeapon = weaponHolder.CurrentWeapon;
        WeaponDefinition definition = runtimeWeapon.Definition;
        WeaponAmmoState ammoState = weaponHolder.CurrentAmmoState;

        if (ammoState == null || !ammoState.CanFire)
        {
            reloadController?.TryStartReload();
            return;
        }

        if (dataRoot.AmmoCatalog == null || !dataRoot.AmmoCatalog.TryGet(ammoState.LoadedAmmoId, out AmmoDefinition ammoDefinition))
        {
            LogWarning($"장전된 AmmoId {ammoState.LoadedAmmoId}를 AmmoCatalog에서 찾을 수 없습니다.");
            return;
        }

        aimController.RefreshAim();

        if (!aimController.HasValidAimDirection)
        {
            LogWarning("Cannot fire: Invalid aim direction.");
            return;
        }

        Transform muzzle = weaponHolder.MuzzleTransform;

        Vector3 fireVector = aimController.AimWorldPosition - muzzle.position;

        if (!aimController.HasAimTarget)
            fireVector.y = 0f;

        if (fireVector.sqrMagnitude < 0.0001f)
        {
            LogWarning("발사 방향이 너무 짧습니다.");
            return;
        }

        Vector3 fireDirection = fireVector.normalized;

        float fireDistance = fireVector.magnitude;

        if (recoilController != null)
        {
            bool isMoving = inputHandler.MoveInput.sqrMagnitude > 0.01f;

            fireDirection = recoilController.GetFireDirection(fireDirection, definition, aimController.IsAdsReady, isMoving);
        }

        bool spawned = projectileSpawnService.TrySpawnShot(
            runtimeWeapon,
            ammoDefinition,
            muzzle.position,
            fireDirection,
            fireDistance,
            gameObject,
            projectileHitMask,
            GetDamageStatSnapshot(),
            dataRoot.Pools
            );

        if (!spawned)
        {
            LogWarning("발사 실패: 투사체를 생성할 수 없습니다.");
            return;
        }

        // [ACT-03·DATA-01 변경] 로비도 위의 장탄 수 검사를 통과해야 하지만, 발사로 차감하지 않는다.
        // 탐험에서는 실제 투사체 생성 성공 뒤 한 발을 소비하고 소유 상태 변경을 통지한다.
        if (consumeAmmoOnFire)
        {
            if (!ammoState.TryConsumeRound(out _))
            {
                Debug.LogError("투사체 생성 후 탄약 소비에 실패했습니다. 탄창 상태를 확인해야 합니다.", this);
                return;
            }

            GetComponent<PlayerInventory>()?.NotifyOwnedItemsChanged();
        }

        SoundStimulusHub.Emit(
            muzzle.position,
            definition.shotNoiseRadius,
            gameObject
        );

        // 실제 발사가 확정된 뒤 총구 VFX와 발사음을 풀에서 대여해 재생한다.
        dataRoot.FeedbackService?.PlayMuzzleAndSound(
            runtimeWeapon,
            muzzle.position,
            muzzle.rotation
        );

        if (showDebugLog)
            Debug.Log($"AmmoId {ammoDefinition.ammoId} 발사. 남은 장탄: {ammoState.LoadedAmmoCount}/{ammoState.Capacity}", this);

        if (recoilController != null)
        {
            Vector2 screenDirection = aimController.GetScreenAimDirection(muzzle.position);

            // 다음 발사에 영향을 줄 반동을 미리 계산한다.
            recoilController.AddRecoil(definition, aimController.IsAdsReady, screenDirection);
        }
    }
    public void OnFireFrame()
    {
        ExecutePendingFire();
    }

    // 애니메이션 이벤트를 기다리던 현재 발사 요청을 취소한다.
    public void CancelPendingFire()
    {
        isFirePending = false;
    }

    private bool CanFire()
    {
        ResolveReferences();
        if (itemUse != null && itemUse.IsUsing) return false;

        // 창을 연 직후 늦게 도착한 발사 애니메이션 이벤트도 차단한다.
        if (inputHandler != null && inputHandler.IsUIInputBlocked) return false;

        if (stateMachine == null || stateMachine.CurrentState != PlayerActionState.Free)
        {
            return false;
        }

        if (inputHandler == null)
        {
            LogWarning(
                "PlayerInputHandler 참조가 없습니다."
            );

            return false;
        }

        if (aimController == null)
        {
            LogWarning(
                "PlayerAimController 참조가 없습니다."
            );

            return false;
        }

        if (weaponHolder == null)
        {
            LogWarning(
                "PlayerWeaponHolder 참조가 없습니다."
            );

            return false;
        }

        if (dataRoot == null ||
            !dataRoot.isReady ||
            dataRoot.Pools == null ||
            !dataRoot.Pools.IsReady)
        {
            LogWarning(
                "전투 데이터 파이프라인이 준비되지 않았습니다."
            );

            return false;
        }

        if (!weaponHolder.HasWeapon)
        {
            LogWarning(
                "장착된 RuntimeWeapon이 없습니다."
            );

            return false;
        }

        if (!weaponHolder.HasMuzzle)
        {
            LogWarning(
                "MuzzleTransform이 없습니다."
            );

            return false;
        }

        WeaponDefinition definition =
            weaponHolder.CurrentWeapon.Definition;

        if (definition.fireRate <= 0f)
        {
            LogWarning(
                "fireRate가 올바르지 않습니다."
            );

            return false;
        }

        if (animationController == null)
        {
            LogWarning(
                "PlayerAnimationController 참조가 없습니다."
            );

            return false;
        }

        return true;
    }

    private void ResolveReferences()
    {
        if (inputHandler == null)
        {
            inputHandler =
                GetComponent<PlayerInputHandler>();
        }

        if (aimController == null)
        {
            aimController =
                GetComponent<PlayerAimController>();
        }

        if (weaponHolder == null)
        {
            weaponHolder =
                GetComponent<PlayerWeaponHolder>();
        }

        if (recoilController == null)
        {
            recoilController = GetComponent<PlayerRecoilController>();
        }

        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }

        if (animationController == null)
        {
            animationController =
                GetComponent<PlayerAnimationController>();
        }

        if (stateMachine == null)
        {
            stateMachine =
                GetComponent<PlayerStateMachine>();
        }

        if (reloadController == null)
        {
            reloadController = GetComponent<PlayerReloadController>();
        }
    }

    private bool HasLoadedRound()
    {
        return weaponHolder != null && weaponHolder.CurrentAmmoState != null && weaponHolder.CurrentAmmoState.CanFire;
    }

    private DamageStatSnapshot GetDamageStatSnapshot()
    {
        if (characterStats == null)
            return DamageStatSnapshot.Identity;

        float outgoingDamageMultiplier = characterStats.GetValue(StatType.OutgoingDamageMultiplier, 1f);
        float headshotDamageMultiplier = characterStats.GetValue(StatType.HeadshotDamageMultiplier, 1f);
        return new DamageStatSnapshot(outgoingDamageMultiplier, headshotDamageMultiplier);
    }

    private void LogWarning(string message)
    {
        if (!showDebugLog)
            return;

        Debug.LogWarning(message, this);
    }
}
