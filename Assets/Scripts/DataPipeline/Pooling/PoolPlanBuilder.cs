using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    // 각 총기에 대한 풀링 계획을 생성한다. 풀링 계획은 런타임에서 풀을 생성할 때 사용된다.
    public sealed class PoolPlanBuilder
    {
        public IReadOnlyList<PoolSpec> Build(IReadOnlyList<RuntimeWeapon> encounterWeapons,
            RuntimeAssetRegistry registry,
            IReadOnlyDictionary<AssetKind, PoolPolicy> policies,
            int audioEmitterPoolId,
            DataReport report)
        {
            if (encounterWeapons == null)
                throw new ArgumentNullException(nameof(encounterWeapons));

            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (policies == null)
                throw new ArgumentNullException(nameof(policies));

            if (report == null)
                throw new ArgumentNullException(nameof(report));

            if (report.HasErrors)
                return Array.Empty<PoolSpec>();

            Dictionary<int, PoolSpec> specsByPoolId = new();

            foreach (RuntimeWeapon weapon in encounterWeapons)
            {
                if (weapon == null)
                {
                    AddIssue(
                        report,
                        0,
                        "RuntimeWeapon 후보가 null입니다.");

                    continue;
                }

                // 각 총기의 발사체에 대한 풀 사양을 추가하거나 병합한다.
                AddOrMergePoolSpec(
                    weapon.ProjectileId,
                    AssetKind.Projectile,
                    weapon.Definition.projectilePrewarmSize,
                    registry,
                    policies,
                    specsByPoolId,
                    report);

                // 장착 시 사용할 무기 외형 프리팹의 풀 사양을 추가하거나 병합한다.
                AddOrMergePoolSpec(
                    weapon.WeaponViewId,
                    AssetKind.WeaponView,
                    weapon.Definition.weaponViewPrewarmSize,
                    registry,
                    policies,
                    specsByPoolId,
                    report);

                // 각 총기의 발사 효과에 대한 풀 사양을 추가하거나 병합한다.
                AddOrMergePoolSpec(
                    weapon.MuzzleVfxId,
                    AssetKind.MuzzleVfx,
                    0,
                    registry,
                    policies,
                    specsByPoolId,
                    report);

                // 각 총기의 명중 효과에 대한 풀 사양을 추가하거나 병합한다.
                AddOrMergePoolSpec(
                    weapon.HitVfxId,
                    AssetKind.HitVfx,
                    0,
                    registry,
                    policies,
                    specsByPoolId,
                    report);

                // 각 총기의 발사음에 대한 풀 사양을 추가하거나 병합한다.
                AddOrMergePoolSpec(
                    audioEmitterPoolId,
                    AssetKind.AudioEmitter,
                    0,
                    registry,
                    policies,
                    specsByPoolId,
                    report);
            }

            if (report.HasErrors)
                return Array.Empty<PoolSpec>();

            List<int> sortedPoolIds = new(specsByPoolId.Keys);
            sortedPoolIds.Sort();

            List<PoolSpec> sortedSpecs = new(sortedPoolIds.Count);

            foreach (int poold in sortedPoolIds)
            {
                sortedSpecs.Add(specsByPoolId[poold]);
            }

            return sortedSpecs;
        }

        private static void AddOrMergePoolSpec(
            int poolId,
            AssetKind expectedKind,
            int requestedPrewarmCount,
            RuntimeAssetRegistry registry,
            IReadOnlyDictionary<AssetKind, PoolPolicy> policies,
            Dictionary<int, PoolSpec> specsByPoolId,
            DataReport report)
        {
            // poolId가 0이라는 뜻은 해당 리소스를 사용하지 않는다는 의미로 간주한다.
            if (poolId == 0) return;

            if (poolId < 0)
            {
                AddIssue(
                    report,
                    poolId,
                    "PoolId는 0보다 작을 수 없습니다.");

                return;
            }

            if (!registry.TryResolve(poolId, out string address, out AssetKind actualKind))
            {
                AddIssue(
                    report,
                    poolId,
                    $"PoolId {poolId}에 해당하는 리소스를 찾을 수 없습니다.");

                return;
            }

            if (actualKind != expectedKind)
            {
                AddIssue(
                    report,
                    poolId,
                    $"PoolId {poolId}에 해당하는 리소스의 종류가 예상과 다릅니다. 예상: {expectedKind}, 실제: {actualKind}");

                return;
            }

            if (!policies.TryGetValue(expectedKind,
                out PoolPolicy policy))
            {
                AddIssue(
                    report,
                    poolId,
                    $"PoolId {poolId}에 해당하는 리소스의 종류 {expectedKind}에 대한 풀 정책을 찾을 수 없습니다.");

                return;
            }

            if (policy == null || policy.Kind != expectedKind)
            {
                AddIssue(
                    report,
                    poolId,
                    $"PoolId {poolId}에 해당하는 리소스의 종류 {expectedKind}에 대한 풀 정책이 유효하지 않습니다.");
                return;
            }

            if (requestedPrewarmCount < 0)
            {
                AddIssue(
                    report,
                    poolId,
                    $"PoolId {poolId}에 대한 요청된 PrewarmCount가 0보다 작습니다.");
                return;
            }

            int prewarmCount = Math.Max(requestedPrewarmCount, policy.PrewarmCount);

            if (prewarmCount > policy.MaxRetained || prewarmCount > policy.MaxActive)
            {
                AddIssue(
                    report,
                    poolId,
                    $"PoolId {poolId}에 대한 요청된 PrewarmCount가 정책의 제한을 초과합니다. 요청된: {prewarmCount}, 정책 MaxRetained: {policy.MaxRetained}, 정책 MaxActive: {policy.MaxActive}");
                return;
            }

            // 해당 poolId에 대한 PoolSpec이 이미 존재하는 경우, PrewarmCount를 병합한다.
            if (specsByPoolId.TryGetValue(poolId, out PoolSpec existingSpec))
            {
                existingSpec.PrewarmCount = Math.Max(existingSpec.PrewarmCount, prewarmCount);

                return;
            }

            // 새로운 PoolSpec을 추가한다.
            specsByPoolId.Add(
                poolId,
                new PoolSpec
                {
                    PoolId = poolId,
                    Kind = actualKind,
                    Address = address,
                    PrewarmCount = prewarmCount,
                    MaxActive = policy.MaxActive,
                    MaxRetained = policy.MaxRetained,
                    Overflow = policy.Overflow
                });
        }

        private static void AddIssue(DataReport report, int poolId, string message)
        {
            report.Add(new DataIssue
            {
                Record = 0,
                Column = nameof(PoolSpec.PoolId),
                RawValue = poolId.ToString(),
                Message = message
            });
        }
    }
}
