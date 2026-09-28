using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAmmoStorage : MonoBehaviour, IAmmoStorage
{
    [Serializable]
    private struct StartingAmmoEntry
    {
        public int ammoId;
        [Min(0)] public int amount;
    }

    [Header("테스트용 초기 예비 탄약")]
    [SerializeField] private StartingAmmoEntry[] startingAmmo = Array.Empty<StartingAmmoEntry>();

    private readonly Dictionary<int, int> ammoCounts = new();

    // 첫 번째 값은 ammoId, 두 번째 값은 변경 후 보유 수량이다.
    public event Action<int, int> OnAmmoCountChanged;

    private void Awake()
    {
        BuildStartingAmmo();
    }

    public int GetCount(int ammoId)
    {
        return ammoId > 0 && ammoCounts.TryGetValue(ammoId, out int count) ? count : 0;
    }

    public bool TryAdd(int ammoId, int amount)
    {
        if (ammoId <= 0 || amount <= 0)
            return false;

        int currentCount = GetCount(ammoId);
        if (currentCount > int.MaxValue - amount)
            return false;

        int nextCount = currentCount + amount;
        ammoCounts[ammoId] = nextCount;
        OnAmmoCountChanged?.Invoke(ammoId, nextCount);
        return true;
    }

    public bool TryRemove(int ammoId, int amount)
    {
        if (ammoId <= 0 || amount <= 0 || !ammoCounts.TryGetValue(ammoId, out int currentCount) || currentCount < amount)
            return false;

        int nextCount = currentCount - amount;
        if (nextCount == 0)
            ammoCounts.Remove(ammoId);
        else
            ammoCounts[ammoId] = nextCount;

        OnAmmoCountChanged?.Invoke(ammoId, nextCount);
        return true;
    }

    private void BuildStartingAmmo()
    {
        ammoCounts.Clear();

        foreach (StartingAmmoEntry entry in startingAmmo)
        {
            if (entry.ammoId <= 0 || entry.amount < 0)
            {
                Debug.LogWarning($"초기 탄약 설정이 올바르지 않습니다. AmmoId: {entry.ammoId}, Amount: {entry.amount}", this);
                continue;
            }

            if (ammoCounts.ContainsKey(entry.ammoId))
            {
                Debug.LogWarning($"초기 탄약 설정에 중복된 AmmoId가 있습니다. AmmoId: {entry.ammoId}", this);
                continue;
            }

            if (entry.amount > 0)
                ammoCounts.Add(entry.ammoId, entry.amount);
        }
    }
}
