using UnityEngine;
using ExtractionShooter.DataPipeline;

// 애니메이션 합성이 아닌 프로젝트의 실제 탄환 처리를 연결한다.
[DefaultExecutionOrder(220)]
public sealed class DirectClipShotPreview : MonoBehaviour
{
    public WeaponView weaponView;
    public HealthComponent target;
    public Projectile projectilePrefab;
    public WeaponDefinition definition;
    public int Fired { get; private set; }
    public int Hits { get; private set; }
    public int Rounds => weapon == null ? 0 : weapon.AmmoState.LoadedAmmoCount;
    private readonly Projectile[] projectiles = new Projectile[4];
    private WeaponInstance weapon;
    private AmmoDefinition ammo;
    private bool pendingShot;
    private float nextShotTime;

    private void Start()
    {
        ammo = new AmmoDefinition { ammoId = 622, displayName = "Preview ammo", ammoType = definition.ammoType, damageMultiplier = 1, maxStackCount = 99 };
        weapon = new WeaponInstance(new RuntimeWeapon(definition, 0, 0, 0, 0, 0));
        weapon.AmmoState.Load(ammo, 3);
        target.Initialize(100);
        target.OnDamageTaken += HandleDamage;
        for (int i = 0; i < projectiles.Length; i++)
        {
            projectiles[i] = Instantiate(projectilePrefab);
            projectiles[i].gameObject.name = "Direct Clip Projectile " + i;
            projectiles[i].gameObject.SetActive(false);
        }
    }

    public void QueueShot() => pendingShot = true;

    private void LateUpdate()
    {
        // 조준 자세가 Animator에 반영된 뒤 총구 위치와 방향을 읽는다.
        if (!pendingShot) return;
        pendingShot = false;
        if (weapon == null || !weapon.AmmoState.CanFire || Time.time < nextShotTime) return;
        Projectile available = null;
        foreach (var projectile in projectiles)
            if (!projectile.gameObject.activeSelf) { available = projectile; break; }
        if (available == null) return;
        var muzzle = weaponView.MuzzleTransform;
        available.ResetForPool();
        available.transform.rotation = muzzle.rotation;
        // 표적을 향해 방향을 속이지 않고 실제 총구 전방으로 발사한다.
        available.Initialize(definition, ammo, muzzle.position, muzzle.forward, 15f, gameObject, ~0, DamageStatSnapshot.Identity);
        if (!weapon.AmmoState.TryConsumeRound(out _)) return;
        available.gameObject.SetActive(true);
        nextShotTime = Time.time + 1f / definition.fireRate;
        Fired++;
    }

    private void HandleDamage(DamageInfo damage)
    {
        if (damage.attacker == gameObject) Hits++;
    }

    public void ResetTrial()
    {
        // 비행 중인 탄환이 초기화 직후 표적을 다시 맞히는 상황을 차단한다.
        foreach (var projectile in projectiles)
            if (projectile != null && projectile.gameObject.activeSelf) return;
        target.Initialize(100);
        weapon.AmmoState.Load(ammo, weapon.AmmoState.RemainingCapacity);
        Fired = Hits = 0;
        nextShotTime = 0;
    }

    private void OnDestroy()
    {
        if (target != null) target.OnDamageTaken -= HandleDamage;
        foreach (var projectile in projectiles) if (projectile != null) Destroy(projectile.gameObject);
    }
}
