using UnityEngine;
using ExtractionShooter.DataPipeline;

[DisallowMultipleComponent]
public class EnemyAttackController : MonoBehaviour
{
    [Header("공격에 필요한 기능")]
    [SerializeField] private EnemyWeaponLoadout weaponLoadout;
    [SerializeField] private CombatDataCompositionRoot dataRoot;

    [Header("투사체 충돌 대상")]
    [SerializeField] private LayerMask projectileHitMask;

    [Header("조준 오차")]
    [SerializeField, Min(0f)] private float spreadMultiplier = 1f;

    [Header("디버그")]
    [SerializeField] private bool showDebugLog = true;

    [Header("점사 설정")]
    [SerializeField, Min(1)] private int minBurstShotCount = 3;
    [SerializeField, Min(1)] private int maxBurstShotCount = 6;
    [SerializeField, Min(0f)] private float minBurstCooldown = 0.8f;
    [SerializeField, Min(0f)] private float maxBurstCooldown = 1.4f;

    private int remainingBurstShots;
    private float nextBurstStartTime;

    private readonly ProjectileSpawnService projectileSpawnService = new ProjectileSpawnService();

    private float nextFireTime;

    private float reloadCompleteTime;

    private float currentSpreadAngle;

    public bool IsReloading { get; private set; }

    public bool IsReady =>
        weaponLoadout != null &&
        weaponLoadout.IsReady &&
        dataRoot != null &&
        dataRoot.isReady &&
        dataRoot.Pools != null &&
        dataRoot.Pools.IsReady;

    public bool IsBurstActive => remainingBurstShots > 0;

    private void Awake()
    {
        if (weaponLoadout == null)
            weaponLoadout = GetComponent<EnemyWeaponLoadout>();

        if(weaponLoadout == null)
        {
            Debug.LogError(
                "EnemyAttackController에 EnemyWeaponLoadout이 필요합니다.",
                this
            );

            enabled = false;
            return;
        }

        if (dataRoot == null)
        {
            Debug.LogError(
                "EnemyAttackController에 CombatDataCompositionRoot 참조가 필요합니다.",
                this
            );

            enabled = false;
        }
    }

    private void OnValidate()
    {
        maxBurstShotCount = Mathf.Max(minBurstShotCount, maxBurstShotCount);
        maxBurstCooldown = Mathf.Max(minBurstCooldown, maxBurstCooldown);
    }

    public void Tick()
    {
        RecoverSpread();

        if (!IsReloading)
            return;

        if (Time.time <= reloadCompleteTime)
            return;

        CompleteReload();
    }

    private bool TryStartReload()
    {
        if (!IsReady || IsReloading)
            return false;

        WeaponAmmoState ammoState = weaponLoadout.AmmoState;

        if (ammoState == null || ammoState.IsFull)
            return false;

        float reloadDuration = weaponLoadout.CurrentWeapon.Definition.reloadDuration;

        if (reloadDuration <= 0f)
        {
            Debug.LogError(
                "적 무기의 reloadDuration은 0보다 커야 합니다.",
                this
            );

            return false;
        }

        IsReloading = true;
        reloadCompleteTime = Time.time + reloadDuration;

        remainingBurstShots = 0;
        nextBurstStartTime = 0f;

        if (showDebugLog)
        {
            Debug.Log(
                $"[{name}] 재장전을 시작합니다. 완료까지 {reloadDuration:F2}초",
                this
            );
        }

        return true;
    }

    private void CompleteReload()
    {
        bool reloadSucceeded = weaponLoadout.TryReloadFull();

        // 재장전 여부와 상관없이 재장전 상태를 초기화한다.
        IsReloading = false;
        reloadCompleteTime = 0f;

        if (reloadSucceeded)
        {
            if (showDebugLog)
            {
                Debug.Log(
                    $"[{name}] 재장전을 완료했습니다. 현재 탄약: {weaponLoadout.AmmoState.LoadedAmmoCount}/{weaponLoadout.AmmoState.Capacity}",
                    this
                );
            }
        }
        else
        {
            Debug.LogWarning(
                $"[{name}] 재장전이 실패했습니다. 현재 탄약: {weaponLoadout.AmmoState.LoadedAmmoCount}/{weaponLoadout.AmmoState.Capacity}",
                this
            );
        }
    }

    public void CancelReload()
    {
        IsReloading = false;
        reloadCompleteTime = 0f;
    }

