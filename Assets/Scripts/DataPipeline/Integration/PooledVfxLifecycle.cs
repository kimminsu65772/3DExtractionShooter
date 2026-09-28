using System;
using System.Collections;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public class PooledVfxLifecycle : MonoBehaviour, IPoolLifecycle
    {
        [Header("안전 반환 설정")]
        [SerializeField, Min(0.1f)] private float maxPlaybackSeconds = 10f;

        private ParticleSystem[] particleSystems;

        private PoolLease currentLease;
        private IPoolReturner owner;
        private Coroutine returnCoroutine;
        private bool hasLease;

        private void Awake()
        {
            EnsureCache();
        }

        public void OnRent(PoolLease lease, IPoolReturner owner)
        {
            EnsureCache();

            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            StopReturnCoroutine();
            StopAndClearParticles();

            currentLease = lease;
            this.owner = owner;
            hasLease = true;
        }

        // 비활성 부모 아래에서 생성된 프리팹은 Awake보다 OnRent가 먼저 올 수 있으므로, EnsureCache를 OnRent에서도 호출하여
        // 필요한 캐시를 확보할 수 있도록 한다.
        private void EnsureCache()
        {
            particleSystems ??=
                GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        }

        public void Play()
        {
            if (!hasLease || owner == null)
                return;

            EnsureCache();

            if (particleSystems.Length == 0)
            {
                Debug.LogWarning("재생할 파티클 시스템이 없습니다.", this);

                ReturnToOwner();
                return;
            }

            StopReturnCoroutine();

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null)
                    continue;
                ps.Play(withChildren: false);
            }

            returnCoroutine = StartCoroutine(ReturnWhenFinished());
        }

        private IEnumerator ReturnWhenFinished()
        {
            float elapsedTime = 0f;
            while (IsAnyParticleAlive())
            {
                if (elapsedTime >= maxPlaybackSeconds)
                {
                    Debug.LogWarning("VFX가 제한 시간 안에 종료되지 않아 강제로 풀에 반환합니다.");
                    break;
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            returnCoroutine = null;
            ReturnToOwner();
        }

        private bool IsAnyParticleAlive()
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null)
                    continue;
                if (ps.IsAlive(withChildren: false))
                    return true;
            }
            return false;
        }

        private void StopAndClearParticles()
        {
            if (particleSystems == null)
                return;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null)
                    continue;
                ps.Stop(withChildren: false, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        // 이전에 재생 중이던 반환 코루틴이 남아 있는 상태로 다시 VFX가 대여되면,
        // 해당 코루틴에 의해 새롭게 재생중인 VFX가 강제로 반환될 수 있으므로, 새로 대여될 때 이전의 반환 코루틴을 중지한다.
        private void StopReturnCoroutine()
        {
            if (returnCoroutine == null)
                return;

            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        private void ReturnToOwner()
        {
            if (!hasLease || owner == null)
                return;
            PoolLease lease = currentLease;
            IPoolReturner returner = owner;
            hasLease = false;
            currentLease = default;
            owner = null;

            if (!returner.TryReturn(lease))
            {
                Debug.LogWarning("VFX를 소유 풀에 반환하지 못했습니다.", this);
            }
        }

        public void OnReturn()
        {
            StopReturnCoroutine();
            StopAndClearParticles();

            hasLease = false;
            currentLease = default;
            owner = null;
        }
    }
}
