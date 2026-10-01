using UnityEngine;
using ExtractionShooter.DataPipeline;

public class PlayerInventory : MonoBehaviour
{
    [Header("기본 칸 수 설정")]
    [SerializeField] private int baseCapacity = 16;
    [SerializeField] private CombatDataCompositionRoot dataRoot;

    private InventoryContainer inventoryContainer;
    private InventoryAmmoStorage ammoStorage;

    public InventoryContainer InventoryContainer => inventoryContainer;
    public InventoryAmmoStorage AmmoStorage => ammoStorage;

    private void Awake()
    {
        inventoryContainer = new InventoryContainer(baseCapacity);
    }

    private void OnEnable()
    {
        if (dataRoot == null)
        {
            Debug.LogError("PlayerInventory의 DataRoot를 연결해주세요.", this);
            return;
        }

        dataRoot.OnInitialized += HandleDataInitialized;

        // 데이터가 먼저 준비됐다면 등록한 이벤트가 호출되지 않으므로, 준비 상태를 확인하고 직접 호출하도록 한다.
        if (dataRoot.isReady)
            HandleDataInitialized();
    }

    private void OnDisable()
    {
        if (dataRoot != null)
            dataRoot.OnInitialized -= HandleDataInitialized;
    }

    private void HandleDataInitialized()
    {
        if (ammoStorage != null)
            return;

        if (dataRoot.AmmoCatalog == null)
        {
            Debug.LogError("PlayerInventory의 DataRoot에 AmmoCatalog가 연결되지 않았습니다.", this);
            return;
        }

        ammoStorage = new InventoryAmmoStorage(inventoryContainer, dataRoot.AmmoCatalog);
    }

#if UNITY_EDITOR

    // 컴포넌트 메뉴에서 직접 선택했을 때 이 메서드를 실행합니다.
    [ContextMenu("검증/소총탄 90발 지급")]
    private void AddAmmoForTesting()
    {
        // 플레이 중이 아니거나 데이터 연결이 끝나지 않았다면 지급하지 않습니다.
        if (!Application.isPlaying || ammoStorage == null)
        {
            Debug.LogWarning("플레이 중이고 탄약 보관소가 준비되어야 합니다.", this);
            return;
        }

        // CSV에 등록된 소총탄을 실제 가방에 가능한 수량만큼 추가합니다.
        ammoStorage.TryAdd(2001, 90, out int addedCount);

        // 요청량이 아니라 실제 추가량과 현재 가방 보유량을 확인합니다.
        Debug.Log($"추가: {addedCount}발 / 가방: {ammoStorage.GetCount(2001)}발", this);
    }

    [ContextMenu("검증/탄약량 확인")]
    private void PrintAmmoForTesting()
    {
        // 준비되지 않은 가방에는 접근하지 않습니다.
        if (!Application.isPlaying || ammoStorage == null)
            return;

        // 같은 플레이어에 붙은 무기 홀더를 조회합니다.
        PlayerWeaponHolder holder = GetComponent<PlayerWeaponHolder>();

        // 무기가 있다면 현재 탄창 상태를 가져옵니다.
        WeaponAmmoState magazine = holder != null ? holder.CurrentAmmoState : null;

        // 무기가 없으면 장탄 수는 0으로 표시합니다.
        int loadedCount = magazine != null ? magazine.LoadedAmmoCount : 0;

        // 가방의 예비 탄약과 총 안의 탄약을 따로 표시합니다.
        Debug.Log($"가방 소총탄: {ammoStorage.GetCount(2001)}발 / 탄창: {loadedCount}발", this);
    }

#endif
}