    public bool TryAttack(Vector3 targetPoint)
    {
        if (!IsReady)
            return false;
        if (IsReloading)
            return false;
        if (!TryStartBurst())
            return false;
        if (Time.time < nextFireTime)
            return false;

        WeaponAmmoState ammoState = weaponLoadout.AmmoState;

        if (ammoState == null || !ammoState.CanFire)
        {
            TryStartReload();
            return false;
        }

        WeaponDefinition definition = weaponLoadout.CurrentWeapon.Definition;

        if (definition.fireRate <= 0f)
        {
            Debug.LogError(
                "적 무기의 fireRate는 0보다 커야 합니다.",
                this
            );
            return false;
        }

        nextFireTime = Time.time + (1f / definition.fireRate);



        return TryFire(targetPoint);
    }

    private bool TryFire(Vector3 targetPoint)
    {
        Transform muzzle = weaponLoadout.MuzzleTransform;

        Vector3 fireVector = targetPoint - muzzle.position;

        if (fireVector.sqrMagnitude < 0.0001f)
            return false;

        float fireDistance = fireVector.magnitude;

        RuntimeWeapon weapon = weaponLoadout.CurrentWeapon;

        WeaponDefinition definition = weapon.Definition;

        float spreadAngle = WeaponSpreadUtility.GetEffectiveSpreadAngle(definition, currentSpreadAngle, spreadMultiplier);

        Vector3 fireDirection = WeaponSpreadUtility.ApplySpread(fireVector / fireDistance, spreadAngle);

        AmmoDefinition ammo = weaponLoadout.CurrentAmmo;

        bool spawned = projectileSpawnService.TrySpawn(
            weapon,
            ammo,
            muzzle.position,
            fireDirection,
            fireDistance,
            gameObject,
            projectileHitMask,
            DamageStatSnapshot.Identity,
            dataRoot.Pools
        );

        if (!spawned)
        {
            Debug.LogError(
                $"[{name}] 투사체를 생성하지 못했습니다. 무기: {weapon.Definition.displayName}, 탄약: {ammo.displayName}",
                this
            );
            return false;
        }

        if (!weaponLoadout.TryConsumeRound(out int consumedAmmoId))
        {
            Debug.LogError(
                $"[{name}] 탄약을 소비하지 못했습니다. 무기: {weapon.Definition.displayName}, 탄약: {ammo.displayName}",
                this
            );

            return false;
        }

        currentSpreadAngle = WeaponSpreadUtility.IncreaseSpread(currentSpreadAngle, definition);

        SoundStimulusHub.Emit(
            muzzle.position,
            weapon.Definition.shotNoiseRadius,
            gameObject
        );

        // 실제 발사가 확정된 뒤 플레이어와 동일한 총구 VFX와 발사음 재생 경로를 사용한다.
        dataRoot.FeedbackService?.PlayMuzzleAndSound(
            weapon,
            muzzle.position,
            muzzle.rotation
        );

        WeaponAmmoState ammoState = weaponLoadout.AmmoState;

        if (showDebugLog)
        {
            Debug.Log(
                $"[{name}] 공격을 수행했습니다. 무기: {weapon.Definition.displayName}, 탄약: {ammo.displayName}, 남은 탄약: {ammoState.LoadedAmmoCount}/{ammoState.Capacity}",
                this
            );
        }

        remainingBurstShots--;

        if (remainingBurstShots <= 0)
            CompleteBurst();

        if (ammoState.IsEmpty)
            TryStartReload();

        return true;
    }

    private void RecoverSpread()
    {
        if (remainingBurstShots > 0 || !IsReady)
            return;

        WeaponDefinition definition = weaponLoadout.CurrentWeapon.Definition;
        currentSpreadAngle = WeaponSpreadUtility.RecoverSpread(currentSpreadAngle, definition, Time.deltaTime);
    }

    private bool TryStartBurst()
    {
        if (remainingBurstShots > 0)
            return true;

        if (Time.time < nextBurstStartTime)
            return false;

        remainingBurstShots = Random.Range(minBurstShotCount, maxBurstShotCount + 1);

        return true;
    }

    private void CompleteBurst()
    {
        remainingBurstShots = 0;
        nextBurstStartTime = Time.time + Random.Range(minBurstCooldown, maxBurstCooldown);
    }

}
