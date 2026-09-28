using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HealthComponent))]
public class TargetDummyHealthReset : MonoBehaviour
{
    [SerializeField] private float resetDelay = 3f;
    [SerializeField] private bool showDebugLog = true;

    private HealthComponent healthComponent;
    private float resetReadyTime;
    private bool isWaitingForReset;

    private void Awake()
    {
        healthComponent = GetComponent<HealthComponent>();
    }

    private void OnEnable()
    {
        if (healthComponent == null)
        {
            healthComponent = GetComponent<HealthComponent>();
        }

        healthComponent.OnDamageTaken += HandleDamageTaken;
    }

    private void OnDisable()
    {
        if (healthComponent == null)
            return;

        healthComponent.OnDamageTaken -= HandleDamageTaken;
    }

    private void Update()
    {
        if (!isWaitingForReset)
            return;

        if (Time.time < resetReadyTime)
            return;

        ResetHealth();
    }

    private void HandleDamageTaken(DamageInfo damageInfo)
    {
        resetReadyTime = Time.time + resetDelay;
        isWaitingForReset = true;
    }

    private void ResetHealth()
    {
        isWaitingForReset = false;

        if (healthComponent.CurrentHealth >= healthComponent.MaxHealth)
            return;

        healthComponent.Initialize(healthComponent.MaxHealth);

        if (showDebugLog)
        {
            Debug.Log($"[{name}] Target dummy health reset.", this);
        }
    }
}
