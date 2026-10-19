using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UI-01: 재장전·회복 컨트롤러의 실제 진행 정보를 기존 링과 텍스트에 표시한다.
[DisallowMultipleComponent]
public sealed class ActionProgressUI : MonoBehaviour
{
    [SerializeField] private GameObject progressRoot;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text remainingTimeText;
    [SerializeField] private TMP_Text actionStateText;

    private PlayerReloadController reload;
    private PlayerMedicineUseController medicine;
    private PlayerInteractionController interaction;

    public void SetSources(PlayerReloadController reloadSource, PlayerMedicineUseController medicineSource,
        PlayerInteractionController interactionSource)
    {
        Unsubscribe();
        reload = reloadSource;
        medicine = medicineSource;
        interaction = interactionSource;
        if (isActiveAndEnabled) Subscribe();
        RefreshView();
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshView();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (progressRoot != null) progressRoot.SetActive(false);
    }

    private void Subscribe()
    {
        if (reload != null)
        {
            reload.OnReloadStateChanged += HandleStateChanged;
            reload.OnReloadProgressChanged += HandleProgressChanged;
        }
        if (medicine != null)
        {
            medicine.OnUseStateChanged += HandleStateChanged;
            medicine.OnUseProgressChanged += HandleProgressChanged;
        }
        if (interaction != null) interaction.OnInteractionStateChanged += HandleStateChanged;
    }

    private void Unsubscribe()
    {
        if (reload != null)
        {
            reload.OnReloadStateChanged -= HandleStateChanged;
            reload.OnReloadProgressChanged -= HandleProgressChanged;
        }
        if (medicine != null)
        {
            medicine.OnUseStateChanged -= HandleStateChanged;
            medicine.OnUseProgressChanged -= HandleProgressChanged;
        }
        if (interaction != null) interaction.OnInteractionStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(bool active) => RefreshView();
    private void HandleProgressChanged(float progress) => RefreshView();

    private void RefreshView()
    {
        // 보통 동시에 시작할 수 없지만, 두 상태가 모두 켜지면 기존 재장전 표시를 우선한다.
        bool reloading = reload != null && reload.IsReloading;
        bool healing = !reloading && medicine != null && medicine.IsUsing;
        bool active = isActiveAndEnabled && (reloading || healing) &&
            (interaction == null || !interaction.IsInteracting);
        float progress = reloading ? reload.ReloadProgress : healing ? medicine.UseProgress : 0f;
        float remaining = reloading ? reload.ReloadRemainingTime : healing ? medicine.UseRemainingTime : 0f;
        if (progressFill != null) progressFill.fillAmount = progress;
        if (remainingTimeText != null)
            remainingTimeText.SetText("{0:1}s", remaining);
        string action = reloading ? "재장전 중" : healing ? "회복 중" : string.Empty;
        if (actionStateText != null && actionStateText.text != action) actionStateText.SetText(action);
        if (progressRoot != null) progressRoot.SetActive(active);
    }
}
