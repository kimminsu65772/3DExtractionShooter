using TMPro;
using UnityEngine;

// 표시용 레퍼런스를 빌드에 포함하고, 원본 프리팹을 수정하지 않고 재사용하기 위한 참조 묶음.
public sealed class InventoryReferenceAssets : ScriptableObject
{
    public GameObject inventory;
    public GameObject warehouse;
    public GameObject slot;
    public GameObject weaponSlots;
    public GameObject itemSlots;
    public TMP_FontAsset font;
}
