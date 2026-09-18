using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class PooledAudioLifecycle : MonoBehaviour, IPoolLifecycle
    {
        public void OnRent(PoolLease lease, IPoolReturner owner)
        {
            // TODO S08: AudioSource 캐시/lease 저장. 볼륨·pitch·loop·spatialBlend 등 초기화.
            throw new NotImplementedException();
        }
        public void Play(AudioClip clip, Vector3 position)
        {
            // TODO S08: clip 할당 후 Play. 한 emitter=한 voice. 종료/명시 정지 시 현재 lease 반환.
            // TODO: pause와 자연 종료 구분. clip lease는 세션 소유자가 재생 종료까지 유지.
            throw new NotImplementedException();
        }
        public void OnReturn()
        {
            // TODO S08: Stop, clip=null, 콜백/타이머 정리, 모든 AudioSource 설정 복구.
            throw new NotImplementedException();
        }
    }
}
