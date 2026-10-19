using UnityEngine;

// 기존 진행 바 프리팹에 상호작용 데이터만 연결한다. 배치와 크기는 프리팹이 소유한다.
public class InteractionProgressUI : MonoBehaviour
{
    [SerializeField] private GameObject progressRoot;
    [SerializeField] private UnityEngine.UI.Image progressFill;
    private PlayerInteractionController interaction;

    public void SetSource(PlayerInteractionController source)
    {
        if (interaction != null)
        {
            interaction.OnInteractionStateChanged -= HandleStateChanged;
            interaction.OnInteractionProgressChanged -= HandleProgressChanged;
        }

        interaction = source;
        if (interaction != null && isActiveAndEnabled)
        {
            interaction.OnInteractionStateChanged += HandleStateChanged;
            interaction.OnInteractionProgressChanged += HandleProgressChanged;
        }
        RefreshView();
    }

    private void OnEnable()
    {
        SetSource(interaction);
    }

    private void OnDisable()
    {
        if (interaction != null)
        {
            interaction.OnInteractionStateChanged -= HandleStateChanged;
            interaction.OnInteractionProgressChanged -= HandleProgressChanged;
        }
        if (progressRoot != null) progressRoot.SetActive(false);
    }

    private void HandleStateChanged(bool active)
    {
        RefreshView();
    }

    private void HandleProgressChanged(float progress)
    {
        RefreshView();
    }

    private void RefreshView()
    {
        bool isInteracting = interaction != null && interaction.IsInteracting;
        float progress = isInteracting ? interaction.InteractionProgress : 0f;
        if (progressFill != null) progressFill.fillAmount = progress;
        if (progressRoot != null) progressRoot.SetActive(isActiveAndEnabled && isInteracting);
    }
}
