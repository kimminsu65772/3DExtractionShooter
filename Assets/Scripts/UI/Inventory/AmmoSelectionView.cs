using System.Collections.Generic;
using TMPro;
using UnityEngine;

// UI-01: 원본 BulletTypeHUD에서 복원한 계층을 참조한다. 배치·폰트는 프리팹에 저장한다.
public sealed class AmmoSelectionView : MonoBehaviour
{
    public TMP_Text currentName;
    public GameObject selectionRoot;
    public AmmoSelectionEntryView entryTemplate;
    private readonly List<AmmoSelectionEntryView> entries = new();
    public bool IsConfigured => currentName != null && selectionRoot != null && selectionRoot != gameObject &&
        entryTemplate != null && entryTemplate.IsConfigured && entryTemplate.transform.IsChildOf(selectionRoot.transform);

    public void SetCurrentName(string value)
    {
        if (currentName != null && currentName.text != value) currentName.text = value;
    }

    public bool ShowChoices(IReadOnlyList<AmmoDefinition> choices, InventoryContainer bag, int selected)
    {
        if (!IsConfigured || choices == null || bag == null || selected < 0 || selected >= choices.Count)
        {
            HideChoices();
            return false;
        }
        for (int i = 0; i < choices.Count; i++)
            if (choices[i] == null) { HideChoices(); return false; }
        while (entries.Count < choices.Count)
            entries.Add(Instantiate(entryTemplate, entryTemplate.transform.parent));
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] == null && i < choices.Count) entries[i] = Instantiate(entryTemplate, entryTemplate.transform.parent);
            if (entries[i] == null) continue;
            entries[i].gameObject.SetActive(i < choices.Count);
            if (i < choices.Count && !entries[i].Show(choices[i], bag.GetAmmoCount(choices[i].ammoId), i == selected))
            {
                HideChoices();
                return false;
            }
        }
        selectionRoot.SetActive(true);
        return true;
    }

    public void HideChoices()
    {
        if (selectionRoot != null) selectionRoot.SetActive(false);
    }
}
