using ExtractionShooter.DataPipeline;
using UnityEngine;

// 적이 소유한 보호 장비를 보관하고 피격 부위별 보호 정보를 제공한다.
[DisallowMultipleComponent]
public class EnemyProtectionLoadout : MonoBehaviour, IProtectionProvider
{
    [SerializeField] private CombatDataCompositionRoot dataRoot;
    [Tooltip("방어구 카탈로그의 ID를 지정한다. 0이면 미장착이다.")]
    [SerializeField, Min(0)] private int initialArmorId;
    [Tooltip("헬멧 카탈로그의 ID를 지정한다. 0이면 미장착이다.")]
    [SerializeField, Min(0)] private int initialHelmetId;

    private readonly ProtectionEquipment equipment = new();
    public ArmorInventoryItem Armor => equipment.Armor;
    public ArmorInventoryItem Helmet => equipment.Helmet;
    public bool IsReady { get; private set; }

    private void OnEnable()
    {
        // 무기와 같은 데이터 공급자를 사용하되 별개의 장비 상태를 소유한다.
        if (dataRoot == null) dataRoot = GetComponent<EnemyWeaponLoadout>()?.DataRoot;
        if (IsReady) return;
        if (initialArmorId == 0 && initialHelmetId == 0)
        {
            IsReady = true;
            return;
        }
        if (dataRoot == null)
        {
            Debug.LogError("적 보호 장비에 전투 데이터 참조가 없다.", this);
            return;
        }
        dataRoot.OnInitialized += HandleDataInitialized;
        if (dataRoot.isReady) HandleDataInitialized();
    }

    private void OnDisable()
    {
        if (dataRoot != null) dataRoot.OnInitialized -= HandleDataInitialized;
        // 잠시 비활성화되어도 내구도를 초기화하거나 드롭된 객체를 수정하지 않는다.
    }

    private void HandleDataInitialized()
    {
        if (!IsReady && !TryInitialize(initialArmorId, initialHelmetId))
            Debug.LogError("적 방어구 또는 헬멧 ID를 카탈로그에서 찾지 못했다.", this);
    }

    public bool TryInitialize(int armorId, int helmetId)
    {
        if (IsReady || armorId < 0 || helmetId < 0) return false;
        ArmorDefinition armor = null;
        HelmetDefinition helmet = null;
        if (armorId != 0 || helmetId != 0)
        {
            if (dataRoot == null || !dataRoot.isReady) return false;
            if (armorId != 0 && (dataRoot.ArmorCatalog == null || !dataRoot.ArmorCatalog.TryGet(armorId, out armor))) return false;
            if (helmetId != 0 && (dataRoot.HelmetCatalog == null || !dataRoot.HelmetCatalog.TryGet(helmetId, out helmet))) return false;
        }
        // 둘 다 검증한 뒤 공개해 잘못된 헬멧 ID 때문에 방어구만 지급되는 것을 막는다.
        var armorItem = armor != null ? new ArmorInventoryItem(new ArmorInstance(armor)) : null;
        var helmetItem = helmet != null ? new ArmorInventoryItem(new ArmorInstance(helmet)) : null;
        equipment.SetItem(ItemType.Armor, armorItem);
        equipment.SetItem(ItemType.Helmet, helmetItem);
        IsReady = true;
        return true;
    }

    public ArmorInstance GetProtection(HitPart hitPart)
    {
        return hitPart == HitPart.Head ? Helmet?.Armor : Armor?.Armor;
    }
}
