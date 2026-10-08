using UnityEngine;

// 바닥의 외형은 표현만 담당하고, 기존 런타임 아이템은 한 칸의 소유 공간에 보관한다.
public class WorldItemPickup : MonoBehaviour, IInteractable
{
    private readonly InventoryContainer contents = new InventoryContainer(1);
    private readonly System.Collections.Generic.List<Mesh> ownedMeshes = new();
    public IInventoryItem Item => contents.GetSlot(0).Item;
    public string InteractionLabel => Item is WeaponInventoryItem gun ? $"획득: {gun.GetWeapon().Definition.displayName}" : Item is ArmorInventoryItem armor ? $"획득: {armor.Armor.Definition.displayName}" : Item is DefinitionInventoryItem defined ? $"획득: {defined.Definition.displayName}" : Item is AmmoStack ammo ? $"획득: {ammo.Definition.displayName}" : "획득";

    public bool TryInitialize(IInventoryItem item) => item != null && contents.GetSlot(0).IsEmpty && contents.TryPlaceInEmptySlot(item, out _);

    // 공유 자산과 달리 BakeMesh로 만든 메시의 수명은 바닥 오브젝트가 소유한다.
    internal void OwnMesh(Mesh mesh) => ownedMeshes.Add(mesh);

    private void OnDestroy()
    {
        foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
    }

    public bool CanInteract(PlayerInteractionController interactor)
    {
        return isActiveAndEnabled && Item != null && interactor != null && interactor.GetComponent<PlayerInventory>() != null;
    }

    public bool TryInteract(PlayerInteractionController interactor)
    {
        if (!CanInteract(interactor)) return false;
        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        if (!inventory.TryAcquireFrom(contents, 0)) return false;
        // 일부만 옮겨졌다면 나머지 스택과 외형을 남긴다. 전부 획득한 경우에만 월드 물체를 제거한다.
        if (Item == null)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
        return true;
    }
}
