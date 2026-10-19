using System.Collections.Generic;
using UnityEngine;
using ExtractionShooter.DataPipeline;

[DisallowMultipleComponent]
public class EnemyWeaponLoadout : MonoBehaviour
{
    [Header("전투 데이터")]
    [SerializeField] private CombatDataCompositionRoot dataRoot;
    [SerializeField] private int initialWeaponId;
    [SerializeField] private int initialAmmoId;

    [Header("무기 외형 장착 위치")]
    [SerializeField] private Transform weaponMount;

    // 적 한 명이 실제로 사용하는 무기와 무기의 탄창 상태
    private WeaponInstance currentWeaponInstance;

    // 적이 계속 재장전해서 사용할 탄약 데이터
    private AmmoDefinition currentAmmo;

    // 현재 장착한 무기 외형과 해당 외형의 풀 대여권
    private WeaponView currentWeaponView;
    private PoolLease currentWeaponViewLease;
    private bool hasWeaponViewLease;
    private readonly List<Collider> deathDisabledWeaponColliders = new();
    private VisibilityTarget visibilityTarget;

    public WeaponInstance CurrentWeaponInstance => currentWeaponInstance;
    public CombatDataCompositionRoot DataRoot => dataRoot;
    public RuntimeWeapon CurrentWeapon => currentWeaponInstance?.RuntimeWeapon;
    public WeaponAmmoState AmmoState => currentWeaponInstance?.AmmoState;
    public AmmoDefinition CurrentAmmo => currentAmmo;
    public WeaponView CurrentWeaponView => currentWeaponView;
    public Transform MuzzleTransform => currentWeaponView != null ? currentWeaponView.MuzzleTransform : null;

    public bool IsReady =>
        currentWeaponInstance != null &&
        currentAmmo != null &&
        currentWeaponView != null &&
        currentWeaponView.HasMuzzle;

    // 부트스트랩이나 스포너가 최초 활성화 전에 데이터 루트를 전달한다.
    public void SetDataRoot(CombatDataCompositionRoot root)
    {
        dataRoot = root;
    }

    // 비활성 신규 스폰에만 사용한다. 실제 장착은 기존 OnEnable 초기화가 담당한다.
    internal void ConfigureSpawn(int weaponId, int ammoId)
    {
        initialWeaponId = weaponId;
        initialAmmoId = ammoId;
    }

    private void Awake()
    {
        visibilityTarget = GetComponent<VisibilityTarget>();
    }

    private void OnEnable()
    {
        if (dataRoot == null)
        {
            Debug.LogError("EnemyWeaponLoadout에 CombatDataCompositionRoot 참조가 필요합니다.", this);
            return;
        }

        // CSV와 Addressables 데이터 준비가 끝나는 시점을 기다린다.
        dataRoot.OnInitialized += HandleDataInitialized;

        // Loadout보다 데이터 파이프라인이 먼저 준비된 상황도 처리한다.
        if (dataRoot.isReady)
            TryInitialize(initialWeaponId, initialAmmoId);
    }

    private void OnDisable()
    {
        if (dataRoot != null)
            dataRoot.OnInitialized -= HandleDataInitialized;

        Unequip();
    }

    private void HandleDataInitialized()
    {
        if (!TryInitialize(initialWeaponId, initialAmmoId))
            Debug.LogError("적의 초기 무기와 탄약을 구성하지 못했습니다.", this);
    }

    public bool TryInitialize(int weaponId, int ammoId)
    {
        // 아직 CSV 데이터와 런타임 카탈로그가 준비되지 않았다면 초기화 실패
        if (dataRoot == null || !dataRoot.isReady)
            return false;

        WeaponCatalog weaponCatalog = dataRoot.WeaponCatalog;
        AmmoCatalog ammoCatalog = dataRoot.AmmoCatalog;

        if (weaponCatalog == null || ammoCatalog == null)
            return false;

        if (!weaponCatalog.TryGet(weaponId, out RuntimeWeapon runtimeWeapon))
        {
            Debug.LogError($"적 무기 ID {weaponId}를 WeaponCatalog에서 찾을 수 없습니다.", this);
            return false;
        }

        if (!ammoCatalog.TryGet(ammoId, out AmmoDefinition ammoDefinition))
        {
            Debug.LogError($"적 탄약 ID {ammoId}를 AmmoCatalog에서 찾을 수 없습니다.", this);
            return false;
        }

        if (runtimeWeapon.Definition.ammoType != ammoDefinition.ammoType)
        {
            Debug.LogError($"적 무기 ID {weaponId}의 탄약 타입과 적 탄약 ID {ammoId}의 타입이 일치하지 않습니다.", this);
            return false;
        }

        WeaponInstance candidateWeapon = new WeaponInstance(runtimeWeapon);

        // 적 무기 인스턴스의 탄창 상태를 가득 채운다.
        int loadedAmmoCount = candidateWeapon.AmmoState.Load(ammoDefinition, candidateWeapon.AmmoState.Capacity);

        if (loadedAmmoCount != candidateWeapon.AmmoState.Capacity)
        {
            Debug.LogWarning($"적 무기 ID {weaponId}의 탄창을 가득 채우지 못했습니다. (로드된 탄약: {loadedAmmoCount}, 탄창 용량: {candidateWeapon.AmmoState.Capacity})", this);
            return false;
        }

        if (!TryRentWeaponView(runtimeWeapon, out WeaponView newWeaponView, out PoolLease newWeaponViewLease))
            return false;

        // 새로운 무기 구성이 모두 준비된 뒤에만 이전 무기를 반환한다.
        Unequip();

        currentWeaponInstance = candidateWeapon;
        currentAmmo = ammoDefinition;
        currentWeaponView = newWeaponView;
        currentWeaponViewLease = newWeaponViewLease;
        hasWeaponViewLease = true;

        // Awake 이후 추가된 무기도 현재 FOV 표시 상태를 즉시 적용받도록 등록한다.
        if (visibilityTarget != null)
            visibilityTarget.RefreshRenderers();

        return true;
    }

