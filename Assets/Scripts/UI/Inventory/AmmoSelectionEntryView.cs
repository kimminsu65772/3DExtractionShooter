using TMPro;
using UnityEngine;

public sealed class AmmoSelectionEntryView : MonoBehaviour
{
    public TMP_Text nameText, countText;
    public GameObject selectedMarker;
    public UnityEngine.UI.Image background;
    public Color normalColor = new Color(0, 0, 0, 0.7f);
    public Color selectedColor = new Color(0.12f, 0.35f, 0.5f, 0.95f);
    public bool IsConfigured => nameText != null && countText != null && selectedMarker != null;

    public bool Show(AmmoDefinition ammo, int count, bool selected)
    {
        if (ammo == null || !IsConfigured) return false;
        nameText.text = ammo.displayName;
        countText.text = count.ToString();
        selectedMarker.SetActive(selected);
        if (background != null) background.color = selected ? selectedColor : normalColor;
        return true;
    }
}
