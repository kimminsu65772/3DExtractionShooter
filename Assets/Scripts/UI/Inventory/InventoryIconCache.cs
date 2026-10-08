using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// 같은 주소의 그림을 한 번만 요청한다. 슬롯이 바뀌어도 로드 콜백이 이전 슬롯을 덮어쓰지 않는다.
public class InventoryIconCache
{
    private readonly Dictionary<string, AsyncOperationHandle<Sprite>> handles = new();

    public Sprite Get(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        if (!handles.TryGetValue(address, out AsyncOperationHandle<Sprite> handle))
        {
            handle = Addressables.LoadAssetAsync<Sprite>(address);
            handles.Add(address, handle);
        }
        return handle.IsValid() && handle.IsDone && handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
    }

    public void Release()
    {
        // 완료와 실패, 아직 진행 중인 요청까지 이 UI가 소유한 핸들만 해제한다.
        foreach (AsyncOperationHandle<Sprite> handle in handles.Values)
            if (handle.IsValid()) Addressables.Release(handle);
        handles.Clear();
    }
}
