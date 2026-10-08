using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// UI와 모델을 변경하지 않고 파밍 시스템의 필수 컴포넌트만 연결한다. 씬 저장은 직접 결정한다.
public static class FarmingSystemSetup
{
    [MenuItem("Tools/Inventory/현재 씬 파밍 시스템 연결")]
    public static void PrepareScene()
    {
        if (Application.isPlaying) return;
        PlayerInventory player = Object.FindFirstObjectByType<PlayerInventory>();
        if (player == null)
        {
            Debug.LogWarning("PlayerInventory가 있는 씬을 연다.");
            return;
        }
        PlayerWorldItemDropper dropper = player.GetComponent<PlayerWorldItemDropper>();
        if (dropper == null) dropper = Undo.AddComponent<PlayerWorldItemDropper>(player.gameObject);
        if (player.GetComponent<PlayerCarryWeight>() == null) Undo.AddComponent<PlayerCarryWeight>(player.gameObject);
        SerializedObject inventorySettings = new SerializedObject(player);
        inventorySettings.FindProperty("worldItemDropper").objectReferenceValue = dropper;
        inventorySettings.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Selection.activeGameObject = player.gameObject;
    }
}
