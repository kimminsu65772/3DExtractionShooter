using System;
using ExtractionShooter.DataPipeline;

public class InventoryAmmoStorage : IAmmoStorage
{
    private readonly InventoryContainer inventory;

    private readonly AmmoCatalog ammoCatalog;

    public InventoryAmmoStorage(InventoryContainer inventory, AmmoCatalog ammoCatalog)
    {
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.ammoCatalog = ammoCatalog ?? throw new ArgumentNullException(nameof(ammoCatalog));
    }

    public int GetCount(int ammoId)
    {
        if (ammoId <= 0) return 0;

        return inventory.GetAmmoCount(ammoId);
    }

    public bool TryRemove(int ammoId, int count)
    {
        if (ammoId <= 0 || count <= 0) return false;

        return inventory.TryRemoveAmmo(ammoId, count);
    }

    public bool TryAdd(int ammoId, int count, out int addedCount)
    {
        addedCount = 0;

        if (ammoId <= 0 || count <= 0) return false;

        if (!ammoCatalog.TryGet(ammoId, out var ammoData)) return false;

        addedCount = inventory.AddAmmoUpTo(ammoData, count);

        return addedCount > 0;
    }
}
