using System;
using System.Collections;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    [RequireComponent(typeof(AudioSource))]
    public class PooledAudioLifecycle : MonoBehaviour, IPoolLifecycle
    {
        [SerializeField]
        private AudioSource audioSource;

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

            if (audioSource == null)
                throw new InvalidOperationException("AudioSource가 없습니다.");

            StopReturnCoroutine();
            ResetAudioSource();

            currentLease = lease;
            this.owner = owner;
            hasLease = true;
        }

        private void EnsureCache()
        {
            if (audioSource == null)
            {
                audioSource =
                    GetComponent<AudioSource>();
            }

            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.loop = false;
            }
        }
        public void Play(AudioClip clip, Vector3 position)
        {
            if (!hasLease || owner == null)
                return;

            if (clip == null)
            {
                Debug.LogWarning("재생할 오디오 클립이 없습니다.", this);

                ReturnToOwner();
                return;
            }

            EnsureCache();
            StopReturnCoroutine();

            transform.position = position;

            audioSource.clip = clip;
            audioSource.loop = false;
            audioSource.Play();

            returnCoroutine = StartCoroutine(ReturnWhenFinished());
        }

        private IEnumerator ReturnWhenFinished()
        {
            // Play 직후 같은 프레임에서 isPlaying이 false일 수 있으므로, 최소 1프레임 대기 후 체크한다.
            yield return null;

            while (audioSource != null)
            {
                // 오디오가 일시 정지된 상태에서는 종료로 판단하지 않는다.
                if (AudioListener.pause)
                {
                    yield return null;
                    continue;
                }

                if (!audioSource.isPlaying)
                    break;

                yield return null;
            }

            returnCoroutine = null;
            ReturnToOwner();
        }

        private void StopReturnCoroutine()
        {
            if (returnCoroutine == null)
                return;

            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        private void ResetAudioSource()
        {
            if (audioSource == null)
                return;

            audioSource.Stop();
            audioSource.clip = null;
            audioSource.loop = false;
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
                Debug.LogWarning($"AudioEmitter를 소유 풀에 반환하지 못했습니다.", this);
            }
        }

        public void OnReturn()
        {
            StopReturnCoroutine();
            ResetAudioSource();

            hasLease = false;
            currentLease = default;
            owner = null;
        }
    }
}
