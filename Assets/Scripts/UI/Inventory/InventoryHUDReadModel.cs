using ExtractionShooter.DataPipeline;

// HUD가 읽을 값을 계산한다. 탄창이나 가방의 수량은 변경하지 않는다.
public static class InventoryHUDReadModel
{
    // 실제 탄종과 마지막 탄종을 우선하고, 장전 이력이 없을 때만 보유한 후보를 찾는다.
    public static int GetDisplayAmmoId(WeaponAmmoState magazine, InventoryContainer bag = null, AmmoCatalog catalog = null)
    {
        if (magazine == null) return 0;
        if (magazine.LoadedAmmoId > 0) return magazine.LoadedAmmoId;
        if (magazine.LastLoadedAmmoId > 0) return magazine.LastLoadedAmmoId;
        if (bag == null || catalog == null) return 0;

        int selectedId = 0;
        int greatestCount = 0;
        var candidates = catalog.GetByType(magazine.CompatibleAmmoType);
        // 현재 재장전과 같은 기준으로 보유량이 가장 많은 호환 탄약을 선택한다.
        // 수량이 같으면 먼저 등록된 후보를 유지하며, 보유하지 않은 후보는 제외한다.
        for (int i = 0; i < candidates.Count; i++)
        {
            int count = bag.GetAmmoCount(candidates[i].ammoId);
            if (count <= greatestCount) continue;
            greatestCount = count;
            selectedId = candidates[i].ammoId;
        }
        return selectedId;
    }

    public static int GetReserve(InventoryContainer bag, WeaponAmmoState magazine, AmmoCatalog catalog = null)
    {
        int id = GetDisplayAmmoId(magazine, bag, catalog);
        return bag == null || id <= 0 ? 0 : bag.GetAmmoCount(id);
    }
}
