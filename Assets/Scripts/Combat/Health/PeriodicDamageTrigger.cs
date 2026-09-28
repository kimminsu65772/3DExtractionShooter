using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PeriodicDamageTrigger : MonoBehaviour
{
    [Header("피해 설정")]
    [SerializeField, Min(0f)] private float damagePerTick = 10f;
    [SerializeField, Min(0.01f)] private float tickInterval = 0.3f;

    private class ContactState
    {
        public readonly IDamageable Damageable;
        public int ColliderCount;
        public Coroutine DamageRoutine;

        public ContactState(IDamageable damageable)
        {
            Damageable = damageable;
            ColliderCount = 1;
        }
    }

    // 한 대상에게 여러 Collider가 있어도 피해 틱은 하나만 실행한다.
    private readonly Dictionary<Component, ContactState> contacts = new Dictionary<Component, ContactState>();

    private void Reset()
    {
        ConfigurePhysicsComponents();
    }

    private void Awake()
    {
        if (!ConfigurePhysicsComponents())
            enabled = false;
    }

    private void OnDisable()
    {
        foreach (ContactState contact in contacts.Values)
        {
            if (contact.DamageRoutine != null)
                StopCoroutine(contact.DamageRoutine);
        }

        contacts.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryResolveDamageable(other, out Component target, out IDamageable damageable))
            return;

        if (contacts.TryGetValue(target, out ContactState existingContact))
        {
            existingContact.ColliderCount++;
            return;
        }

        ContactState newContact = new ContactState(damageable);
        contacts.Add(target, newContact);
        newContact.DamageRoutine = StartCoroutine(ApplyDamageWhileContacted(target, newContact));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!TryResolveDamageable(other, out Component target, out _))
            return;

        if (!contacts.TryGetValue(target, out ContactState contact))
            return;

        contact.ColliderCount--;

        if (contact.ColliderCount > 0)
            return;

        if (contact.DamageRoutine != null)
            StopCoroutine(contact.DamageRoutine);

        contacts.Remove(target);
    }

    private IEnumerator ApplyDamageWhileContacted(Component target, ContactState contact)
    {
        WaitForSeconds waitForNextTick = new WaitForSeconds(tickInterval);

        // 접촉한 순간 첫 피해를 적용하고, 접촉이 유지되면 설정한 간격마다 반복한다.
        while (target != null && contact.ColliderCount > 0)
        {
            contact.Damageable.TakeDamage(CreateDamageInfo(target));
            yield return waitForNextTick;
        }

        if (target != null)
            contacts.Remove(target);
    }

    private DamageInfo CreateDamageInfo(Component target)
    {
        Vector3 hitDirection = target.transform.position - transform.position;

        if (hitDirection.sqrMagnitude > 0.0001f)
            hitDirection.Normalize();

        return new DamageInfo
        {
            weaponId = 0,
            ammoId = 0,
            armorPenetrationLevel = int.MaxValue,
            ammoDamageMultiplier = 1f,
            incomingDamage = damagePerTick,
            hitPoint = target.transform.position,
            hitDirection = hitDirection,
            hitPart = HitPart.Body,
            attacker = gameObject,
            baseDamage = damagePerTick,
            rangeMultiplier = 1f,
            hitPartMultiplier = 1f
        };
    }

    private static bool TryResolveDamageable(Collider collider, out Component target, out IDamageable damageable)
    {
        damageable = collider.GetComponentInParent<IDamageable>();
        target = damageable as Component;
        return target != null;
    }

    private bool ConfigurePhysicsComponents()
    {
        Collider triggerCollider = GetComponent<Collider>();

        if (triggerCollider == null)
        {
            Debug.LogError("PeriodicDamageTrigger에는 Collider가 필요합니다.", this);
            return false;
        }

        triggerCollider.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        return true;
    }
}