    private bool TryRentWeaponView(RuntimeWeapon runtimeWeapon, out WeaponView weaponView, out PoolLease lease)
    {
        weaponView = null;
        lease = default;

        if (weaponMount == null)
        {
            Debug.LogError("EnemyWeaponLoadout에 WeaponMount 참조가 필요합니다.", this);
            return false;
        }

        if (runtimeWeapon.WeaponViewId <= 0)
        {
            Debug.LogError($"적 무기 ID {runtimeWeapon.WeaponId}의 WeaponViewId가 유효하지 않습니다.", this);
            return false;
        }

        if (dataRoot.Pools == null || !dataRoot.Pools.IsReady)
        {
            Debug.LogError("무기 외형을 대여할 오브젝트 풀이 준비되지 않았습니다.", this);
            return false;
        }

        if (!dataRoot.Pools.TryRent(runtimeWeapon.WeaponViewId, out lease))
        {
            Debug.LogError($"적 무기 ID {runtimeWeapon.WeaponId}의 무기 외형을 대여하지 못했습니다.", this);
            return false;
        }

        GameObject viewInstance = lease.Instance;
        weaponView = viewInstance.GetComponentInChildren<WeaponView>(includeInactive: true);

        if (weaponView == null || !weaponView.HasMuzzle)
        {
            Debug.LogError($"적 무기 ID {runtimeWeapon.WeaponId}의 WeaponView 또는 MuzzleTransform이 유효하지 않습니다.", this);
            dataRoot.Pools.TryReturn(lease);
            weaponView = null;
            lease = default;
            return false;
        }

        // 같은 풀의 무기를 사용하는 적도 동일한 크기 보정을 적용한다.
        if (!weaponView.TryAttachTo(weaponMount))
        {
            dataRoot.Pools.TryReturn(lease);
            weaponView = null;
            lease = default;
            return false;
        }

        dataRoot.Pools.Activate(lease);
        return true;
    }

    private void Unequip()
    {
        // 사망 연출 중 끈 충돌체를 반환 전에 복구해 다음 대여자에게 죽은 적의 설정을 넘기지 않는다.
        foreach (Collider weaponCollider in deathDisabledWeaponColliders)
            if (weaponCollider != null) weaponCollider.enabled = true;
        deathDisabledWeaponColliders.Clear();

        if (hasWeaponViewLease && dataRoot?.Pools != null)
        {
            // 적의 숨김 상태를 풀에 넘기지 않는다. 다음 소유자의 표시 정책은 장착 후 적용된다.
            if (currentWeaponViewLease.Instance != null)
                foreach (Renderer renderer in currentWeaponViewLease.Instance.GetComponentsInChildren<Renderer>(true))
                    renderer.forceRenderingOff = false;
            dataRoot.Pools.TryReturn(currentWeaponViewLease);
        }

        currentWeaponInstance = null;
        currentAmmo = null;
        currentWeaponView = null;
        currentWeaponViewLease = default;
        hasWeaponViewLease = false;

        // 반환된 총을 계속 제어하면 다른 적이나 플레이어의 총까지 숨길 수 있다.
        if (visibilityTarget != null)
            visibilityTarget.RefreshRenderers();
    }

    public bool TryConsumeRound(out int consumedAmmoId)
    {
        consumedAmmoId = 0;
        if (!IsReady)
            return false;
        
        return currentWeaponInstance.AmmoState.TryConsumeRound(out consumedAmmoId);
    }

    public bool TryReloadFull()
    {
        if (!IsReady)
            return false;

        WeaponAmmoState ammoState = currentWeaponInstance.AmmoState;

        if (ammoState.IsFull)
            return false;

        // 현재의 탄약을 나머지 용량만큼 채운다.
        int loadedAmmoCount = ammoState.Load(currentAmmo, ammoState.RemainingCapacity);

        return loadedAmmoCount > 0;
    }

    public void DisableWeaponCollisionForDeath()
    {
        if (!hasWeaponViewLease || currentWeaponViewLease.Instance == null) return;
        // 원래 켜져 있던 충돌체만 기억한다. 이미 꺼져 있던 충돌체는 반환할 때도 켜지 않는다.
        foreach (Collider weaponCollider in currentWeaponViewLease.Instance.GetComponentsInChildren<Collider>(true))
        {
            if (!weaponCollider.enabled) continue;
            deathDisabledWeaponColliders.Add(weaponCollider);
            weaponCollider.enabled = false;
        }
    }

    // 예비 탄약이 무한한 적도 샷건의 한 발 장전 규칙은 동일하게 적용한다.
    public bool TryReloadRound()
    {
        return IsReady && currentWeaponInstance.AmmoState.Load(currentAmmo, 1) == 1;
    }
}
