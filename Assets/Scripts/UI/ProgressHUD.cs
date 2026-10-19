using UnityEngine;

// UI-01: 표시 위치는 공통 프리팹의 ProgressAnchor가 소유하고 현재 플레이어만 연결한다.
[DisallowMultipleComponent]
public sealed class ProgressHUD : MonoBehaviour
{
    [SerializeField] private InteractionProgressUI interactionProgress;
    [UnityEngine.Serialization.FormerlySerializedAs("reloadProgress")]
    [SerializeField] private ActionProgressUI actionProgress;

    public void SetSources(PlayerInteractionController interaction, PlayerReloadController reload,
        PlayerMedicineUseController medicine)
    {
        if (interactionProgress != null) interactionProgress.SetSource(interaction);
        if (actionProgress != null) actionProgress.SetSources(reload, medicine, interaction);
    }
}
