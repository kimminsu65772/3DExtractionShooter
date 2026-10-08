using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    // 여러 개의 CombatDataCompositionRoot 컴포넌트가 같은 GameObject에 붙는 것을 방지하기 위해 DisallowMultipleComponent 어트리뷰트를 사용한다.
    [DisallowMultipleComponent]
    public class CombatDataCompositionRoot : MonoBehaviour
    {
        [Header("데이터 주소")]
        [SerializeField] private string weaponCsvAddress = "data/weapons";
        [SerializeField] private string ammoCsvAddress = "data/ammo";
        [SerializeField] private string medicineCsvAddress = "data/medicine";
        [SerializeField] private string armorCsvAddress = "data/armor";
        [SerializeField] private string helmetCsvAddress = "data/helmet";

        [Header("비활성 풀 보관 위치")]
        [SerializeField] private Transform inactivePoolRoot;

        [Header("투사체 풀 정책")]
        [SerializeField, Min(0)] private int projectileMinimumPrewarmCount;
        [SerializeField, Min(1)] private int projectileMaxActive = 128;
        [SerializeField, Min(1)] private int projectileMaxRetained = 64;
        [SerializeField] private PoolOverflowPolicy projectileOverflow = PoolOverflowPolicy.GrowWithinLimit;

        [Header("무기 외형 풀 정책")]
        [SerializeField, Min(0)] private int weaponViewPrewarmCount = 1;
        [SerializeField, Min(1)] private int weaponViewMaxActive = 32;
        [SerializeField, Min(1)] private int weaponViewMaxRetained = 16;
        [SerializeField] private PoolOverflowPolicy weaponViewOverflow = PoolOverflowPolicy.GrowWithinLimit;

        [Header("총구 VFX 풀 정책")]
        [SerializeField, Min(0)] private int muzzleVfxPrewarmCount = 4;
        [SerializeField, Min(1)] private int muzzleVfxMaxActive = 32;
        [SerializeField, Min(1)] private int muzzleVfxMaxRetained = 16;
        [SerializeField] private PoolOverflowPolicy muzzleVfxOverflow = PoolOverflowPolicy.GrowWithinLimit;

        [Header("피격 VFX 풀 정책")]
        [SerializeField, Min(0)] private int hitVfxPrewarmCount = 8;
        [SerializeField, Min(1)] private int hitVfxMaxActive = 64;
        [SerializeField, Min(1)] private int hitVfxMaxRetained = 32;
        [SerializeField] private PoolOverflowPolicy hitVfxOverflow = PoolOverflowPolicy.GrowWithinLimit;

        [Header("오디오 재생기")]
        [SerializeField] private string audioEmitterAddress = "feedback/audio-emitter";
        [SerializeField, Min(0)] private int audioEmitterPrewarmCount = 8;
        [SerializeField, Min(1)] private int audioEmitterMaxActive = 32;
        [SerializeField, Min(1)] private int audioEmitterMaxRetained = 16;

        [SerializeField]
        private PoolOverflowPolicy audioEmitterOverflow =
            PoolOverflowPolicy.GrowWithinLimit;

        private CancellationTokenSource lifetimeCancellation;
        private CombatDataBootstrap bootstrap;
        private CombatFeedbackService feedbackService;

        public CombatDataBootstrap Bootstrap => bootstrap;
        public CombatFeedbackService FeedbackService => feedbackService;

        public bool isReady => bootstrap != null && bootstrap.State == PipelineState.Ready;

        public WeaponCatalog WeaponCatalog => bootstrap?.WeaponCatalog;
        public AmmoCatalog AmmoCatalog => bootstrap?.AmmoCatalog;
        public ItemDefinitionCatalog<MedicineDefinition> MedicineCatalog => bootstrap?.MedicineCatalog;
        public ItemDefinitionCatalog<ArmorDefinition> ArmorCatalog => bootstrap?.ArmorCatalog;
        public ItemDefinitionCatalog<HelmetDefinition> HelmetCatalog => bootstrap?.HelmetCatalog;

        public KeyedPoolService Pools => bootstrap?.Pools;

        // 데이터 준비를 기다리는 객체가 파이프라인 준비 완료 시점을 알 수 있도록 이벤트를 제공한다.
        public event Action OnInitialized;

        private void Awake()
        {
            lifetimeCancellation = new CancellationTokenSource();

            EnsureInactivePoolRoot();

            IReadOnlyDictionary<AssetKind, PoolPolicy> poolPolicies = CreatePoolPolicies();

            AddressablesAssetStore assetStore = new AddressablesAssetStore();

            CsvHelperRecordReader csvReader = new CsvHelperRecordReader();

            GeneratedWeaponBinding weaponBinding = new GeneratedWeaponBinding();

            WeaponValidator weaponValidator = new WeaponValidator();

            CsvTableLoader<WeaponDefinition> weaponTableLoader = new CsvTableLoader<WeaponDefinition>();

            WeaponCatalog weaponCatalog = new WeaponCatalog();

            GeneratedAmmoBinding ammoBinding = new GeneratedAmmoBinding();
            AmmoValidator ammoValidator = new AmmoValidator();
            CsvTableLoader<AmmoDefinition> ammoTableLoader = new CsvTableLoader<AmmoDefinition>();
            AmmoCatalog ammoCatalog = new AmmoCatalog();

            PoolPlanBuilder poolPlanBuilder =
                new PoolPlanBuilder();

            KeyedPoolService pools =
                new KeyedPoolService();

            bootstrap = new CombatDataBootstrap(
                assetStore,
                csvReader,
                weaponBinding,
                weaponValidator,
                weaponTableLoader,
                weaponCatalog,
                ammoBinding,
                ammoValidator,
                ammoTableLoader,
                ammoCatalog,
                poolPlanBuilder,
                pools,
                poolPolicies,
                inactivePoolRoot,
                audioEmitterAddress
            );

            // 플레이어와 적이 동일한 풀링 기반 전투 피드백 재생기를 공유한다.
            feedbackService = new CombatFeedbackService(bootstrap);
        }

        private async void Start()
        {
            try
            {
                await bootstrap.InitializeAsync(weaponCsvAddress, ammoCsvAddress, medicineCsvAddress, armorCsvAddress, helmetCsvAddress, lifetimeCancellation.Token);

                OnInitialized?.Invoke();

                Debug.Log(
                    $"전투 데이터 파이프라인 준비 완료. " +
                    $"등록된 무기: {bootstrap.WeaponCatalog.Count}, 등록된 탄약: {bootstrap.AmmoCatalog.Count}",
                    this
                );
            }
            catch (OperationCanceledException)
            {
                // 오브젝트가 파괴되거나 게임이 종료되어 발생한
                // 정상적인 초기화 취소는 오류로 기록하지 않는다.
                if (!lifetimeCancellation.IsCancellationRequested)
                    throw;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                LogDataIssues();
            }
        }

        private void EnsureInactivePoolRoot()
        {
            if (inactivePoolRoot == null)
            {
                GameObject rootObject = new GameObject("Inactive Combat Pools");
                inactivePoolRoot = rootObject.transform;
                rootObject.transform.SetParent(transform, false);
            }

            inactivePoolRoot.gameObject.SetActive(false);
        }

        private IReadOnlyDictionary<AssetKind, PoolPolicy> CreatePoolPolicies()
        {
            return new Dictionary<AssetKind, PoolPolicy>
            {
                {
                    AssetKind.Projectile,
                    new PoolPolicy(
                        AssetKind.Projectile,
                        projectileMinimumPrewarmCount,
                        projectileMaxActive,
                        projectileMaxRetained,
                        projectileOverflow
                    )
                },
                {
                    AssetKind.WeaponView,
                    new PoolPolicy(
                        AssetKind.WeaponView,
                        weaponViewPrewarmCount,
                        weaponViewMaxActive,
                        weaponViewMaxRetained,
                        weaponViewOverflow
                    )
                },
                {
                    AssetKind.MuzzleVfx,
                    new PoolPolicy(
                        AssetKind.MuzzleVfx,
                        muzzleVfxPrewarmCount,
                        muzzleVfxMaxActive,
                        muzzleVfxMaxRetained,
                        muzzleVfxOverflow
                    )
                },
                {
                    AssetKind.HitVfx,
                    new PoolPolicy(
                        AssetKind.HitVfx,
                        hitVfxPrewarmCount,
                        hitVfxMaxActive,
                        hitVfxMaxRetained,
                        hitVfxOverflow
                    )
                },
                {
                    AssetKind.AudioEmitter,
                    new PoolPolicy(
                        AssetKind.AudioEmitter,
                        audioEmitterPrewarmCount,
                        audioEmitterMaxActive,
                        audioEmitterMaxRetained,
                        audioEmitterOverflow
                    )
                }
            };
        }

        private void LogDataIssues()
        {
            DataReport report = bootstrap?.LastReport;

            if (report == null)
                return;

            foreach (DataIssue issue in report.Issues)
            {
                Debug.LogError(
                    $"[{issue.Source}] " +
                    $"행: {issue.Record}, " +
                    $"열: {issue.Column}, " +
                    $"값: {issue.RawValue}, " +
                    $"오류: {issue.Message}",
                    this
                );
            }
        }

        private async void OnDestroy()
        {
            lifetimeCancellation?.Cancel();

            if (bootstrap != null)
            {
                try
                {
                    await bootstrap.ShutdownAsync();
                }
                catch (Exception e)
                {
                    Debug.LogError($"CombatDataCompositionRoot: ShutdownAsync 중 오류 발생: {e.Message}", this);
                }
            }

            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }
    }
}
