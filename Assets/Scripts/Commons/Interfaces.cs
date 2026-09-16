using UnityEngine;

public interface IDamageable
{
    void TakeDamage(DamageInfo damageInfo);
}

public interface IHitPartProvider
{
    HitPart GetHitPart(Collider hitCollider);
}


