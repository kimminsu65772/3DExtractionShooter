using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 기존 프레임의 셀렉터 참조만 연결한다. UI 생성·이동·크기·글꼴·버튼 변경은 하지 않는다.
public static class AmmoUISetup
{
    private const string HudPath = "Assets/Resources/UI/InventoryHUD.prefab";

    [MenuItem("Tools/ExtractionShooter/UI/Connect Ammo Selection")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 실행한다.");
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            InventoryUIView view = stage.prefabContentsRoot.GetComponent<InventoryUIView>();
            if (view != null) Undo.RecordObject(view, "Bind frame ammo selectors");
            int count = BindExisting(view);
            if (EditorUtility.IsDirty(view)) EditorSceneManager.MarkSceneDirty(stage.scene);
            // 열어 둔 프리팹은 다른 편집 내용과 함께 사용자가 저장한다.
            Debug.Log($"프레임별 탄약 셀렉터 {count}개를 확인했습니다. 변경된 참조는 현재 프리팹에서 저장하세요.", view);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            InventoryUIView view = root.GetComponent<InventoryUIView>();
            int count = BindExisting(view);
            if (EditorUtility.IsDirty(view)) PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            Debug.Log($"프레임별 탄약 셀렉터 {count}개 연결 확인: {HudPath}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static int BindExisting(InventoryUIView view)
    {
        if (view == null || view.hotbar == null || view.hotbar.weaponSlots == null || view.hotbar.weaponSlots.Length == 0)
            throw new InvalidOperationException("InventoryUIView와 Hotbar의 Weapon Slots 참조를 먼저 연결하세요.");

        InventorySlotView[] slots = view.hotbar.weaponSlots;
        var result = new AmmoSelectionView[slots.Length];
        var used = new HashSet<AmmoSelectionView>();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) throw new InvalidOperationException($"Weapon Slots[{i}]가 비어 있습니다.");
            // Inspector에서 직접 정한 순서를 우선한다. 비어 있는 칸만 해당 무기 프레임 아래에서 찾는다.
            AmmoSelectionView selector = view.ammoSelectors != null && i < view.ammoSelectors.Length ? view.ammoSelectors[i] : null;
            if (selector == null)
            {
                AmmoSelectionView[] candidates = slots[i].GetComponentsInChildren<AmmoSelectionView>(true);
                if (candidates.Length != 1)
                    throw new InvalidOperationException($"무기 프레임 {i + 1}의 셀렉터가 {candidates.Length}개입니다. 기존 셀렉터 하나를 Ammo Selectors[{i}]에 직접 연결하세요. UI는 자동 생성하지 않습니다.");
                selector = candidates[0];
            }
            if (!selector.transform.IsChildOf(view.transform))
                throw new InvalidOperationException($"Ammo Selectors[{i}]는 이 InventoryHUD 안의 오브젝트여야 합니다.");
            if (!selector.IsConfigured)
                throw new InvalidOperationException($"Ammo Selectors[{i}]의 Current Name, Selection Root, Entry Template 및 템플릿 문구/선택 표시 참조를 확인하세요. 템플릿은 Selection Root 아래에 있어야 합니다.");
            if (!used.Add(selector)) throw new InvalidOperationException($"Ammo Selectors[{i}]가 다른 슬롯과 같은 셀렉터를 참조합니다.");
            result[i] = selector;
        }

        // 모든 칸 검증 후 배열만 수정한다. 실패하면 기존 연결도 그대로 보존한다.
        bool changed = view.ammoSelectors == null || view.ammoSelectors.Length != result.Length;
        if (!changed)
            for (int i = 0; i < result.Length; i++) if (view.ammoSelectors[i] != result[i]) { changed = true; break; }
        if (changed)
        {
            view.ammoSelectors = result;
            EditorUtility.SetDirty(view);
        }
        return result.Length;
    }
}
