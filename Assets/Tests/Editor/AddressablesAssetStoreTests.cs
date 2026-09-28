using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionShooter.DataPipeline.Tests
{
    public sealed class AddressablesAssetStoreTests
    {
        [Test]
        public void AcquireAsync_EmptyAddress_ThrowsArgumentException()
        {
            var store = new AddressablesAssetStore();

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await store.AcquireAsync<Texture2D>(
                    "   ",
                    CancellationToken.None));
        }

        [Test]
        public void AcquireAsync_PreCanceledToken_ThrowsWithoutStartingLoad()
        {
            var store = new AddressablesAssetStore();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await store.AcquireAsync<Texture2D>(
                    "test-address",
                    cancellation.Token));
        }

        [Test]
        public async Task WaitForLoadOrCancellationAsync_LoadCompletes_ReturnsNormally()
        {
            await InvokeWaitForLoadOrCancellationAsync(
                Task.CompletedTask,
                CancellationToken.None);
        }

        [Test]
        public void WaitForLoadOrCancellationAsync_LoadFails_PropagatesException()
        {
            Task failedLoad = Task.FromException(
                new InvalidOperationException("load failed"));

            InvalidOperationException exception =
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await InvokeWaitForLoadOrCancellationAsync(
                        failedLoad,
                        CancellationToken.None));

            Assert.That(exception.Message, Is.EqualTo("load failed"));
        }

        [Test]
        public async Task WaitForLoadOrCancellationAsync_CallerCancels_SharedLoadRemainsPending()
        {
            var sharedLoad = new TaskCompletionSource<bool>();
            using var cancellation = new CancellationTokenSource();

            Task callerWait = InvokeWaitForLoadOrCancellationAsync(
                sharedLoad.Task,
                cancellation.Token);

            cancellation.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await callerWait);
            Assert.That(sharedLoad.Task.IsCompleted, Is.False);

            sharedLoad.TrySetResult(true);
            await sharedLoad.Task;
        }

        [Test]
        public void AssetLease_DisposeTwice_ReleasesExactlyOnceAndRejectsAssetAccess()
        {
            var asset = new Texture2D(1, 1);
            int releaseCount = 0;
            AssetLease<Texture2D> lease = CreateLease(
                asset,
                () => releaseCount++);

            try
            {
                Assert.That(lease.Asset, Is.SameAs(asset));

                lease.Dispose();
                lease.Dispose();

                Assert.That(releaseCount, Is.EqualTo(1));
                Assert.That(lease.IsDisposed, Is.True);
                Assert.Throws<ObjectDisposedException>(() =>
                {
                    _ = lease.Asset;
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static Task InvokeWaitForLoadOrCancellationAsync(
            Task loadTask,
            CancellationToken token)
        {
            MethodInfo method = typeof(AddressablesAssetStore).GetMethod(
                "WaitForLoadOrCancellationAsync",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);

            return (Task)method.Invoke(
                null,
                new object[] { loadTask, token });
        }

        private static AssetLease<T> CreateLease<T>(T asset, Action release)
            where T : UnityEngine.Object
        {
            ConstructorInfo constructor = typeof(AssetLease<T>).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                new[] { typeof(T), typeof(Action) },
                modifiers: null);

            Assert.That(constructor, Is.Not.Null);

            return (AssetLease<T>)constructor.Invoke(
                new object[] { asset, release });
        }
    }
}
