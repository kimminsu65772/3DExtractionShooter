using UnityEngine;
using ExtractionShooter.DataPipeline;

// 원작 모델의 외형과 우리 탄환·체력 코드를 연결하는 격리된 실험이다.
[DefaultExecutionOrder(100)]
public sealed class SniperFiringPreview : MonoBehaviour
{
    // 여러 시험 씬을 함께 열어도 조작 패널은 사격 시험 하나만 표시한다.
    public static bool HasActivePreview { get; private set; }
    public Animator animator;
    public Transform weaponVisual;
    public Transform muzzle;
    public HealthComponent target;
    public Projectile projectilePrefab;
    public Camera previewCamera;
    public WeaponDefinition definition;
    [Min(0.1f)] public float shotInterval = 1.2f;
    [Min(0f)] public float recoilDistance = 0.12f;
    [Min(0.01f)] public float recoilReturnSpeed = 1f;
    public bool autoFire = true;

    private readonly Projectile[] projectiles = new Projectile[4];
    private WeaponInstance weapon;
    private AmmoDefinition ammo;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private float recoil;
    private float nextShot;
    private int fired;
    private int hits;
    private bool aiming = true;
    private Camera[] mutedCameras;
    private bool[] cameraWasEnabled;
    private bool queuedShot;

    private void Start()
    {
        // 공통 애니메이션 시험이 열려 있으면 별도 사격 시험을 동시에 실행하지 않는다.
        if (CommonCharacterAnimationPreview.IsActive || DirectClipWeaponPreview.IsActive) { enabled = false; return; }
        HasActivePreview = true;
        ammo = new AmmoDefinition { ammoId = 622, displayName = "Preview ammo", ammoType = definition.ammoType, damageMultiplier = 1f, maxStackCount = 99 };
        // 실제 소유 객체의 탄창을 재사용한다. 테스트 ID는 원본 CSV에 등록하지 않는다.
        weapon = new WeaponInstance(new RuntimeWeapon(definition, 0, 0, 0, 0, 0));
        weapon.AmmoState.Load(ammo, 3);
        restPosition = weaponVisual.localPosition;
        restRotation = weaponVisual.localRotation;
        target.Initialize(100f);
        target.OnDamageTaken += HandleDamage;
        animator.Play("Aim", 0, 0f);
        nextShot = Time.time + 2f;
        for (int i = 0; i < projectiles.Length; i++)
        {
            // 발사 때 생성 비용을 내지 않도록 고정 개수의 실제 Projectile을 미리 준비한다.
            projectiles[i] = Instantiate(projectilePrefab);
            projectiles[i].gameObject.name = "Preview Projectile " + i;
            projectiles[i].gameObject.SetActive(false);
        }
        // 추가로 열려 있는 씬의 카메라는 실행 중에만 끄고 종료 시 원래 상태로 되돌린다.
        mutedCameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        cameraWasEnabled = new bool[mutedCameras.Length];
        for (int i = 0; i < mutedCameras.Length; i++)
        {
            cameraWasEnabled[i] = mutedCameras[i].enabled;
            if (mutedCameras[i] != previewCamera) mutedCameras[i].enabled = false;
        }
        Debug.Log("SniperFiring ready: real Projectile + WeaponInstance + HealthComponent; isolated test definitions.");
    }

    private void LateUpdate()
    {
        if (weapon == null) return;
        recoil = Mathf.MoveTowards(recoil, 0f, recoilReturnSpeed * Time.deltaTime);
        // 손의 애니메이션을 유지하고 총구의 전방 축만 표적 방향에 맞춘다.
        weaponVisual.localPosition = restPosition;
        weaponVisual.localRotation = restRotation;
        if (aiming)
        {
            Vector3 direction = target.transform.position - muzzle.position;
            if (direction.sqrMagnitude > 0.0001f)
                weaponVisual.rotation = Quaternion.FromToRotation(muzzle.forward, direction.normalized) * weaponVisual.rotation;
            weaponVisual.position -= muzzle.forward * recoil;
        }
        if (queuedShot || (autoFire && aiming && fired < 3 && Time.time >= nextShot))
        {
            queuedShot = false;
            TryFire();
        }
    }

    public bool TryFire()
    {
        if (weapon == null || !aiming || !weapon.AmmoState.CanFire || Time.time < nextShot) return false;
        Projectile available = null;
        for (int i = 0; i < projectiles.Length; i++)
            if (!projectiles[i].gameObject.activeSelf) { available = projectiles[i]; break; }
        if (available == null) return false;
        Vector3 offset = target.transform.position - muzzle.position;
        if (offset.sqrMagnitude < 0.0001f) return false;
        available.ResetForPool();
        available.transform.rotation = Quaternion.LookRotation(offset.normalized);
        available.Initialize(definition, ammo, muzzle.position, offset.normalized, offset.magnitude, gameObject, ~0, DamageStatSnapshot.Identity);
        // 생성·초기화에 성공한 뒤 탄약을 소비하고 활성화한다.
        if (!weapon.AmmoState.TryConsumeRound(out _)) return false;
        available.gameObject.SetActive(true);
        recoil = recoilDistance;
        nextShot = Time.time + Mathf.Max(shotInterval, 1f / definition.fireRate);
        fired++;
        Debug.Log($"SniperFiring shot={fired}; rounds={weapon.AmmoState.LoadedAmmoCount}; start={muzzle.position}; direction={offset.normalized}");
        return true;
    }

    private void HandleDamage(DamageInfo damage)
    {
        if (damage.attacker != gameObject) return;
        hits++;
        Debug.Log($"SniperFiring hit={hits}; damage={damage.appliedDamage}; health={target.CurrentHealth}; weapon={damage.weaponId}; ammo={damage.ammoId}");
        if (hits == 3)
            Debug.Log($"SniperFiring result: fired={fired}; hits={hits}; rounds={weapon.AmmoState.LoadedAmmoCount}; hp={target.CurrentHealth}; animatorAim={animator.GetCurrentAnimatorStateInfo(0).IsName("Aim")}");
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(15, 15, 330, 260), GUI.skin.box);
        GUILayout.Label("Sniper / real project projectile");
        GUILayout.Label($"Shots {fired} / Hits {hits} / HP {(target != null ? target.CurrentHealth : 0):F0}");
        GUILayout.Label("Rounds: " + (weapon != null ? weapon.AmmoState.LoadedAmmoCount : 0));
        if (GUILayout.Button("Fire one shot")) queuedShot = true;
        if (GUILayout.Button("Reset three shots") && weapon != null)
        {
            // 비행 중인 탄환이 남아 있는 동안에는 초기화하지 않는다.
            bool active = false;
            foreach (var p in projectiles) active |= p.gameObject.activeSelf;
            if (!active) { target.Initialize(100f); weapon.AmmoState.Load(ammo, weapon.AmmoState.RemainingCapacity); fired = hits = 0; nextShot = Time.time + 1f; }
        }
        GUILayout.Space(8f);
        if (GUILayout.Button("Walk pose"))
        {
            aiming = false;
            animator.Play("Walk", 0, 0f);
        }
        if (GUILayout.Button("Aim pose"))
        {
            aiming = true;
            animator.Play("Aim", 0, 0f);
        }
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        HasActivePreview = false;
        if (target != null) target.OnDamageTaken -= HandleDamage;
        foreach (var p in projectiles) if (p != null) Destroy(p.gameObject);
        if (mutedCameras == null) return;
        for (int i = 0; i < mutedCameras.Length; i++)
            if (mutedCameras[i] != null) mutedCameras[i].enabled = cameraWasEnabled[i];
    }
}
