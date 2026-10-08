using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 화면을 편집 가능한 씬 오브젝트로 미리 만들 때만 실행한다. 기존 씬을 자동 저장하지 않는다.
public static class InventoryUISetup
{
    [MenuItem("Tools/Inventory/현재 씬 UI 준비")]
    public static void PrepareScene()
    {
        if (Application.isPlaying) return;
        PlayerInventory player = Object.FindFirstObjectByType<PlayerInventory>();
        if (player == null) { Debug.LogWarning("PlayerInventory가 있는 씬을 연다."); return; }
        InventoryUIController controller = player.GetComponent<InventoryUIController>();
        if (controller == null) controller = Undo.AddComponent<InventoryUIController>(player.gameObject);
        if (player.GetComponent<PlayerInteractionController>() == null) Undo.AddComponent<PlayerInteractionController>(player.gameObject);
        // 새 UI를 준비할 때 등록 모델도 같은 플레이어에 연결한다.
        if (player.GetComponent<PlayerItemQuickslots>() == null) Undo.AddComponent<PlayerItemQuickslots>(player.gameObject);
        controller.BuildView();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Selection.activeGameObject = controller.SlotTemplate != null ? controller.SlotTemplate.transform.root.gameObject : player.gameObject;
    }

    [MenuItem("Tools/Inventory/플레이어 앞 튜토리얼 상자 생성")]
    public static void CreateTutorialChest()
    {
        if (Application.isPlaying) return;
        PlayerInventory player = Object.FindFirstObjectByType<PlayerInventory>();
        if (player == null) return;
        GameObject chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(chest, "튜토리얼 상자 생성");
        chest.name = "Tutorial Loot Chest";
        chest.transform.position = player.transform.position + player.transform.forward * 1.2f + Vector3.up * 0.1f;
        chest.transform.localScale = new Vector3(0.7f, 0.6f, 0.7f);
        LootContainer container = chest.AddComponent<LootContainer>();
        chest.AddComponent<LootInteractable>();
        SerializedObject settings = new SerializedObject(container);
        settings.FindProperty("containerSize").intValue = (int)LootContainerSize.Small;
        SerializedProperty entries = settings.FindProperty("initialItems");
        entries.arraySize = 2;
        SetSeed(entries.GetArrayElementAtIndex(0), ItemType.Weapon, 1001, 1);
        SetSeed(entries.GetArrayElementAtIndex(1), ItemType.Ammo, 2001, 90);
        settings.ApplyModifiedProperties();
        // 샘플 CSV의 ID를 사용한다. 다른 데이터를 쓰는 경우 상자의 Initial Items에서 바꾼다.
        EditorSceneManager.MarkSceneDirty(chest.scene);
        Selection.activeGameObject = chest;
    }

    private static void SetSeed(SerializedProperty seed, ItemType type, int id, int count)
    {
        seed.FindPropertyRelative("itemType").enumValueIndex = (int)type;
        seed.FindPropertyRelative("itemId").intValue = id;
        seed.FindPropertyRelative("count").intValue = count;
    }
}
