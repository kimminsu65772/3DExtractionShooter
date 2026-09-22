using ExtractionShooter.DataPipeline;
using UnityEngine;

public class PlayerAttackController : MonoBehaviour
{
    [Header("참조 설정")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private PlayerAimController aimController;
    [SerializeField] private PlayerWeaponHolder weaponHolder;
    [SerializeField] private PlayerRecoilController recoilController;
    [SerializeField] private CombatDataCompositionRoot dataRoot;

    [Header("투사체 설정")]
    [SerializeField] private LayerMask projectileHitMask;

    [Header("디버그")]
    [SerializeField] private bool showDebugLog = true;

    private readonly ProjectileSpawnService projectileSpawnService = new ProjectileSpawnService();

    private float nextFireTime;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (recoilController != null && inputHandler != null)
        {
            recoilController.SetRecoveryBlocked(inputHandler.IsAttackHeld);
        }

        if (inputHandler != null && inputHandler.IsAttackHeld)
        {
            TryFire();
        }
    }

    private void TryFire()
    {
        if (!CanFire())
            return;

        if (Time.time < nextFireTime)
            return;

        RuntimeWeapon runtimeWeapon = weaponHolder.CurrentWeapon;

        WeaponDefinition definition = runtimeWeapon.Definition;

        aimController.RefreshAim();

        if (!aimController.HasValidAimDirection)
        {
            LogWarning("Cannot fire: Invalid aim direction.");
            return;
        }

        Transform muzzle = weaponHolder.MuzzleTransform;

        Vector3 fireVector = aimController.AimWorldPosition - muzzle.position;

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

            fireDirection = recoilController.GetFireDirection(fireDirection, definition, inputHandler.IsAimHeld, isMoving);
        }

        bool spawned = projectileSpawnService.TrySpawn(
            runtimeWeapon,
            muzzle.position,
            fireDirection,
            fireDistance,
            gameObject,
            projectileHitMask,
            dataRoot.Pools
            );

        if (!spawned)
        {
            LogWarning("발사 실패: 투사체를 생성할 수 없습니다.");
            return;
        }

        nextFireTime = Time.time + 1f / definition.fireRate;

        if (recoilController != null)
        {
            Vector2 screenDirection = aimController.GetScreenAimDirection(muzzle.position);

            // 다음 발사에 영향을 줄 반동을 미리 계산한다.
            recoilController.AddRecoil(definition, inputHandler.IsAimHeld, screenDirection);
        }
    }

    private bool CanFire()
    {
        ResolveReferences();

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
            recoilController =
                GetComponent<PlayerRecoilController>();
        }
    }

    private void LogWarning(string message)
    {
        if (!showDebugLog)
            return;

        Debug.LogWarning(message, this);
    }
}
