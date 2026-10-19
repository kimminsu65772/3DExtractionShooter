using System;
using System.Collections.Generic;
using ExtractionShooter.DataPipeline;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

// 인벤토리·전리품·상세·우클릭 창과 커서/팝업 수명을 관리한다. Unity 컴포넌트는 기존 InventoryUIController 하나다.
public partial class InventoryUIController
{
    public void ShowDropContextMenu(InventorySlotView slot, Vector2 position)
    {
        if (IsGameplayBlocked) return;
        if (!IsOpen || slot == null || dragItem != null || !CanDropFrom(slot.Area)) return;
        IInventoryItem item = GetItem(slot.Area, slot.Index);
        if (item == null) return;
        dropMenuArea = slot.Area; dropMenuIndex = slot.Index; dropMenuItem = item;
        if (dropMenu == null) { Debug.LogError("우클릭 메뉴 프리팹 참조가 필요합니다.", this); return; }
        dropMenu.SetWeaponActions(item is WeaponInventoryItem, item is WeaponInventoryItem gun && !gun.GetWeapon().AmmoState.IsEmpty);
        dropMenu.Show(position, uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCanvas.worldCamera);
        UIManager.Instance?.RegisterPopup(dropMenu.gameObject, CloseDropMenu);
    }

    private void CloseDropMenu()
    {
        dropMenuItem = null;
        if (dropMenu != null)
        {
            UIManager.Instance?.UnregisterPopup(dropMenu.gameObject);
            dropMenu.Hide();
        }
    }

    public void OpenLoot(LootContainer loot)
    {
        if (IsGameplayBlocked) return;
        if (UIManager.IsLoading() || (UIManager.Instance != null && UIManager.Instance.HasOpenPopups &&
            !UIManager.Instance.IsTopPopupWithin(uiCanvas.gameObject))) return;
        if (loot == null || !loot.TryInitializeContents(dataRoot)) return;
        CancelDrag();
        openedLoot = loot;
        openedLootColliders = loot.GetComponentsInChildren<Collider>();
        previousLootUsed = -1;
        selectedItem = null;
        SetOpen(true);
        lootPanel.SetActive(true);
        RefreshViews();
    }

    public void Close() { CloseAmmoSelection(); SetOpen(false); }

    private void SetOpen(bool open)
    {
        if (open && (IsGameplayBlocked || UIManager.IsLoading())) return;
        if (open) CloseAmmoSelection();
        if (!open) { hoveredSlot = null; CloseDropMenu(); }
        if (windows == null) { SetCursorPresentation(false); return; }
        windows.SetActive(open);
        if (open) UIManager.Instance?.RegisterPopup(windows, Close);
        else UIManager.Instance?.UnregisterPopup(windows);
        bool hasPopup = UIManager.Instance != null && UIManager.Instance.HasOpenPopups;
        SetCursorPresentation(open || hasPopup);
        input?.SetUIInputBlocked(open || hasPopup);
        if (open) attack?.CancelPendingFire();
        if (!open)
        {
            CloseLoot();
            CloseDetail();
            CancelDrag();
        }
    }

    private void CloseDetail()
    {
        // 표시만 끄면 RefreshDetail이 선택된 물건을 보고 다시 열기 때문에 선택도 해제한다.
        selectedItem = null;
        SetDetailVisible(false);
    }

    private void SetDetailVisible(bool visible)
    {
        if (detailPanel == null) return;
        bool wasVisible = detailPanel.activeSelf;
        detailPanel.SetActive(visible);
        if (visible)
        {
            if (!wasVisible) UIManager.Instance?.RegisterPopup(detailPanel, CloseDetail);
        }
        else UIManager.Instance?.UnregisterPopup(detailPanel);
    }

    private void SetCursorPresentation(bool open)
    {
        if (isCursorPresentationOpen == open) return;
        isCursorPresentationOpen = open;
        aimCursor?.SetUICursorActive(open);
        if (crosshairRoot == null) return;
        if (open)
        {
            // 원래 숨겨져 있던 조준점을 창을 닫을 때 임의로 켜지 않는다.
            crosshairWasActive = crosshairRoot.activeSelf;
            crosshairRoot.SetActive(false);
        }
        else crosshairRoot.SetActive(crosshairWasActive);
    }

    private void CloseLoot()
    {
        // 원본 데이터는 드래그 시작 때 꺼내지 않았으므로 취소할 때 복구 작업 자체가 필요 없다.
        CancelDrag();
        openedLoot = null;
        openedLootColliders = null;
        if (lootPanel != null) lootPanel.SetActive(false);
        if (selectedItem != null && !PlayerOwns(selectedItem)) selectedItem = null;
    }

    private bool ValidateLootRange()
    {
        if (ReferenceEquals(openedLoot, null)) return true;
        if (openedLoot == null || !openedLoot.isActiveAndEnabled || interaction == null || !interaction.isActiveAndEnabled || openedLootColliders == null) return false;
        // 열어 둔 상자는 바라보는 방향이 바뀌어도 유지하고, 실제 표면까지의 거리가 멀어지면 닫는다.
        foreach (Collider collider in openedLootColliders)
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy && interaction.IsWithinInteractionDistance(collider)) return true;
        return false;
    }
}
