using System;
using UnityEngine;

// 장비 데이터는 소유하지 않고, 현재 장착 ID에 해당하는 외형만 소켓에 부착한다.
[DisallowMultipleComponent]
public sealed class EnemyProtectionView : MonoBehaviour
{
    [Serializable]
    private sealed class VisualEntry
    {
        [Min(1)] public int itemId;
        [Tooltip("장비 외형만 포함한 프리팹을 지정한다.")]
        public GameObject prefab;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
    }

    [Header("장비 데이터 (같은 오브젝트에 있으면 자동 연결)")]
    [SerializeField] private EnemyProtectionLoadout protectionLoadout;

    [Header("외형을 부착할 소켓")]
    [SerializeField] private Transform armorSocket;
    [SerializeField] private Transform helmetSocket;

    [Header("장비 ID별 외형과 소켓 기준 보정")]
    [SerializeField] private VisualEntry[] visualEntries =
    {
        new VisualEntry { itemId = 4001 },
        new VisualEntry { itemId = 4002 },
        new VisualEntry { itemId = 4003 },
        new VisualEntry { itemId = 5001 },
        new VisualEntry { itemId = 5002 },
        new VisualEntry { itemId = 5003 }
    };

    private VisibilityTarget visibilityTarget;
    private GameObject armorVisual;
    private GameObject helmetVisual;
    private int displayedArmorId;
    private int displayedHelmetId;

    private void Awake()
    {
        if (protectionLoadout == null)
            protectionLoadout = GetComponent<EnemyProtectionLoadout>();

        visibilityTarget = GetComponent<VisibilityTarget>();

        if (protectionLoadout == null)
        {
            Debug.LogError("보호 장비 외형에 연결할 EnemyProtectionLoadout이 없다.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (protectionLoadout == null)
            return;

        protectionLoadout.OnProtectionChanged += RefreshVisuals;

        // 비활성 상태에서 장비 구성을 끝낸 스폰과, 나중에 데이터가 준비되는 초기화를 모두 받는다.
        RefreshVisuals();
    }

    private void OnDisable()
    {
        if (protectionLoadout != null)
            protectionLoadout.OnProtectionChanged -= RefreshVisuals;
    }

    private void OnDestroy()
    {
        // 외부 소켓을 연결했더라도 이 컴포넌트가 생성한 외형만 정리한다.
        if (armorVisual != null) Destroy(armorVisual);
        if (helmetVisual != null) Destroy(helmetVisual);
    }

    public void RefreshVisuals()
    {
        if (protectionLoadout == null)
            return;

        bool armorChanged = UpdateVisual(protectionLoadout.GetProtection(HitPart.Body), armorSocket,
            ref armorVisual, ref displayedArmorId);
        bool helmetChanged = UpdateVisual(protectionLoadout.GetProtection(HitPart.Head), helmetSocket,
            ref helmetVisual, ref displayedHelmetId);

        // 새로 부착된 옷과 헬멧도 캐릭터의 현재 시야 표시 상태를 따른다.
        if ((armorChanged || helmetChanged) && visibilityTarget != null)
            visibilityTarget.RefreshRenderers();
    }

    private bool UpdateVisual(ArmorInstance equipment, Transform socket, ref GameObject visual, ref int displayedId)
    {
        int itemId = equipment != null ? equipment.Definition.itemId : 0;

        // 같은 외형을 유지할 때는 다시 생성하지 않는다. 장비 인스턴스와 내구도는 건드리지 않는다.
        if (itemId == displayedId && (itemId == 0 || visual != null))
            return false;

        bool changed = RemoveVisual(ref visual);
        displayedId = 0;

        if (itemId == 0)
            return changed;

        VisualEntry entry = FindVisualEntry(itemId);
        if (socket == null || entry == null || entry.prefab == null)
        {
            Debug.LogWarning($"보호 장비 ID {itemId}의 외형 프리팹 또는 소켓이 설정되지 않았다. 장비 성능은 유지한다.", this);
            return changed;
        }

        visual = Instantiate(entry.prefab, socket, false);
        visual.transform.SetLocalPositionAndRotation(entry.localPosition, Quaternion.Euler(entry.localEulerAngles));
        visual.transform.localScale = Vector3.Scale(entry.prefab.transform.localScale, entry.localScale);

        // 외형 프리팹의 원래 레이어 대신 캐릭터의 표시 레이어를 사용한다.
        foreach (Transform part in visual.GetComponentsInChildren<Transform>(true))
            part.gameObject.layer = gameObject.layer;

        visual.SetActive(true);
        displayedId = itemId;
        return true;
    }

    private VisualEntry FindVisualEntry(int itemId)
    {
        if (visualEntries == null)
            return null;

        // 장비가 바뀔 때만 소수의 ID 매핑을 검색한다. 프레임별 탐색은 하지 않는다.
        foreach (VisualEntry entry in visualEntries)
            if (entry != null && entry.itemId == itemId)
                return entry;

        return null;
    }

    private bool RemoveVisual(ref GameObject visual)
    {
        if (visual == null)
            return false;

        visual.SetActive(false);
        // Destroy는 프레임 끝에 처리되므로 표시 목록을 다시 수집하기 전에 계층에서 분리한다.
        visual.transform.SetParent(null, false);
        Destroy(visual);
        visual = null;
        return true;
    }
}
