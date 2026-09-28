using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public class CombatFeedbackService
    {
        private readonly CombatDataBootstrap bootstrap;

        public CombatFeedbackService(CombatDataBootstrap bootstrap)
        {
            this.bootstrap = bootstrap ??
                throw new ArgumentNullException(nameof(bootstrap));
        }

        public void PlayMuzzleAndSound(RuntimeWeapon weapon, Vector3 position, Quaternion rotation)
        {
            if (weapon == null)
                return;

            TryPlayVfx(weapon.MuzzleVfxId, position, rotation);

            TryPlaySound(weapon.FireSoundId, position);
        }
        public void PlayHit(int effectId, Vector3 position, Vector3 normal)
        {
            if (effectId <= 0)
                return;

            Quaternion rotation = normal.sqrMagnitude > 0.0001f
                ? Quaternion.FromToRotation(Vector3.forward, normal.normalized)
                : Quaternion.identity;

            TryPlayVfx(effectId, position, rotation);
        }

        private bool TryPlayVfx(int effecId, Vector3 position, Quaternion rotation)
        {
            if (effecId <= 0)
                return false;

            KeyedPoolService pools = bootstrap?.Pools;

            if (pools == null || !pools.IsReady)
                return false;

            if (!pools.TryRent(effecId, out PoolLease lease))
                return false;

            try
            {
                GameObject instance = lease.Instance;

                if (!instance.TryGetComponent(out PooledVfxLifecycle lifecycle))
                {
                    throw new MissingComponentException(
                        $"VFX 프리팹에는 " +
                        $"{nameof(PooledVfxLifecycle)}가 필요합니다."
                    );
                }

                instance.transform.SetPositionAndRotation(position, rotation);

                pools.Activate(lease);

                lifecycle.Play();

                return true;
            }
            catch (Exception e)
            {
                // 초기화나 활성화 중에 예외가 발생하면, 객체를 반환한다.
                pools.TryReturn(lease);

                Debug.LogException(e, lease.Instance);

                return false;
            }
        }

        private bool TryPlaySound(int audioClipId, Vector3 position)
        {
            if (audioClipId <= 0)
                return false;

            if (!bootstrap.TryGetAudioClip(audioClipId, out AudioClip clip))
                return false;

            int emitterPoolId = bootstrap.AudioEmitterPoolId;

            if (emitterPoolId <= 0)
                return false;

            KeyedPoolService pools = bootstrap?.Pools;

            if (pools == null || !pools.IsReady)
                return false;

            if (!pools.TryRent(emitterPoolId, out PoolLease lease))
                return false;

            try
            {
                GameObject instance = lease.Instance;

                if (!instance.TryGetComponent(out PooledAudioLifecycle lifecycle))
                {
                    throw new MissingComponentException(
                        $"AudioEmitter 프리팹에는 " +
                        $"{nameof(AudioSource)}가 필요합니다."
                    );
                }

                instance.transform.position = position;

                pools.Activate(lease);

                lifecycle.Play(clip, position);

                return true;
            }
            catch (Exception e)
            {
                // 초기화나 활성화 중에 예외가 발생하면, 객체를 반환한다.
                pools.TryReturn(lease);
                Debug.LogException(e, lease.Instance);
                return false;
            }
        }
    }
}
