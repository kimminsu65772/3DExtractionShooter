using UnityEngine;

public class PlayerAttackController : MonoBehaviour
{
    [Header("참조 설정")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private PlayerAimController aimController;
    [SerializeField] private PlayerWeaponHolder weaponHolder;
    [SerializeField] private PlayerRecoilController recoilController;

    [Header("투사체 설정")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform projectileParent;
    [SerializeField] private LayerMask projectileHitMask;

    [Header("디버그")]
    [SerializeField] private bool showDebugLog = true;

    private float nextFireTime;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (inputHandler == null)
            return;

        if (!inputHandler.IsAttackHeld)
            return;

        TryFire();
    }

    private void TryFire()
    {
        if (!CanFire())
            return;

        WeaponDefinition weapon = weaponHolder.CurrentWeapon;

        if (Time.time < nextFireTime)
            return;

        aimController.RefreshAim();

        if (!aimController.HasValidAimDirection)
        {
            LogWarning("유효한 조준 방향이 없어 발사를 취소합니다.");
            return;
        }

        float fireInterval = 1f / weapon.fireRate;

        nextFireTime = Time.time + fireInterval;

        Transform muzzleTransform = weaponHolder.MuzzleTransform;
        Vector3 fireTargetPoint = aimController.AimWorldPosition;
        Vector3 fireVector = fireTargetPoint - muzzleTransform.position;

        if (fireVector.sqrMagnitude < 0.0001f)
        {
            LogWarning("발사 방향이 너무 짧습니다.");
            return;
        }

        Vector3 fireDirection = fireVector.normalized;
        float fireDistance = fireVector.magnitude;

        if (recoilController != null)
        {
            bool isMoving = inputHandler.MoveInput.sqrMagnitude > 0.0001f;

            fireDirection = recoilController.GetFireDirection(
                fireDirection,
                weapon,
                inputHandler.IsAimHeld,
                isMoving
            );

            recoilController.AddRecoil(weapon);
        }

        Projectile projectile = Instantiate(
            projectilePrefab,
            muzzleTransform.position,
            Quaternion.LookRotation(fireDirection, Vector3.up),
            projectileParent
        );

        projectile.Initialize(
            weapon,
            muzzleTransform.position,
            fireDirection,
            fireDistance,
            gameObject,
            projectileHitMask
        );
    }

    private bool CanFire()
    {
        ResolveReferences();

        if (aimController == null)
        {
            LogWarning("PlayerAimController 참조가 없습니다.");
            return false;
        }

        if (weaponHolder == null)
        {
            LogWarning("PlayerWeaponHolder 참조가 없습니다.");
            return false;
        }

        if (!weaponHolder.HasWeapon)
        {
            LogWarning("발사 가능한 무기 데이터가 없습니다.");
            return false;
        }

        if (!weaponHolder.HasMuzzle)
        {
            LogWarning("무기 총구 위치가 설정되지 않았습니다.");
            return false;
        }

        if (projectilePrefab == null)
        {
            LogWarning("Projectile Prefab이 설정되지 않았습니다.");
            return false;
        }

        if (weaponHolder.CurrentWeapon.fireRate <= 0f)
        {
            LogWarning("fireRate가 올바르지 않습니다.");
            return false;
        }

        return true;
    }

    private void ResolveReferences()
    {
        if (inputHandler == null)
        {
            inputHandler = GetComponent<PlayerInputHandler>();
        }

        if (aimController == null)
        {
            aimController = GetComponent<PlayerAimController>();
        }

        if (weaponHolder == null)
        {
            weaponHolder = GetComponent<PlayerWeaponHolder>();
        }

        if (recoilController == null)
        {
            recoilController = GetComponent<PlayerRecoilController>();
        }
    }

    private void LogWarning(string message)
    {
        if (!showDebugLog)
            return;

        Debug.LogWarning(message, this);
    }
}
