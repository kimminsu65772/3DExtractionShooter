using System;
using UnityEngine;

// INV-02: 장착 데이터를 읽어 외형만 표시한다. 아이템 생성·소유권·내구도는 담당하지 않는다.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerEquipment))]
public sealed class PlayerProtectionView : MonoBehaviour
{
    [Serializable]
    private sealed class VisualEntry
    {
        [Min(1)] public int itemId;
        public GameObject prefab;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
    }

    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private Transform armorSocket;
    [SerializeField] private Transform helmetSocket;
    [SerializeField] private VisualEntry[] visualEntries = Array.Empty<VisualEntry>();

    private GameObject armorVisual;
    private GameObject helmetVisual;
    private int displayedArmorId;
    private int displayedHelmetId;

    private void Awake()
    {
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
    }

    private void OnEnable()
    {
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
        if (equipment == null) return;
        equipment.OnProtectionChanged += RefreshVisuals;
        // 활성화 전에 복원된 장비와, 활성화 후 이벤트로 복원되는 장비를 모두 받는다.
        RefreshVisuals();
    }

    private void OnDisable()
    {
        if (equipment != null) equipment.OnProtectionChanged -= RefreshVisuals;
        ClearVisuals();
    }

    private void OnDestroy()
    {
        ClearVisuals();
    }

    private void RefreshVisuals()
    {
        if (!isActiveAndEnabled || equipment == null) return;
        UpdateVisual(equipment.ProtectionEquipment.Armor, armorSocket, ref armorVisual, ref displayedArmorId);
        UpdateVisual(equipment.ProtectionEquipment.Helmet, helmetSocket, ref helmetVisual, ref displayedHelmetId);
    }

    private void UpdateVisual(ArmorInventoryItem item, Transform socket, ref GameObject visual, ref int displayedId)
    {
        int itemId = item != null ? item.ItemId : 0;
        // 같은 ID의 새 인스턴스나 내구도 0도 현재 장착 상태 그대로 표시한다.
        if (itemId == displayedId && (itemId == 0 || visual != null)) return;

        RemoveVisual(ref visual);
        displayedId = 0;
        if (itemId == 0) return;

        VisualEntry entry = FindEntry(itemId);
        if (socket == null || entry == null || entry.prefab == null)
        {
            Debug.LogWarning($"플레이어 보호구 ID {itemId}의 외형 또는 소켓이 없다.", this);
            return;
        }

        visual = Instantiate(entry.prefab, socket, false);
        visual.transform.SetLocalPositionAndRotation(entry.localPosition, Quaternion.Euler(entry.localEulerAngles));
        visual.transform.localScale = Vector3.Scale(entry.prefab.transform.localScale, entry.localScale);

        // 외형도 플레이어 표시 레이어를 따르고, 조준·탄환의 물리 판정을 막지 않는다.
        foreach (Transform part in visual.GetComponentsInChildren<Transform>(true))
            part.gameObject.layer = gameObject.layer;
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        visual.SetActive(true);
        displayedId = itemId;
    }

    private VisualEntry FindEntry(int itemId)
    {
        if (visualEntries == null) return null;
        // 장비 변경 시에만 27개 매핑을 확인한다. 프레임별 검색은 하지 않는다.
        foreach (VisualEntry entry in visualEntries)
            if (entry != null && entry.itemId == itemId) return entry;
        return null;
    }

    private void ClearVisuals()
    {
        RemoveVisual(ref armorVisual);
        RemoveVisual(ref helmetVisual);
        displayedArmorId = displayedHelmetId = 0;
    }

    private void RemoveVisual(ref GameObject visual)
    {
        if (visual == null) return;
        visual.SetActive(false);
        // 사망·해제 시 즉시 숨긴 뒤, 외부 소켓에 연결된 경우에도 남지 않게 정리한다.
        visual.transform.SetParent(null, false);
        Destroy(visual);
        visual = null;
    }
}
