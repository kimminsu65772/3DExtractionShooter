using System.Collections.Generic;
using UnityEngine;
using System;

public class DestructibleCover : MonoBehaviour, ICover
{
    [Header("엄폐물 설정")]
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private Collider coverCollider;

    [Header("엄폐 대상 마스크")]
    [SerializeField] private LayerMask coverTargetMask;

    [Header("엄폐물")]
    [SerializeField] private GameObject defaultVisuals;
    [SerializeField] private GameObject deadVisuals;

    private PlayerController coveredPlayer;

    private readonly HashSet<Collider> registeredTargets = new();

    public Collider CoverCollider => coverCollider;

    public PlayerController CoveredPlayer => coveredPlayer;

    public bool IsAvailable => healthComponent != null && healthComponent.IsAlive && isActiveAndEnabled;

    public event Action<bool> OnCoverStateChanged;

    private void OnEnable()
    {
        if (healthComponent != null)
            healthComponent.OnDeath += Destroyed;
    }

    private void OnDisable()
    {
        ClearTargets();
        if (healthComponent != null)
            healthComponent.OnDeath -= Destroyed;
    }

    public bool IsCovering(GameObject character)
    {
        return IsAvailable
            && coveredPlayer != null
            && registeredTargets.Count > 0
            && coveredPlayer.gameObject == character;
    }

    private void Destroyed(DamageInfo damageInfo)
    {
        ClearTargets();
        defaultVisuals.SetActive(false);
        deadVisuals.SetActive(true);
    }

    public void RegisterTarget(Collider other)
    { 
        if (other == null || registeredTargets.Contains(other))
            return;

        if (((1 << other.gameObject.layer) & coverTargetMask) != 0 && IsAvailable)
        {
            PlayerController playerController = other.GetComponentInParent<PlayerController>();
            if (playerController != null)
            {
                coveredPlayer = playerController;
                registeredTargets.Add(other);

                if (registeredTargets.Count == 1)
                {
                    OnCoverStateChanged?.Invoke(true);
                }
            }
        }
    }

    public void UnregisterTarget(Collider other)
    {
        if (other == null || !registeredTargets.Contains(other))
            return;

        registeredTargets.Remove(other);
        if (registeredTargets.Count == 0)
        {
            coveredPlayer = null;
            OnCoverStateChanged?.Invoke(false);
        }
    }

    public void ClearTargets()
    {
        // 정리 전에 실제 엄폐 중이었는지 기억한다.
        bool wasCovering = registeredTargets.Count > 0;

        registeredTargets.Clear();
        coveredPlayer = null;

        // 파괴·비활성화가 연이어 발생해도 종료 알림은 한 번만 보낸다.
        if (wasCovering)
            OnCoverStateChanged?.Invoke(false);
    }
}
