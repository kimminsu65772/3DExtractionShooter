using System;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;
using ExtractionShooter.DataPipeline;
using UnityEngine;

public class SceneBootStrap : MonoBehaviour
{

    [Header("플레이어 스폰")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerSpawnPoint;

    [Header("플레이어 사망")]
    [SerializeField] private GameObject playerDeathEffectPrefab;

    [Header("스테이지 구성")]
    [SerializeField] private StageEnemySpawner stageEnemySpawner;
    [SerializeField] private StageLootInitializer stageLootInitializer;

    [Header("시야 제한 (로비에서는 끔)")]
    [SerializeField] private bool useFOV;

    [Header("시야 촬영")]
    [SerializeField] private FOVMaskRenderer fovMaskRenderer;

    [Header("참조를 연결할 컴포넌트 목록")]
    [SerializeField] private QuaterViewCameraController quaterViewCamera;
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private Crosshair crosshairPrefab;
    [SerializeField] private UnitHealthBarPresenter unitHealthBarPresenter;

    [Header("로비/스테이지 구분")]
    [SerializeField] private bool isLobbyScene;

    [Header("로비 창고")]
    [SerializeField] private LootContainer playerStorage;

    private PlayerController playerController;
    private PlayerInputHandler playerInputHandler;
    private PlayerAimController playerAimController;
    private PlayerInventory playerInventory;
    private PlayerWeaponHolder playerWeaponHolder;
    private PlayerAttackController playerAttackController;
    private PlayerReloadController playerReloadController;
    private PlayerWorldItemDropper playerWorldItemDropper;
    private PlayerAimCursor playerAimCursor;
    private PlayerEquipment playerEquipment;
    private PlayerItemQuickslots playerItemQuickslots;
    private FOV playerFOV;
    private AimPointProvider aimPointProvider;
    private GameObject spawnedPlayer;
    private TargetDummySpawner targetDummySpawner;
    private HealthComponent playerHealth;
    private Crosshair spawnedCrosshair;
    private ProgressHUD spawnedProgressHUD;
    private InteractionPromptUI spawnedInteractionPrompt;
    private bool playerDeathHandled;

    // [DATA-01 변경] 저장 상태와 호출 흐름을 부트스트랩 한 곳에서 관리한다.
    private GameSaveService saveService;
    private InventorySaveData preservedChest;
    private bool saveFlowReady;
    private bool ownedItemsDirty;
    private bool transitionPending;
    private bool saveFailed;
    private bool retryInProgress;
    private int saveOperation;
    private string lastRequestedContent;
    private GameSaveData pendingSaveData;
    private GameScene? pendingDestination;
    private GameObject pendingDeathEffect;
    private bool pendingRequestCreated;
    private GameObject saveFailurePanel;
    private UnityEngine.UI.Button retrySaveButton;
    private Font ownedSaveFailureFont;


    public async UniTask<bool> InitializeAsync()
    {
        CombatDataCompositionRoot instance = CombatDataCompositionRoot.Instance;

        if (instance == null)
        {
            Debug.LogError("CombatDataCompositionRoot 인스턴스가 존재하지 않습니다.");
            return false;
        }

        // 각 컴포넌트에 CombatDataCompositionRoot 인스턴스를 전달
        await instance.InitializeAsync();

        saveService = GameSaveContext.Service;

        if (saveService == null)
        {
            Debug.LogError("GameSaveContext.Service가 존재하지 않습니다.");
            return false;
        }

        if (!saveService.HasSaveData)
        {
            Debug.LogError("저장 데이터가 존재하지 않습니다. 게임을 시작하기 전에 저장 데이터를 생성해야 합니다.");
            return false;
        }

        GameSaveData saveData = GameSaveContext.Service.Load();

        if (saveData == null)
        {
            Debug.LogError("저장 데이터를 불러오지 못했습니다.");
            return false;
        }

        if (!InventorySaveMapper.TryRestoreSlots(saveData.Bag, instance, out IInventoryItem[] bagSlots))
        {
            Debug.LogError("인벤토리 데이터를 불러오지 못했습니다");
            return false;
        }

        if (bagSlots == null || bagSlots.Length <= 0)
        {
            Debug.LogError("인벤토리 슬롯이 비어있습니다");
            return false;
        }


        if (!InventorySaveMapper.TryRestoreSlots(saveData.Chest, instance, out IInventoryItem[] chestSlots))
        {
            Debug.LogError("창고 데이터를 불러오지 못했습니다");
            return false;
        }

        if (chestSlots == null || chestSlots.Length <= 0)
        {
            Debug.LogError("창고 슬롯이 비어있습니다");
            return false;
        }

        if (!InventorySaveMapper.TryRestoreEquipment(saveData.Equipment, instance, out WeaponEquipment weapons, out ProtectionEquipment protection, out int selectedWeaponIndex))
        {
            Debug.LogError("장비를 불러오지 못했습니다");
            return false;
        }
        
        InventoryContainer bagContainer = new InventoryContainer(bagSlots.Length);

        bagContainer.ApplyResize(bagSlots);

        if (bagContainer == null || bagContainer.SlotCount <= 0 || bagContainer.SlotCount != bagSlots.Length)
        {
            Debug.LogError("인벤토리 컨테이너를 생성하지 못했습니다");
            return false;
        }

        if (!InventorySaveMapper.TryRestoreQuickslots(saveData.QuickslotsBagIndices, bagContainer, out ItemQuickslots quickslots))
        {
            Debug.LogError("퀵슬롯 데이터를 불러오지 못했습니다");
            return false;
        }

        // [DATA-01 변경] 모든 복원 데이터를 준비한 뒤, 로비의 실제 창고에 슬롯 배치를 적용한다.
        // 스테이지는 창고 오브젝트를 요구하지 않으며, 로비 창고 복원 실패 시 플레이어 생성 전에 중단한다.
        if (isLobbyScene)
        {
            if (playerStorage == null)
            {
                Debug.LogError("로비 창고 참조가 없습니다.", this);
                return false;
            }

            if (!playerStorage.TryApplyRestoredSlots(chestSlots))
            {
                Debug.LogError("로비 창고에 저장된 슬롯을 적용하지 못했습니다.", this);
                return false;
            }
        }

        // [DATA-01 변경] 탐험 씬에는 창고 오브젝트가 없으므로 입장 시 확정한 값 DTO를 보존한다.
        preservedChest = saveData.Chest;

        spawnedPlayer = Instantiate(playerPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
        if (!TryGetPlayerComponent())
            return false;

        SetPlayerInputBlocked(true); // 초기화 중에는 입력을 차단

        if (quaterViewCamera == null)
        {
            Debug.LogError("QuaterViewCameraController가 할당되지 않았습니다.");
            return false;
        }

        Camera camera = quaterViewCamera.TryGetComponent<Camera>(out var cam) ? cam : null;
        

        playerController.SetCamera(camera.transform);
        playerAimController.SetCamera(camera);
        playerInventory.SetDataRoot(instance);
        playerWeaponHolder.SetDataRoot(instance);
        playerAttackController.SetDataRoot(instance);
        // [ACT-03·DATA-01 변경] 로비는 사격 시 장탄 수를 보존하고 탐험은 정상 소비한다.
        playerAttackController.SetConsumeAmmoOnFire(!isLobbyScene);
        playerReloadController.SetDataRoot(instance);
        playerWorldItemDropper.SetDataRoot(instance);
        quaterViewCamera.SetTarget(spawnedPlayer.transform, playerInputHandler, playerAimController);
        ConfigurePlayerFOV();
        
        spawnedCrosshair = Instantiate(crosshairPrefab, uiCanvas.transform, false);
        spawnedCrosshair.gameObject.SetActive(false);

        spawnedCrosshair.SetAimCursor(playerAimCursor);

        // 공통 배치 지점은 프리팹에서 편집하고 새 플레이어 데이터만 연결한다.
        GameObject progressPrefab = Resources.Load<GameObject>("UI/ProgressHUD");
        if (progressPrefab != null)
        {
            spawnedProgressHUD = Instantiate(progressPrefab, uiCanvas.transform, false).GetComponent<ProgressHUD>();
            spawnedProgressHUD.SetSources(spawnedPlayer.GetComponent<PlayerInteractionController>(), playerReloadController,
                spawnedPlayer.GetComponent<PlayerMedicineUseController>());
        }
        else Debug.LogError("공용 진행 HUD 프리팹이 없습니다: Resources/UI/ProgressHUD", this);

        // 씬별 배치를 늘리지 않고 공통 HUD를 새 플레이어와 실제 카메라에 연결한다.
        GameObject promptPrefab = Resources.Load<GameObject>("UI/InteractionPromptHUD");
        if (promptPrefab != null)
        {
            spawnedInteractionPrompt = Instantiate(promptPrefab, uiCanvas.transform, false).GetComponent<InteractionPromptUI>();
            spawnedInteractionPrompt.SetSource(spawnedPlayer.GetComponent<PlayerInteractionController>(), camera);
        }
        else Debug.LogError("상호작용 HUD 프리팹이 없습니다: Resources/UI/InteractionPromptHUD", this);

        playerDeathHandled = false;
        playerHealth.OnDeath += HandlePlayerDeath;
        spawnedPlayer.SetActive(true);

        if (!RestorePlayerData(weapons, protection, bagSlots, quickslots, selectedWeaponIndex))
        {
            Debug.LogError("플레이어 데이터를 복원하지 못했습니다.");
            return false;
        }

        playerInventory.RefreshOwnedItemReferences();

        spawnedCrosshair.gameObject.SetActive(true);

        if (isLobbyScene)
        {
            // 로비 씬에서는 타겟 더미를 스폰한다.
            if (targetDummySpawner == null)
            {
                Debug.LogError("TargetDummySpawner가 할당되지 않았습니다.");
                return false;
            }

            if (!targetDummySpawner.TrySpawn(instance))
            {
                Debug.LogError("타겟 더미 스폰에 실패했습니다.");
                return false;
            }
            if (unitHealthBarPresenter != null)
                foreach (var group in targetDummySpawner.SpawnedGroups)
                    unitHealthBarPresenter.Register(group);
        }
            
        if (!isLobbyScene)
        {
            // 로딩이 끝나기 전에 이번 탐험의 상자 출현과 내용물을 한 번 확정한다.
            if (stageLootInitializer == null || !stageLootInitializer.TryInitialize(instance))
            {
                Debug.LogError("스테이지 전리품 초기화에 실패했습니다.");
                return false;
            }
            stageEnemySpawner.SetDataRoot(instance);

            stageEnemySpawner.SetTarget(spawnedPlayer.transform, aimPointProvider.BodyAimPoint, aimPointProvider);

            if (!stageEnemySpawner.TrySpawn())
            {
                Debug.LogError("스테이지 적 스폰에 실패했습니다.");
                return false;
            }
            if (unitHealthBarPresenter != null)
                foreach (var group in stageEnemySpawner.SpawnedGroups)
                    unitHealthBarPresenter.Register(group);
        }

        // [DATA-01 변경] 복원·스폰이 모두 끝난 뒤부터 변경을 감시한다. 최초 저장을 새로 생성하지 않는다.
        lastRequestedContent = GetSaveContent(CaptureCurrentSaveData());
        playerInventory.OnOwnedItemsChanged += HandleOwnedItemsChanged;
        playerHealth.OnDamageTaken += HandleSaveRelevantDamage;
        saveFlowReady = true;
        return true;
    }

    private void ConfigurePlayerFOV()
    {
        playerFOV.enabled = useFOV;

        foreach (FOVEyeDepthRenderer eyeDepth in spawnedPlayer.GetComponentsInChildren<FOVEyeDepthRenderer>(true))
            eyeDepth.enabled = useFOV;

        // 로비에는 마스크 카메라를 배치하지 않아도 된다.
        if (fovMaskRenderer == null)
            return;

        // 촬영을 켜기 전에 추적 대상을 먼저 연결한다.
        fovMaskRenderer.SetSourceFOV(playerFOV);

        // 마스크 텍스처 생성과 갱신 여부를 설정한다.
        fovMaskRenderer.enabled = useFOV;

        // 실제 카메라의 렌더링 여부도 함께 설정한다.
        fovMaskRenderer.GetComponent<Camera>().enabled = useFOV;
    }

    private void DisconnectPlayerFOV()
    {
        // 촬영 카메라가 없는 로비에서는 해제할 대상이 없다.
        if (fovMaskRenderer == null)
            return;

        // 제거될 플레이어의 참조를 해제한다.
        fovMaskRenderer.SetSourceFOV(null);

        // OnDisable을 통해 마스크 RenderTexture를 정리한다.
        fovMaskRenderer.enabled = false;

        // 촬영 카메라의 렌더링도 중단한다.
        fovMaskRenderer.GetComponent<Camera>().enabled = false;
    }

    private bool TryGetPlayerComponent()
    {
        playerController = spawnedPlayer.TryGetComponent<PlayerController>(out var controller) ? controller : null;
        playerInputHandler = spawnedPlayer.TryGetComponent<PlayerInputHandler>(out var inputHandler) ? inputHandler : null;
        playerAimController = spawnedPlayer.TryGetComponent<PlayerAimController>(out var aimController) ? aimController : null;
        playerInventory = spawnedPlayer.TryGetComponent<PlayerInventory>(out var inventory) ? inventory : null;
        playerWeaponHolder = spawnedPlayer.TryGetComponent<PlayerWeaponHolder>(out var weaponHolder) ? weaponHolder : null;
        playerAttackController = spawnedPlayer.TryGetComponent<PlayerAttackController>(out var attackController) ? attackController : null;
        playerReloadController = spawnedPlayer.TryGetComponent<PlayerReloadController>(out var reloadController) ? reloadController : null;
        playerWorldItemDropper = spawnedPlayer.TryGetComponent<PlayerWorldItemDropper>(out var worldItemDropper) ? worldItemDropper : null;
        aimPointProvider = spawnedPlayer.TryGetComponent<AimPointProvider>(out var aimProvider) ? aimProvider : null;
        playerAimCursor = spawnedPlayer.TryGetComponent<PlayerAimCursor>(out var aimCursor) ? aimCursor : null;
        playerHealth = spawnedPlayer.GetComponent<HealthComponent>();
        playerFOV = spawnedPlayer.TryGetComponent<FOV>(out var fov) ? fov : null;
        playerEquipment = spawnedPlayer.TryGetComponent<PlayerEquipment>(out var equipment) ? equipment : null;
        playerItemQuickslots = spawnedPlayer.TryGetComponent<PlayerItemQuickslots>(out var quickslots) ? quickslots : null;

        if (useFOV)
        {
            if (playerFOV == null)
            {
                Debug.LogError("플레이어에 FOV 컴포넌트가 없습니다. 시야 제한을 사용하려면 FOV 컴포넌트를 추가해야 합니다.");
                return false;
            }
        }

        if (playerController == null || playerInputHandler == null || playerAimController == null ||
            playerInventory == null || playerWeaponHolder == null || playerAttackController == null ||
            playerReloadController == null || playerWorldItemDropper == null || aimPointProvider == null ||
            playerAimCursor == null || playerHealth == null || playerEquipment == null || playerItemQuickslots == null)
        {
            return false;
        }

        return true;
    }

    public void SetTargetDummySpawner(TargetDummySpawner spawner)
    {
        targetDummySpawner = spawner;
    }

    public void SetPlayerInputBlocked(bool blocked)
    {
        // [DATA-01 변경] 외부 로딩 완료 호출이 저장 실패·전환 중 잠금을 해제하지 못하게 한다.
        if (!blocked && (transitionPending || saveFailed)) return;
        if (playerInputHandler != null)
            playerInputHandler.SetGameplayInputBlocked(blocked);
        if (blocked && spawnedPlayer != null)
            spawnedPlayer.GetComponent<InventoryUIController>()?.Close();
    }

    private void HandlePlayerDeath(DamageInfo damageInfo)
    {
        if (playerDeathHandled || spawnedPlayer == null)
            return;

        playerDeathHandled = true;
        // [DATA-01 변경] 이전 탈출/로비 저장의 완료 콜백보다 사망 결과가 우선한다.
        int deathOperation = ++saveOperation;
        transitionPending = true;
        saveFailed = false;
        retryInProgress = false;
        HideSaveFailure();
        playerHealth.OnDeath -= HandlePlayerDeath;
        playerHealth.OnDamageTaken -= HandleSaveRelevantDamage;
        playerInventory.OnOwnedItemsChanged -= HandleOwnedItemsChanged;
        SetPlayerInputBlocked(true);
        spawnedPlayer.GetComponent<PlayerStateMachine>().TryChangeState(PlayerActionState.Dead);
        playerController.enabled = false;
        playerAttackController.CancelPendingFire();
        playerReloadController.CancelReload();
        spawnedPlayer.GetComponent<PlayerItemUseController>()?.CancelUse();
        spawnedPlayer.GetComponent<PlayerInteractionController>()?.CancelInteraction();
        spawnedPlayer.GetComponent<PlayerHeldItemController>()?.ClearItemSelection();

        // 파괴 전에 실제 소유 공간과 선택 참조를 비운다. 창고에는 접근하지 않는다.
        spawnedPlayer.GetComponent<PlayerItemQuickslots>()?.ClearForDeath();
        spawnedPlayer.GetComponent<PlayerEquipment>().ClearForDeath();
        playerInventory.ClearForDeath();

        // [DATA-01 변경] 파괴 전에 빈 소지품과 보존할 창고를 값 DTO로 확정한다.
        pendingSaveData = CaptureCurrentSaveData();
        pendingDestination = GameScene.Lobby;
        pendingRequestCreated = false;

        GameObject deathEffect = playerDeathEffectPrefab != null
            ? Instantiate(playerDeathEffectPrefab, spawnedPlayer.transform.position, spawnedPlayer.transform.rotation)
            : null;
        pendingDeathEffect = deathEffect;

        // 플레이어보다 오래 살아 있는 씬 오브젝트는 파괴 전에 연결을 해제한다.
        if (quaterViewCamera != null)
            quaterViewCamera.SetTarget(null, null, null);
        if (spawnedCrosshair != null)
        {
            spawnedCrosshair.SetAimCursor(null);
            Destroy(spawnedCrosshair.gameObject);
            spawnedCrosshair = null;
        }
        if (stageEnemySpawner != null)
            stageEnemySpawner.ClearTarget();
        ClearInteractionPrompt();
        ClearProgressHUD();

        Destroy(spawnedPlayer);
        spawnedPlayer = null;
        playerController = null;
        playerInputHandler = null;
        playerAimController = null;
        playerInventory = null;
        playerWeaponHolder = null;
        playerAttackController = null;
        playerReloadController = null;
        playerWorldItemDropper = null;
        playerAimCursor = null;
        playerFOV = null;
        aimPointProvider = null;
        playerHealth = null;

        DisconnectPlayerFOV();

        SaveAndContinueAsync(deathOperation, false).Forget();
    }

    private bool RestorePlayerData(WeaponEquipment weapons, ProtectionEquipment protection, IInventoryItem[] bagSlots, ItemQuickslots quickslots, int selectedWeaponIndex)
    {
        if (!playerEquipment.TryApplyRestoredEquipment(weapons, protection))
            return false;
        if (!playerInventory.TryApplyRestoredSlots(bagSlots))
            return false;
        if (!playerItemQuickslots.TryApplyRestoredQuickslots(quickslots))
            return false;
        if (!playerEquipment.TrySelectWeaponSlot(selectedWeaponIndex))
            return false;

        return true;
    }

    public bool CanRequestSceneTransition => saveFlowReady && !playerDeathHandled &&
        !transitionPending && !saveFailed && !GameSceneManager.IsLoading &&
        playerInputHandler != null && !playerInputHandler.IsGameplayInputBlocked;

    private void HandleOwnedItemsChanged() => ownedItemsDirty = true;
    private void HandleSaveRelevantDamage(DamageInfo damageInfo) => HandleOwnedItemsChanged();

    private GameSaveData CaptureCurrentSaveData()
    {
        GameSaveData data = SaveDataFactory.CreateNewData<GameSaveData>();
        data.Bag = InventorySaveMapper.CaptureContainer(playerInventory.InventoryContainer);
        data.Chest = isLobbyScene
            ? InventorySaveMapper.CaptureContainer(playerStorage.InventoryContainer)
            : preservedChest;
        data.Equipment = InventorySaveMapper.CaptureEquipment(playerEquipment);
        data.QuickslotsBagIndices = InventorySaveMapper.CaptureQuickslots(
            playerItemQuickslots.Quickslots, playerInventory.InventoryContainer);
        return data;
    }

    private static string GetSaveContent(GameSaveData data)
    {
        // 캡처할 때마다 바뀌는 저장 시각은 제외한다. 실패·단순 클릭·되돌리기는 디스크 쓰기를 만들지 않는다.
        return JsonConvert.SerializeObject(new { data.Bag, data.Chest, data.Equipment, data.QuickslotsBagIndices });
    }

    private void LateUpdate()
    {
        // 주기적인 전체 상태 비교가 아니다. 변경 통지가 있던 프레임에만 최종 소유 상태를 읽는다.
        // 탐험 중 파밍·소비는 저장하지 않아 도중 종료 시 탐험 전 상태로 돌아간다.
        if (!isLobbyScene || !saveFlowReady || !ownedItemsDirty || playerDeathHandled ||
            transitionPending || saveFailed || playerInputHandler == null || playerInputHandler.IsGameplayInputBlocked)
            return;

        ownedItemsDirty = false;
        try
        {
            GameSaveData snapshot = CaptureCurrentSaveData();
            string content = GetSaveContent(snapshot);
            if (content == lastRequestedContent) return;
            lastRequestedContent = content;
            pendingSaveData = snapshot;
            pendingDestination = null;
            pendingDeathEffect = null;
            pendingRequestCreated = false;
            SaveAndContinueAsync(++saveOperation, false).Forget();
        }
        catch (Exception exception)
        {
            ShowSaveFailure(exception);
        }
    }

    public bool TryBeginSceneTransition(GameScene destination)
    {
        if (!CanRequestSceneTransition || destination == GameScene.Loading || destination == GameScene.GameStart)
            return false;

        // 첫 await 전에 중복 요청을 잠그고, 예약된 발사·장전·아이템 사용을 취소한 다음 캡처한다.
        transitionPending = true;
        int operation = ++saveOperation;
        pendingSaveData = null;
        pendingDestination = destination;
        pendingDeathEffect = null;
        pendingRequestCreated = false;
        BlockSaveRelevantActions();
        try
        {
            pendingSaveData = CaptureCurrentSaveData();
            lastRequestedContent = GetSaveContent(pendingSaveData);
            SaveAndContinueAsync(operation, false).Forget();
        }
        catch (Exception exception)
        {
            ShowSaveFailure(exception);
        }
        return true;
    }

    private void BlockSaveRelevantActions()
    {
        SetPlayerInputBlocked(true);
        playerAttackController?.CancelPendingFire();
        playerReloadController?.CancelReload();
        if (spawnedPlayer == null) return;
        spawnedPlayer.GetComponent<PlayerItemUseController>()?.CancelUse();
        spawnedPlayer.GetComponent<PlayerInteractionController>()?.CancelInteraction();
    }

    private async UniTask SaveAndContinueAsync(int operation, bool retry)
    {
        GameSaveData snapshot = pendingSaveData;
        GameScene? destination = pendingDestination;
        GameObject deathEffect = pendingDeathEffect;
        try
        {
            // 직렬화 전에 실패했을 때만 잠긴 런타임에서 다시 캡처한다.
            if (snapshot == null) snapshot = pendingSaveData = CaptureCurrentSaveData();
            UniTask request = retry && pendingRequestCreated
                ? saveService.RetryAsync()
                : saveService.SaveAsync(snapshot);
            pendingRequestCreated = true;
            await request;
            if (this == null || operation != saveOperation) return;

            if (destination.HasValue)
            {
                // 직전 로비 변경 요청까지 모두 디스크에 확정한 뒤 다음 씬이 Load하도록 한다.
                await saveService.FlushAsync();
                if (this == null || operation != saveOperation) return;
                if (playerDeathHandled)
                    await UniTask.WaitUntil(() => deathEffect == null,
                        cancellationToken: this.GetCancellationTokenOnDestroy());
                if (this == null || operation != saveOperation) return;
            }

            saveFailed = false;
            retryInProgress = false;
            HideSaveFailure();
            if (destination.HasValue)
            {
                GameSceneManager.MoveToNextDestination(destination.Value);
            }
            else
            {
                pendingSaveData = null;
                // 보통의 로비 자동 저장은 입력을 잠그지 않는다. 실패 재시도 후에만 해제한다.
                if (retry) SetPlayerInputBlocked(false);
            }
        }
        catch (OperationCanceledException) when (this == null) { }
        catch (Exception exception)
        {
            // 저장 중 사망했거나 언로드된 이전 요청이 현재 흐름을 덮어쓰지 못하게 한다.
            if (this == null || operation != saveOperation) return;
            retryInProgress = false;
            ShowSaveFailure(exception);
        }
    }

    public void RetryFailedSave()
    {
        if (!saveFailed || retryInProgress) return;
        retryInProgress = true;
        if (retrySaveButton != null) retrySaveButton.interactable = false;
        // 소멸한 플레이어를 다시 조회하지 않고 마지막 승인 요청을 재사용한다.
        SaveAndContinueAsync(saveOperation, true).Forget();
    }

    private void ShowSaveFailure(Exception exception)
    {
        saveFailed = true;
        BlockSaveRelevantActions();
        Debug.LogError($"저장 실패. 현재 씬에서 재시도가 필요합니다: {exception}", this);
        EnsureSaveFailureUI();
        if (saveFailurePanel == null) return;
        saveFailurePanel.SetActive(true);
        retrySaveButton.interactable = true;
        // 실패 화면이 닫힐 때 원래 숨김·가두기 상태로 돌아가도록 기존 커서 경계를 사용한다.
        playerAimCursor?.SetUICursorActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void HideSaveFailure()
    {
        // 정상 자동 저장의 완료가 열린 인벤토리의 UI 커서까지 해제하지 않도록 한다.
        if (saveFailurePanel == null || !saveFailurePanel.activeSelf) return;
        saveFailurePanel.SetActive(false);
        playerAimCursor?.SetUICursorActive(false);
    }

    private void EnsureSaveFailureUI()
    {
        if (saveFailurePanel != null || uiCanvas == null) return;
        // 기존 uGUI와 같은 글꼴을 쓰되, 플레이어 사망 후에도 남도록 이 화면이 폰트를 소유한다.
        ownedSaveFailureFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 20);
        Font font = ownedSaveFailureFont != null ? ownedSaveFailureFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        saveFailurePanel = new GameObject("SaveFailure", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.GraphicRaycaster), typeof(UnityEngine.UI.Image));
        RectTransform overlay = (RectTransform)saveFailurePanel.transform;
        overlay.SetParent(uiCanvas.transform, false);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        Canvas canvas = saveFailurePanel.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32760;
        saveFailurePanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.85f);

        GameObject message = new GameObject("Message", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        RectTransform messageRect = (RectTransform)message.transform;
        messageRect.SetParent(overlay, false);
        messageRect.anchorMin = messageRect.anchorMax = new Vector2(0.5f, 0.5f);
        messageRect.anchoredPosition = new Vector2(0f, 45f);
        messageRect.sizeDelta = new Vector2(560f, 110f);
        UnityEngine.UI.Text text = message.GetComponent<UnityEngine.UI.Text>();
        text.font = font;
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.text = "저장하지 못했습니다.\n저장을 다시 시도해주세요.";

        GameObject button = new GameObject("Retry", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        RectTransform buttonRect = (RectTransform)button.transform;
        buttonRect.SetParent(overlay, false);
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -65f);
        buttonRect.sizeDelta = new Vector2(240f, 60f);
        UnityEngine.UI.Image image = button.GetComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.18f, 0.32f, 0.45f, 1f);
        retrySaveButton = button.GetComponent<UnityEngine.UI.Button>();
        retrySaveButton.targetGraphic = image;
        retrySaveButton.onClick.AddListener(RetryFailedSave);

        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.SetParent(buttonRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        UnityEngine.UI.Text labelText = label.GetComponent<UnityEngine.UI.Text>();
        labelText.font = font;
        labelText.fontSize = 22;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.raycastTarget = false;
        labelText.text = "다시 저장";
    }

    private void ClearInteractionPrompt()
    {
        if (spawnedInteractionPrompt == null) return;
        spawnedInteractionPrompt.SetSource(null, null);
        Destroy(spawnedInteractionPrompt.gameObject);
        spawnedInteractionPrompt = null;
    }

    private void OnDestroy()
    {
        saveOperation++;
        if (playerInventory != null)
            playerInventory.OnOwnedItemsChanged -= HandleOwnedItemsChanged;
        if (playerHealth != null)
            playerHealth.OnDamageTaken -= HandleSaveRelevantDamage;
        if (ownedSaveFailureFont != null) Destroy(ownedSaveFailureFont);
        if (saveFailurePanel != null) Destroy(saveFailurePanel);
        ClearInteractionPrompt();
        ClearProgressHUD();
        if (playerHealth != null)
            playerHealth.OnDeath -= HandlePlayerDeath;
    }

    private void ClearProgressHUD()
    {
        if (spawnedProgressHUD == null) return;
        spawnedProgressHUD.SetSources(null, null, null);
        Destroy(spawnedProgressHUD.gameObject);
        spawnedProgressHUD = null;
    }
}
