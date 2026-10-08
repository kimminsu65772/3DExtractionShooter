using UnityEngine;
using System;
using System.Collections.Generic;

public class PlayerCarryWeight : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField, Min(0.01f)] private float maximumWeight = 45f;
    [SerializeField, Min(1.01f)] private float movementBlockedRatio = 1.31f;
    [SerializeField, Range(0.01f, 1f)] private float overloadedMovementMultiplier = 0.5f;
    private readonly HashSet<object> countedItems = new();
    private Func<int, AmmoDefinition> findAmmo;

    public float MaximumWeight => maximumWeight;
    public float CurrentWeight => InventoryWeightCalculator.Calculate(inventory?.InventoryContainer, equipment?.WeaponEquipment, equipment?.ProtectionEquipment, findAmmo, countedItems);
    public float MovementMultiplier => InventoryWeightCalculator.GetMovementMultiplier(CurrentWeight, maximumWeight, movementBlockedRatio, overloadedMovementMultiplier);

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
        if (inventory != null) findAmmo = inventory.FindAmmoDefinition;
    }

    private void OnValidate()
    {
        maximumWeight = Mathf.Max(0.01f, maximumWeight);
        movementBlockedRatio = Mathf.Max(1.01f, movementBlockedRatio);
        overloadedMovementMultiplier = Mathf.Clamp(overloadedMovementMultiplier, 0.01f, 1f);
    }
}
