using UnityEngine;

// FSM과 분대 BT가 같은 사망 정리와 외형 제거 절차를 사용한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(HealthComponent))]
public sealed class EnemyDeathController : MonoBehaviour
{
    [SerializeField] private GameObject deathEffectPrefab;

    private HealthComponent health;
    private bool hasCompletedDeath;

    private void Awake()
    {
        health = GetComponent<HealthComponent>();
    }

    private void OnEnable()
    {
        health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        health.OnDeath -= HandleDeath;
    }

    private void HandleDeath(DamageInfo damageInfo)
    {
        CompleteDeath();
    }

    public void CompleteDeath()
    {
        // FSM의 Dead 진입과 체력 이벤트 중 어느 쪽이 먼저 와도 한 번만 처리한다.
        if (hasCompletedDeath)
            return;

        hasCompletedDeath = true;
        GetComponent<EnemyDodgeController>()?.CancelDodge();
        GetComponent<EnemyMotor>()?.Stop();
        GetComponent<EnemyAttackController>()?.CancelReload();

        // 장비를 새로 만들지 않고 기존 드롭 경로로 넘긴 다음 외형을 제거한다.
        EnemyLootDropper lootDropper = GetComponent<EnemyLootDropper>();
        if (lootDropper == null || !lootDropper.TryDropLoot())
            Debug.LogWarning("적 사망 시 전리품을 생성하지 못했다.", this);

        GetComponent<EnemyWeaponLoadout>()?.DisableWeaponCollisionForDeath();

        // 부모를 지정하지 않아 적이 파괴된 뒤에도 파티클과 자식 효과가 살아 있게 한다.
        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
