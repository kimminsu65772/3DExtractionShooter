using ExtractionShooter.DataPipeline;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    private WeaponDefinition weapon;
    private AmmoDefinition ammo;
    private Vector3 direction;
    private float traveledDistance;
    private float maxTravelDistance;
    private GameObject attacker;
    private LayerMask hitLayerMask;
    private DamageStatSnapshot damageStats = DamageStatSnapshot.Identity;

    private Vector3 currentPosition;
    private Vector3 nextPosition;

    private PooledProjectileLifecycle pooledLifecycle;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    private void Awake()
    {
        TryGetComponent(out pooledLifecycle);
    }

    public void Initialize(WeaponDefinition weapon, AmmoDefinition ammo, Vector3 startPos, Vector3 fireDirection, float aimDistance, GameObject attacker, LayerMask hitMask, DamageStatSnapshot damageStats)
    {
        this.weapon = weapon;
        this.ammo = ammo;
        this.direction = fireDirection.normalized;
        this.traveledDistance = 0f;
        this.maxTravelDistance = Mathf.Max(aimDistance, weapon.minDamageRange);
        this.attacker = attacker;
        this.hitLayerMask = hitMask;
        this.damageStats = damageStats;
        transform.position = startPos;
    }

    // 풀로 돌아갈 때 이전 발사의 참조와 진행 상태를 제거한다.
    // GameObject 활성화/비활성화는 PrefabPool만 담당한다.
    public void ResetForPool()
    {
        weapon = null;
        ammo = null;
        direction = Vector3.zero;
        traveledDistance = 0f;
        maxTravelDistance = 0f;
        attacker = null;
        hitLayerMask = default;
        damageStats = DamageStatSnapshot.Identity;
        currentPosition = Vector3.zero;
        nextPosition = Vector3.zero;
        System.Array.Clear(hitBuffer, 0, hitBuffer.Length);
    }

    private void Update()
    {
        if (weapon == null)
        {
            RequestReturn();
            return;
        }

        currentPosition = transform.position;
        float moveDistance = weapon.projectileSpeed * Time.deltaTime;

        // 다음 프레임에서 이동할 거리가 최대 이동 거리보다 크면, 최대 이동 거리까지만 이동하도록 한다.
        float remainingDistance = maxTravelDistance - traveledDistance;

        // 이미 최대 이동 거리를 초과한 경우, 반환 요청을 한다.
        if (remainingDistance <= 0f)
        {
            RequestReturn();
            return;
        }

        moveDistance = Mathf.Min(moveDistance, remainingDistance);

        nextPosition = currentPosition + direction * moveDistance;

        // 현재 위치와 다음 프레임 위치 사이에 충돌이 있는지 확인한다.
        int hitCount = Physics.SphereCastNonAlloc(
            currentPosition,
            weapon.projectileRadius,
            direction,
            hitBuffer,
            moveDistance,
            hitLayerMask,
            QueryTriggerInteraction.Collide
        );

        if (TryGetClosestHit(hitCount, out RaycastHit closestHit))
        {
            HandleHit(closestHit);
            return;
        }

        // 충돌이 없으면 이동
        transform.position = nextPosition;
        traveledDistance += moveDistance;
    }

    private bool TryGetClosestHit(int hitCount, out RaycastHit closestHit)
    {
        if (hitCount <= 0)
        {
            closestHit = default;
            return false;
        }

        closestHit = default;
        float closestDistance = float.MaxValue;
        bool hasHit = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];

            if (hit.collider == null)
                continue;

            // 바닥 아이템의 Trigger는 F키 획득 전용이므로 탄환 충돌 대상으로 취급하지 않는다.
            if (hit.collider.GetComponent<WorldItemPickup>() != null)
                continue;

            if (attacker != null && hit.collider.transform.IsChildOf(attacker.transform))
                continue;

            // 발사자가 엄폐 중인 오브젝트의 Trigger는 탄환 충돌 대상으로 취급하지 않는다.
            ICover cover = hit.collider.GetComponentInParent<ICover>();
            if (cover != null
                && hit.collider == cover.CoverCollider
                && cover.IsCovering(attacker))
            {
                continue;
            }

            float distance = hit.distance;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestHit = hit;
                hasHit = true;
            }
        }
        return hasHit;
    }

    private void HandleHit(RaycastHit hit)
    {
        // 1. 충돌 지점으로 투사체의 위치를 이동시킨다.
        transform.position = hit.point;

        // 2. 충돌한 오브젝트가 IDamageable을 갖고 있는지 확인한다.
        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

        if (damageable == null)
        {
            RequestReturn();
            return;
        }

        HitPart hitPart = ResolveHitPart(hit.collider);

        DamageInfo damageInfo = BuildDamageInfo(hit, hitPart);

        if (damageInfo.incomingDamage > 0f)
        {
            damageable.TakeDamage(damageInfo);
        }

        RequestReturn();
    }

    // 풀에서 대여한 투사체라면 소유 풀에 반환하고,
    // 기존 Instantiate 경로에서 생성된 투사체라면 비활성화한다.
    private void RequestReturn()
    {
        if (pooledLifecycle != null)
        {
            pooledLifecycle.ReturnToOwner();
            return;
        }

        gameObject.SetActive(false);
    }

    // 충돌체로부터 히트 파트를 얻어오는 메서드
    private HitPart ResolveHitPart(Collider hitCollider)
    {
        IHitPartProvider hitPartProvider = hitCollider.GetComponentInParent<IHitPartProvider>();

        // IHitPartProvider가 존재하면 HitPart를 반환하고, 존재하지 않으면 HitPart.Body를 반환한다.
        // 부서질 수 있는 오브젝트의 경우, 일괄적으로 HitPart.Body를 반환하여 데미지를 적용하도록 할 수 있도록 한다.
        if (hitPartProvider == null)
            return HitPart.Body;

        HitPart hitPart = hitPartProvider.GetHitPart(hitCollider);

        return hitPart == HitPart.None ? HitPart.Body : hitPart;
    }

    private DamageInfo BuildDamageInfo(RaycastHit hit, HitPart hitPart)
    {
        // 이전 프레임까지 이동한 거리에 이번 SphereCast의 충돌 거리까지 더한다.
        float hitDistance = traveledDistance + hit.distance;
        float rangeMultiplier = weapon.GetRangeMultiplier(hitDistance);
        float characterHitPartMultiplier = hitPart == HitPart.Head ? damageStats.HeadshotDamageMultiplier : 1f;
        float hitPartMultiplier = weapon.GetHitPartMultiplier(hitPart) * characterHitPartMultiplier;
        float ammoDamageMultiplier = ammo?.damageMultiplier ?? 1f;
        float incomingDamage = weapon.baseDamage * ammoDamageMultiplier * rangeMultiplier * hitPartMultiplier * damageStats.OutgoingDamageMultiplier;

        return new DamageInfo
        {
            weaponId = weapon.weaponId,
            ammoId = ammo?.ammoId ?? 0,
            armorPenetrationLevel = ammo?.armorPenetrationLevel ?? 0,
            ammoDamageMultiplier = ammoDamageMultiplier,
            incomingDamage = incomingDamage,
            // 기존 필드명은 유지하되 피해 계산에 사용한 실제 비행 거리를 전달한다.
            aimDistance = hitDistance,
            hitPoint = hit.point,
            hitDirection = direction,
            hitPart = hitPart,
            attacker = attacker,
            baseDamage = weapon.baseDamage,
            rangeMultiplier = rangeMultiplier,
            hitPartMultiplier = hitPartMultiplier
        };
    }
}
