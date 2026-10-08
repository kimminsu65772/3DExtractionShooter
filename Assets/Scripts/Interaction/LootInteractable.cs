using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(LootContainer))]
public class LootInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private LootContainer lootContainer;
    [SerializeField] private string interactionLabel = "열기";
    // 인스펙터에서 기존 팝업의 열기 함수 등을 연결한다. 보관함이나 아이템을 새로 생성하지 않는다.
    [SerializeField] private UnityEvent onOpened = new UnityEvent();

    public string InteractionLabel => interactionLabel;
    public LootContainer LootContainer => lootContainer;
    // UI 연결 코드는 누가 어떤 상자를 열었는지 전달받는다.
    public event Action<PlayerInteractionController, LootContainer> OnOpened;

    private void Awake()
    {
        if (lootContainer == null) lootContainer = GetComponent<LootContainer>();
    }

    public bool CanInteract(PlayerInteractionController interactor)
    {
        // 초기 내용물 지급은 별도 단계에서 처리한다. 여기에서는 유효한 보관함만 확인한다.
        return isActiveAndEnabled && interactor != null && lootContainer != null && lootContainer.InventoryContainer != null;
    }

    public bool TryInteract(PlayerInteractionController interactor)
    {
        if (!CanInteract(interactor)) return false;
        // 열기 요청만 전달한다. UI가 없는 지금 단계에서 게임 정지나 가방 이동을 임의로 수행하지 않는다.
        OnOpened?.Invoke(interactor, lootContainer);
        onOpened.Invoke();
        return true;
    }
}
