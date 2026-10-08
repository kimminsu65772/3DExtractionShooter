using System.Collections.Generic;
using UnityEngine;

// 구체적인 아이템 효과를 모르고 공통 인터페이스를 통해 사용 요청을 전달한다.
[DisallowMultipleComponent]
public class PlayerItemUseController : MonoBehaviour
{
    private readonly List<IItemUseHandler> handlers = new();

    // 실제 담당자의 상태를 읽어 완료·회피 취소 후에도 사용 중 표시가 남지 않게 한다.
    public bool IsUsing
    {
        get
        {
            foreach (IItemUseHandler handler in handlers)
                if (IsAlive(handler) && handler.IsUsing) return true;
            return false;
        }
    }

    private void Awake()
    {
        // 같은 플레이어에 붙은 담당자를 한 번 수집한다. 매 프레임 컴포넌트를 검색하지 않는다.
        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            if (component is IItemUseHandler handler) handlers.Add(handler);
    }

    public bool TryUse(IInventoryItem item)
    {
        if (!isActiveAndEnabled || item == null || IsUsing) return false;
        foreach (IItemUseHandler handler in handlers)
        {
            if (!IsAlive(handler) || !(handler is MonoBehaviour component) || !component.isActiveAndEnabled || !handler.CanHandle(item)) continue;
            // 담당 종류는 맞아도 사용 조건에 실패할 수 있다. 다른 담당자로 우회하지 않는다.
            return handler.TryUse(item);
        }
        return false;
    }

    public void CancelUse()
    {
        // 기존 직접 호출로 시작한 사용도 취소할 수 있도록 실제 사용 중인 담당자를 확인한다.
        foreach (IItemUseHandler handler in handlers)
            if (IsAlive(handler) && handler.IsUsing) handler.CancelUse();
    }

    private static bool IsAlive(IItemUseHandler handler)
    {
        // 인터페이스 참조에 남은, Unity에서 이미 제거된 컴포넌트를 걸러낸다.
        return handler is MonoBehaviour component && component != null;
    }

    private void OnDisable() { CancelUse(); }
}
