using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class InventoryUIController
{
    private AmmoSelectionView[] ammoSelectors;
    // 닫을 창은 장비의 새 인덱스가 아니라 실제로 열었던 뷰를 기억한다.
    private AmmoSelectionView openAmmoSelector;
    private PlayerReloadController reload;
    private PlayerStateMachine ammoStateMachine;
    private WeaponInstance selectingAmmoFor;
    private readonly List<AmmoDefinition> ammoChoices = new();
    private int selectedAmmoIndex;
    private bool ammoChoicesDirty;
    private bool IsSelectingAmmo => selectingAmmoFor != null;
    private bool HasSelectedGun
    {
        get
        {
            if (equipment == null) return false;
            int index = equipment.SelectedWeaponSlotIndex;
            WeaponEquipment slots = equipment.WeaponEquipment;
            return slots != null && index >= 0 && index < slots.SlotCount &&
                InventoryHUDReadModel.CanShowAmmoHint(slots.GetItem(index), holder?.CurrentWeaponInstance, SelectedItemIndex >= 0);
        }
    }

    public void ConfigureAmmoSelector(AmmoSelectionView[] view)
    {
        CloseAmmoSelection();
        HideAmmoSelectors();
        ammoSelectors = view;
        reload = GetComponent<PlayerReloadController>();
        ammoStateMachine = GetComponent<PlayerStateMachine>();
        if (inventory != null)
        {
            inventory.OnOwnedItemsChanged -= InvalidateAmmoChoices;
            inventory.OnOwnedItemsChanged += InvalidateAmmoChoices;
        }
        if (ammoSelectors != null)
        {
            for (int i = 0; i < ammoSelectors.Length; i++)
            {
                if (ammoSelectors[i] != null) ammoSelectors[i].gameObject.SetActive(false);
                if (ammoSelectors[i] != null) ammoSelectors[i].HideChoices();
            }
        }
    }

    private void InvalidateAmmoChoices() => ammoChoicesDirty = true;

    private bool TryGetAmmoSelector(int index, out AmmoSelectionView selector)
    {
        selector = null;
        if (ammoSelectors == null || index < 0 || index >= ammoSelectors.Length) return false;
        AmmoSelectionView candidate = ammoSelectors[index];
        if (candidate == null || !candidate.IsConfigured) return false;
        // 같은 뷰가 두 인덱스에 연결되면 다른 슬롯을 끄는 과정에서 선택 뷰도 꺼진다.
        for (int i = 0; i < ammoSelectors.Length; i++)
            if (i != index && ammoSelectors[i] == candidate) return false;
        selector = candidate;
        return true;
    }

    private void HideAmmoSelectors()
    {
        if (ammoSelectors == null) return;
        foreach (AmmoSelectionView selector in ammoSelectors)
            if (selector != null) selector.gameObject.SetActive(false);
    }

    private bool CanSelectAmmo => !IsOpen && !IsGameplayBlocked && !UIManager.IsLoading() &&
        (health == null || !health.IsDead) && HasSelectedGun &&
        reload != null && !reload.IsReloading && ammoStateMachine != null && ammoStateMachine.CurrentState == PlayerActionState.Free;

    private void CloseAmmoSelection()
    {
        AmmoSelectionView opened = openAmmoSelector;
        openAmmoSelector = null;
        selectingAmmoFor = null;
        ammoChoices.Clear();
        ammoChoicesDirty = false;
        if (opened == null) return;
        if (opened.selectionRoot != null) UIManager.Instance?.UnregisterPopup(opened.selectionRoot);
        opened.HideChoices();
    }

    private void OpenAmmoSelection()
    {
        if (!CanSelectAmmo || UIManager.Instance == null || UIManager.Instance.HasOpenPopups ||
            !TryGetAmmoSelector(equipment.SelectedWeaponSlotIndex, out AmmoSelectionView selector)) return;
        openAmmoSelector = selector;
        selectingAmmoFor = holder.CurrentWeaponInstance;
        selectedAmmoIndex = 0;
        RebuildAmmoChoices(0);
        if (ammoChoices.Count == 0) { CloseAmmoSelection(); return; }
        UIManager.Instance.RegisterPopup(selector.selectionRoot, CloseAmmoSelection);
        input?.SetUIInputBlocked(true);
        attack?.CancelPendingFire();
    }

    // 가방의 앞 순서로 후보를 만들고 수량·호환성은 기존 카탈로그/가방에서 읽는다.
    private void RebuildAmmoChoices(int preserveId)
    {
        ammoChoicesDirty = false;
        ammoChoices.Clear();
        InventoryContainer bag = inventory?.InventoryContainer;
        if (bag == null || selectingAmmoFor == null || dataRoot?.AmmoCatalog == null || openAmmoSelector == null) return;
        for (int i = 0; i < bag.SlotCount; i++)
        {
            if (bag.GetSlot(i).Item is not AmmoStack stack || stack.GetCurrentCount() <= 0 ||
                !dataRoot.AmmoCatalog.TryGet(stack.ItemId, out AmmoDefinition ammo) ||
                ammo.ammoType != selectingAmmoFor.AmmoState.CompatibleAmmoType) continue;
            bool duplicate = false;
            for (int j = 0; j < ammoChoices.Count; j++) if (ammoChoices[j].ammoId == ammo.ammoId) { duplicate = true; break; }
            if (!duplicate) ammoChoices.Add(ammo);
        }
        selectedAmmoIndex = 0;
        int preferredId = preserveId > 0 ? preserveId : selectingAmmoFor.AmmoState.LoadedAmmoId;
        for (int i = 0; i < ammoChoices.Count; i++) if (ammoChoices[i].ammoId == preferredId) selectedAmmoIndex = i;
        if (ammoChoices.Count > 0 && !openAmmoSelector.ShowChoices(ammoChoices, bag, selectedAmmoIndex)) ammoChoices.Clear();
    }

    private void UpdateAmmoSelection(Keyboard keyboard)
    {
        WeaponInstance current = holder?.CurrentWeaponInstance;
        int index = equipment != null ? equipment.SelectedWeaponSlotIndex : -1;
        bool validSelector = TryGetAmmoSelector(index, out AmmoSelectionView selector);
        bool visible = validSelector && HasSelectedGun && inventory != null && !IsOpen && !IsGameplayBlocked &&
            !UIManager.IsLoading() && (health == null || !health.IsDead);
        if (IsSelectingAmmo && (!visible || !CanSelectAmmo || openAmmoSelector != selector ||
            !ReferenceEquals(selectingAmmoFor, current) || UIManager.Instance != null &&
            !UIManager.Instance.IsTopPopupWithin(openAmmoSelector != null ? openAmmoSelector.selectionRoot : null))) CloseAmmoSelection();
        if (ammoSelectors != null)
            foreach (AmmoSelectionView view in ammoSelectors)
                if (view != null) view.gameObject.SetActive(visible && view == selector);
        if (!visible) return;
        // 탄종 안내는 실제 장전된 탄약만 표시한다. 마지막 탄종/가방 후보는 예비탄 수량 표시에서만 사용한다.
        AmmoDefinition display = inventory.FindAmmoDefinition(current.AmmoState.LoadedAmmoId);
        selector.SetCurrentName(display != null ? display.displayName : string.Empty);
        if (IsSelectingAmmo && ammoChoicesDirty)
        {
            int selected = selectedAmmoIndex >= 0 && selectedAmmoIndex < ammoChoices.Count ? ammoChoices[selectedAmmoIndex].ammoId : 0;
            RebuildAmmoChoices(selected);
            if (ammoChoices.Count == 0) CloseAmmoSelection();
        }
        if (keyboard == null) return;
        if (IsSelectingAmmo && keyboard.rKey.wasPressedThisFrame)
        {
            // R 요청은 소비하지 않는다. 뒤이어 기존 장전 컨트롤러가 같은 입력으로 보충한다.
            CloseAmmoSelection();
            return;
        }
        if (keyboard.tKey.wasPressedThisFrame)
        {
            if (!IsSelectingAmmo) OpenAmmoSelection();
            else
            {
                int selected = ammoChoices[selectedAmmoIndex].ammoId;
                CloseAmmoSelection();
                reload.TryStartReload(selected);
            }
        }
        if (!IsSelectingAmmo || Mouse.current == null || ammoChoices.Count == 0) return;
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll == 0f) return;
        selectedAmmoIndex = (selectedAmmoIndex + (scroll > 0 ? -1 : 1) + ammoChoices.Count) % ammoChoices.Count;
        if (openAmmoSelector == null || !openAmmoSelector.ShowChoices(ammoChoices, inventory.InventoryContainer, selectedAmmoIndex)) CloseAmmoSelection();
    }

    private void UnloadContextItem()
    {
        IInventoryItem target = dropMenuItem;
        InventoryUIArea area = dropMenuArea;
        int index = dropMenuIndex;
        CloseDropMenu();
        if (IsGameplayBlocked || !IsOpen || health != null && health.IsDead ||
            !ReferenceEquals(GetItem(area, index), target) || target is not WeaponInventoryItem gun) return;
        // 클릭한 총이 장전 대상일 때만 취소한다. 가방의 다른 총 해제는 현재 장전을 건드리지 않는다.
        if (reload != null && reload.IsReloadingWeapon(gun.GetWeapon())) reload.CancelReload();
        inventory.TryUnloadWeapon(gun.GetWeapon(), out _);
    }
}
