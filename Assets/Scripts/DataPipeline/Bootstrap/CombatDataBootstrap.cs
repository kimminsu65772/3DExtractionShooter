using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    // 씬에 자동 부착되지 않는 orchestration 뼈대. 구현 후 composition root에서 생성/호출한다.
    public class CombatDataBootstrap
    {
        private readonly AddressablesAssetStore assets;
        private readonly ICsvRecordReader csvReader;
        private readonly ICsvBinding<WeaponDefinition> weaponBinding;
        private readonly IRowValidator<WeaponDefinition> weaponValidator;
        private readonly CsvTableLoader<WeaponDefinition> weaponTableLoader;
        private readonly WeaponCatalog weaponCatalog;
        private readonly ICsvBinding<AmmoDefinition> ammoBinding;
        private readonly IRowValidator<AmmoDefinition> ammoValidator;
        private readonly CsvTableLoader<AmmoDefinition> ammoTableLoader;
        private readonly AmmoCatalog ammoCatalog;
        private readonly PoolPlanBuilder poolPlanBuilder;
        private readonly KeyedPoolService pools;
        private readonly IReadOnlyDictionary<AssetKind, PoolPolicy> poolPolicies;
        private readonly Transform inactiveRoot;
        private readonly string audioEmitterAddress;

        // AudioClip은 GameObject처럼 풀링하지 않고 Addressables lease로 보관한다.
        private readonly Dictionary<int, AssetLease<AudioClip>> audioClipLeases = new();

        private int audioEmitterPoolId;

        private RuntimeAssetRegistry runtimeAssetRegistry;

        private CancellationTokenSource initializationCancellation;

        private PipelineState state = PipelineState.Uninitialized;
        private Task initializationTask;
        private Task shutdownTask;

        public PipelineState State => state;
        public DataReport LastReport { get; private set; }
        public WeaponCatalog WeaponCatalog => weaponCatalog;
        public AmmoCatalog AmmoCatalog => ammoCatalog;
        public KeyedPoolService Pools => pools;
        public int AudioEmitterPoolId => audioEmitterPoolId;

        public CombatDataBootstrap(
            AddressablesAssetStore assets,
            ICsvRecordReader csvReader,
            ICsvBinding<WeaponDefinition> weaponBinding,
            IRowValidator<WeaponDefinition> weaponValidator,
            CsvTableLoader<WeaponDefinition> weaponTableLoader,
            WeaponCatalog weaponCatalog,
            ICsvBinding<AmmoDefinition> ammoBinding,
            IRowValidator<AmmoDefinition> ammoValidator,
            CsvTableLoader<AmmoDefinition> ammoTableLoader,
            AmmoCatalog ammoCatalog,
            PoolPlanBuilder poolPlanBuilder,
            KeyedPoolService pools,
            IReadOnlyDictionary<AssetKind, PoolPolicy> poolPolicies,
            Transform inactiveRoot,
            string audioEnitterAddress)
        {
            this.assets = assets ??
                throw new ArgumentNullException(nameof(assets));

            this.csvReader = csvReader ??
                throw new ArgumentNullException(nameof(csvReader));

            this.weaponBinding = weaponBinding ??
                throw new ArgumentNullException(nameof(weaponBinding));

            this.weaponValidator = weaponValidator ??
                throw new ArgumentNullException(nameof(weaponValidator));

            this.weaponTableLoader = weaponTableLoader ??
                throw new ArgumentNullException(nameof(weaponTableLoader));

            this.weaponCatalog = weaponCatalog ??
                throw new ArgumentNullException(nameof(weaponCatalog));

            this.ammoBinding = ammoBinding ??
                throw new ArgumentNullException(nameof(ammoBinding));

            this.ammoValidator = ammoValidator ??
                throw new ArgumentNullException(nameof(ammoValidator));

            this.ammoTableLoader = ammoTableLoader ??
                throw new ArgumentNullException(nameof(ammoTableLoader));

            this.ammoCatalog = ammoCatalog ??
                throw new ArgumentNullException(nameof(ammoCatalog));

            this.poolPlanBuilder = poolPlanBuilder ??
                throw new ArgumentNullException(nameof(poolPlanBuilder));

            this.pools = pools ??
                throw new ArgumentNullException(nameof(pools));

            this.poolPolicies = poolPolicies ??
                throw new ArgumentNullException(nameof(poolPolicies));

            this.inactiveRoot = inactiveRoot != null
                ? inactiveRoot
                : throw new ArgumentNullException(nameof(inactiveRoot));

            this.audioEmitterAddress = !string.IsNullOrWhiteSpace(audioEnitterAddress)
                ? audioEnitterAddress
                : throw new ArgumentException(nameof(audioEnitterAddress));
        }

        public bool TryGetAudioClip(int audioClipId, out AudioClip audioClip)
        {
            audioClip = null;

            if (state != PipelineState.Ready)
                return false;

            if (audioClipId <= 0)
                return false;

            if (!audioClipLeases.TryGetValue(
                audioClipId,
                out AssetLease<AudioClip> lease))
            {
                return false;
            }

            if (lease == null || lease.IsDisposed)
                return false;

            audioClip = lease.Asset;
            return audioClip != null;
        }

        public Task InitializeAsync(string weaponCsvAddress, string ammoCsvAddress, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(weaponCsvAddress))
                throw new ArgumentException("무기 CSV 주소가 비어있습니다.", nameof(weaponCsvAddress));
            if (string.IsNullOrWhiteSpace(ammoCsvAddress))
                throw new ArgumentException("탄약 CSV 주소가 비어있습니다.", nameof(ammoCsvAddress));

            if (state == PipelineState.Failed)
            {
                throw new InvalidOperationException(
                    "실패한 Bootstrap은 다시 초기화할 수 없습니다. 새 인스턴스를 생성해야 합니다.");
            }

            if (state == PipelineState.Ready)
                return Task.CompletedTask;

            if (state == PipelineState.ShuttingDown ||
                state == PipelineState.Disposed)
            {
                throw new InvalidOperationException(
                    $"현재 상태에서는 초기화할 수 없습니다. State: {state}");
            }

            // 이미 초기화가 진행 중이면, 새 작업을 만들지 않고 기존 초기화 작업을 기다란다.
            if (initializationTask != null && !initializationTask.IsCompleted)
                return initializationTask;

            // 외부 취소 토큰과 부트스트랩 내부 취소 토큰을 연결하여 초기화 작업을 취소할 수 있도록 한다.
            initializationCancellation?.Dispose();
            initializationCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);

            initializationTask = InitializationCoreAsync(weaponCsvAddress, ammoCsvAddress, initializationCancellation.Token);

            return initializationTask;
        }

        private async Task InitializationCoreAsync(string weaponCsvAddress, string ammoCsvAddress, CancellationToken token)
        {
            LastReport = new DataReport();

            try
            {
                state = PipelineState.LoadingData;

                IReadOnlyList<WeaponDefinition> definitions = await LoadDefinitionsAsync(weaponCsvAddress, weaponBinding, weaponValidator, weaponTableLoader, LastReport, token);
                IReadOnlyList<AmmoDefinition> ammoDefinitions = await LoadDefinitionsAsync(ammoCsvAddress, ammoBinding, ammoValidator, ammoTableLoader, LastReport, token);

                token.ThrowIfCancellationRequested();

                state = PipelineState.Validating;

                // 이번 초기화에서 사용할 Address ↔ 정수 ID 대응표를 생성한다.
                runtimeAssetRegistry = new RuntimeAssetRegistry();

                IReadOnlyList<RuntimeWeapon> runtimeWeapons = BuildRuntimeWeapons(definitions, runtimeAssetRegistry, weaponCsvAddress, LastReport);

                ThrowIfReportHasErrors
                    (LastReport,
                    "런타임 무기 데이터 생성 과정에서 오류가 발생했습니다.");

                audioEmitterPoolId = runtimeAssetRegistry.Register(
                    audioEmitterAddress,
                    AssetKind.AudioEmitter);

                runtimeAssetRegistry.Freeze();

                IReadOnlyList<PoolSpec> poolPlan = poolPlanBuilder.Build(runtimeWeapons, runtimeAssetRegistry, poolPolicies, audioEmitterPoolId, LastReport);

                ThrowIfReportHasErrors(
                    LastReport,
                    "오브젝트 풀 계획 생성에 실패했습니다.");

                state = PipelineState.LoadingAssets;

                await LoadAudioClipsAsync(runtimeWeapons, runtimeAssetRegistry, token);

                token.ThrowIfCancellationRequested();

                state = PipelineState.Prewarming;

                await pools.InitializeAsync(poolPlan, assets, inactiveRoot, token);

                token.ThrowIfCancellationRequested();

                weaponCatalog.Publish(runtimeWeapons, LastReport);
                ammoCatalog.Publish(ammoDefinitions, LastReport);

                ThrowIfReportHasErrors(
                    LastReport,
                    "전투 데이터 카탈로그 공개 과정에서 오류가 발생했습니다.");

                state = PipelineState.Ready;
            }
            catch
            {
                await RollbackInitializationFailureAsync();
                state = PipelineState.Failed;
                throw;
            }
        }

        private async Task<IReadOnlyList<T>> LoadDefinitionsAsync<T>(
            string csvAddress,
            ICsvBinding<T> binding,
            IRowValidator<T> validator,
            CsvTableLoader<T> tableLoader,
            DataReport report,
            CancellationToken token) where T : class
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (validator == null) throw new ArgumentNullException(nameof(validator));
            if (tableLoader == null) throw new ArgumentNullException(nameof(tableLoader));

            AssetLease<TextAsset> csvLease = null;

            try
            {
                // Addressables에서 CSV TextAsset의 사용 권한을 획득한다.
                csvLease = await assets.AcquireAsync<TextAsset>(
                    csvAddress,
                    token);

                token.ThrowIfCancellationRequested();

                // TextAsset이 해제된 뒤에도 파싱할 수 있도록
                // CSV 내용을 일반 C# 문자열로 복사한다.
                string csvText = csvLease.Asset.text;

                if (string.IsNullOrWhiteSpace(csvText))
                {
                    report.Add(new DataIssue
                    {
                        Source = csvAddress,
                        Record = 0,
                        Column = string.Empty,
                        RawValue = string.Empty,
                        Message = "CSV 파일의 내용이 비어 있습니다."
                    });

                    ThrowIfReportHasErrors(
                        report,
                        "CSV 원본 확인에 실패했습니다.");
                }

                CsvDocument document;

                // StringReader는 문자열을 TextReader 형태로 제공한다.
                // CsvHelperRecordReader는 구체적인 파일 경로를 몰라도
                // TextReader만으로 CSV를 읽을 수 있다.
                using (StringReader textReader = new StringReader(csvText))
                {
                    document = csvReader.Read(
                        textReader,
                        csvAddress,
                        report);
                }

                ThrowIfReportHasErrors(
                    report,
                    "CSV 읽기에 실패했습니다.");

                if (document == null)
                {
                    throw new InvalidOperationException(
                        "CSV Reader가 CsvDocument를 생성하지 못했습니다.");
                }

                // 헤더 검사, 각 행 바인딩, 데이터 타입별 규칙 검증을 실행한다.
                IReadOnlyList<T> definitions = tableLoader.Load(document, binding, validator, report);

                ThrowIfReportHasErrors(
                    report,
                    $"{typeof(T).Name} 데이터 바인딩 또는 검증에 실패했습니다.");

                return definitions;
            }
            finally
            {
                // CSV 문자열은 이미 복사했으므로 TextAsset을 계속 보관할 필요가 없다.
                csvLease?.Dispose();
            }
        }

        private static IReadOnlyList<RuntimeWeapon> BuildRuntimeWeapons(
            IReadOnlyList<WeaponDefinition> definitions,
            RuntimeAssetRegistry registry,
            string source,
            DataReport report)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (report == null)
                throw new ArgumentNullException(nameof(report));

            List<RuntimeWeapon> runtimeWeapons =
                new List<RuntimeWeapon>(definitions.Count);

            // WeaponCatalog를 공개하기 전에 중복 무기 ID를 발견하기 위해 사용한다.
            HashSet<int> weaponIds = new HashSet<int>();

            for (int i = 0; i < definitions.Count; i++)
            {
                WeaponDefinition definition = definitions[i];

                if (definition == null)
                {
                    report.Add(new DataIssue
                    {
                        Source = source,
                        Record = i + 2,
                        Column = string.Empty,
                        RawValue = string.Empty,
                        Message = "WeaponDefinition이 null입니다."
                    });

                    continue;
                }

                if (!weaponIds.Add(definition.weaponId))
                {
                    report.Add(new DataIssue
                    {
                        Source = source,
                        Record = i + 2,
                        Column = nameof(WeaponDefinition.weaponId),
                        RawValue = definition.weaponId.ToString(),
                        Message = "무기 ID가 중복되었습니다."
                    });

                    continue;
                }

                // 같은 Address와 같은 AssetKind 조합은 동일한 정수 ID를 반환한다.
                int projectileId = registry.Register(
                    definition.projectileAddress,
                    AssetKind.Projectile);

                int weaponViewId = registry.Register(
                    definition.weaponViewAddress,
                    AssetKind.WeaponView);

                int muzzleVfxId = registry.Register(
                    definition.muzzleVfxAddress,
                    AssetKind.MuzzleVfx);

                int hitVfxId = registry.Register(
                    definition.hitVfxAddress,
                    AssetKind.HitVfx);

                // AudioClip 자체는 풀링하지 않지만,
                // 문자열 Address 대신 정수 ID로 조회하기 위해 Registry에는 등록한다.
                int fireSoundId = registry.Register(
                    definition.fireSoundAddress,
                    AssetKind.AudioClip);

                RuntimeWeapon runtimeWeapon = new RuntimeWeapon(
                    definition,
                    projectileId,
                    weaponViewId,
                    muzzleVfxId,
                    hitVfxId,
                    fireSoundId);

                runtimeWeapons.Add(runtimeWeapon);
            }

            return runtimeWeapons;
        }

        private async Task LoadAudioClipsAsync(
            IReadOnlyList<RuntimeWeapon> runtimeWeapons,
            RuntimeAssetRegistry registry,
            CancellationToken token)
        {
            if (runtimeWeapons == null)
                throw new ArgumentNullException(nameof(runtimeWeapons));

            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            for (int i = 0; i < runtimeWeapons.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                RuntimeWeapon weapon = runtimeWeapons[i];

                if (weapon == null)
                    continue;

                int audioClipId = weapon.FireSoundId;

                // ID 0은 해당 무기가 발사 사운드를 사용하지 않는다는 의미다.
                if (audioClipId == 0)
                    continue;

                // 여러 무기가 같은 AudioClip ID를 사용하더라도
                // Addressables lease는 한 번만 획득한다.
                if (audioClipLeases.ContainsKey(audioClipId))
                    continue;

                if (!registry.TryResolve(
                    audioClipId,
                    out string address,
                    out AssetKind kind))
                {
                    throw new InvalidOperationException(
                        $"AudioClip ID에 해당하는 Address를 찾을 수 없습니다. ID: {audioClipId}");
                }

                if (kind != AssetKind.AudioClip)
                {
                    throw new InvalidOperationException(
                        $"리소스 종류가 AudioClip이 아닙니다. ID: {audioClipId}, Kind: {kind}");
                }

                AssetLease<AudioClip> lease = null;

                try
                {
                    lease = await assets.AcquireAsync<AudioClip>(
                        address,
                        token);

                    token.ThrowIfCancellationRequested();

                    // AudioClip 대여권의 소유를 Dictionary에 넘긴다.
                    audioClipLeases.Add(
                        audioClipId,
                        lease);

                    // Dictionary가 lease의 소유권을 넘겨받았으므로
                    // finally에서 Dispose하지 않도록 지역 변수를 비운다.
                    lease = null;
                }
                finally
                {
                    // 로드 직후 취소되거나 Dictionary 등록에 실패한 경우
                    // 아직 소유권을 넘기지 못한 lease를 정리한다.
                    lease?.Dispose();
                }
            }
        }
        private async Task RollbackInitializationFailureAsync()
        {
            // 외부에서 불완전한 무기 데이터를 조회하지 못하도록 먼저 비운다.
            weaponCatalog.Clear();
            ammoCatalog.Clear();

            // 풀까지 준비된 이후 실패했다면 모든 인스턴스와 프리팹 lease를 정리한다.
            if (pools.IsReady)
            {
                try
                {
                    await pools.ShutdownAsync();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            // 먼저 AudioClip lease를 전부 반환한 뒤 Dictionary를 비운다.
            ReleaseAudioClipLeases();
            runtimeAssetRegistry = null;
        }

        private void ReleaseAudioClipLeases()
        {
            foreach (AssetLease<AudioClip> lease in audioClipLeases.Values)
            {
                if (lease == null)
                    continue;

                try
                {
                    lease.Dispose();
                }
                catch (Exception exception)
                {
                    // 하나의 lease 반환이 실패해도 나머지는 계속 정리한다.
                    Debug.LogException(exception);
                }
            }

            audioClipLeases.Clear();
        }
        private static void ThrowIfReportHasErrors(DataReport report, string message)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            if (!report.HasErrors)
                return;

            throw new InvalidOperationException(
                $"{message} 오류 개수: {report.Issues.Count}");
        }
        public Task ShutdownAsync()
        {
            return shutdownTask ??= ShutdownCoreAsync();
        }

        private async Task ShutdownCoreAsync()
        {
            if (state == PipelineState.Disposed)
                return;

            state = PipelineState.ShuttingDown;

            // 초기화가 진행 중이면 연결된 토큰을 취소하여 초기화 작업을 중단한다.
            initializationCancellation?.Cancel();

            if (initializationTask != null && !initializationTask.IsCompleted)
            {
                try
                {
                    await initializationTask;
                }
                catch (OperationCanceledException)
                {
                    // 종료 요청으로 발생한 정상적인 취소이므로 무시한다.
                }
                catch (Exception exception)
                {
                    // 초기화 도중 예외가 발생하더라도 에러를 기록하고 종료 작업은 계속 진행한다.
                    Debug.LogException(exception);
                }
            }

            // 다른 초기화 작업에 의해 상태 변경이 발생했을 수 있으므로 다시 종료 상태로 설정한다.
            state = PipelineState.ShuttingDown;

            weaponCatalog.Clear();
            ammoCatalog.Clear();

            try
            {
                await pools.ShutdownAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            ReleaseAudioClipLeases();

            audioEmitterPoolId = 0;

            runtimeAssetRegistry = null;

            try
            {
                // 부트스트랩이 소유한 모든 대여권을 반환한 뒤 AddressablesAssetStore의 남은 핸들을 정리한다.
                await assets.ShutdownAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            initializationCancellation?.Dispose();
            initializationCancellation = null;

            state = PipelineState.Disposed;
        }
    }
}
