using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public class AssetLease<T> : IDisposable where T : UnityEngine.Object
    {
        private T asset;
        private Action release;
        private bool isDisposed;

        // 외부에서 직접 생성을 제한하면서도 같은 시스템 내에서는 생성할 수 있도록 internal로 제한한다.
        internal AssetLease(T asset, Action release)
        {
            this.asset = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
            this.release = release ?? throw new ArgumentNullException(nameof(release));
            isDisposed = false;
        }
        public T Asset
        {
            get
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(
                        nameof(AssetLease<T>));
                }

                return asset;
            }
        }

        public bool IsDisposed => isDisposed;

        public void Dispose()
        {
            if (isDisposed) return;

            // 중복 반환을 방지하기 위해서 먼저 상태를 폐기 상태로 전환한다.
            isDisposed = true;

            Action releaseCallback = release;

            release = null;
            asset = null;

            releaseCallback.Invoke();
        }
    }
}
