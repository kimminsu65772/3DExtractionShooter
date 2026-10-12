using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 외형 후보와 기존 게임 기능을 조합하고 원본을 보존한 독립 프리팹으로 저장한다.
public static class DuckGameplayPrefabBuilder
{
    public const string Folder = "Assets/Characters/GameCharacters";

    [MenuItem("Tools/ExtractionShooter/Characters/Create Default Enemy and Yellow Player")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료한다.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Enemy_Default.prefab") != null
            || AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Player_YellowDuck.prefab") != null)
            throw new InvalidOperationException("기존 게임용 프리팹을 덮어쓰지 않는다.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Characters", "GameCharacters");
        BuildOne("Assets/Characters/DuckEnemyCopy/DuckEnemyRoot.prefab", "Duck_Default", "Enemy_Default");
        BuildOne("Assets/Characters/DuckPlayerCopy/DuckPlayerRoot.prefab", "Duck_YellowDuck", "Player_YellowDuck");
        AssetDatabase.SaveAssets();
        Debug.Log("일반 적과 노란 오리 플레이어 게임용 프리팹을 생성했다: " + Folder);
    }

    private static void BuildOne(string sourcePath, string candidate, string name)
    {
        var root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            root.name = name;
            var oldAnimator = root.GetComponentsInChildren<Animator>(true).Single();
            // Animator는 외형 루트의 자식에 있으므로 포장 루트까지 함께 교체한다.
            var visualRoot = root.transform.Find("CharacterVisual");
            if (visualRoot == null) throw new InvalidOperationException("CharacterVisual이 없다.");
            var oldModelTransform = oldAnimator.transform;
            while (oldModelTransform.parent != visualRoot)
            {
                oldModelTransform = oldModelTransform.parent;
                if (oldModelTransform == null) throw new InvalidOperationException("Animator가 외형 계층 밖에 있다.");
            }
            var oldModel = oldModelTransform.gameObject;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/DuckCandidates/Prefabs/" + candidate + ".prefab");
            if (source == null) throw new InvalidOperationException("외형 후보가 없다: " + candidate);
            var model = UnityEngine.Object.Instantiate(source, oldModel.transform.parent, false);
            model.name = oldModel.name;
            model.transform.localPosition = oldModel.transform.localPosition;
            model.transform.localRotation = oldModel.transform.localRotation;
            model.transform.localScale = oldModel.transform.localScale;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
            var animator = model.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;
            animator.avatar = oldAnimator.avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // 외형 후보에는 없는 장착점과 피격 판정만 기존 게임용 모델에서 복제한다.
            foreach (var t in oldModel.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "WeaponMount" || t.GetComponent<HitBox>() != null))
            {
                var parent = FindMatching(t.parent, oldModel.transform, model.transform);
                if (parent == null) throw new InvalidOperationException("게임용 부착 위치가 없다: " + t.name);
                if (FindMatching(t, oldModel.transform, model.transform) != null)
                    throw new InvalidOperationException("게임용 부착 위치가 중복된다: " + t.name);
                var extra = UnityEngine.Object.Instantiate(t.gameObject, parent, false);
                extra.name = t.name;
                extra.transform.localPosition = t.localPosition;
                extra.transform.localRotation = t.localRotation;
                extra.transform.localScale = t.localScale;
            }
            // 사격 프레임 릴레이처럼 Animator와 같은 오브젝트에 있던 게임용 연결을 보존한다.
            foreach (var script in oldModel.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script == null) throw new InvalidOperationException("기존 모델에 누락된 스크립트가 있다.");
                var target = FindMatching(script.transform, oldModel.transform, model.transform);
                if (target == null) throw new InvalidOperationException("게임용 스크립트 부착 위치가 없다: " + script.name);
                var copy = target.GetComponent(script.GetType()) ?? target.gameObject.AddComponent(script.GetType());
                EditorUtility.CopySerialized(script, copy);
            }
            // 모델을 지우기 전에 게임 기능의 참조를 새 모델의 동일 상대 경로로 옮긴다.
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) throw new InvalidOperationException("누락된 게임 스크립트가 있다.");
                if (component.transform.IsChildOf(oldModel.transform)) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    // 외형마다 부품 이름과 개수가 달라지는 표시 목록은 아래에서 새로 구성한다.
                    if (component is VisibilityTarget && property.propertyPath.StartsWith("controlledRenderers")) continue;
                    var value = property.objectReferenceValue;
                    var oldComponent = value as Component;
                    var oldObject = value as GameObject;
                    var oldTransform = oldComponent != null ? oldComponent.transform : oldObject != null ? oldObject.transform : null;
                    if (oldTransform == null || !oldTransform.IsChildOf(oldModel.transform)) continue;
                    var replacement = FindMatching(oldTransform, oldModel.transform, model.transform);
                    if (replacement == null) throw new InvalidOperationException("모델 참조를 옮길 수 없다: " + property.propertyPath);
                    UnityEngine.Object mapped = oldComponent != null ? replacement.GetComponent(oldComponent.GetType()) : replacement.gameObject;
                    if (mapped == null) throw new InvalidOperationException("새 모델의 컴포넌트가 없다: " + property.propertyPath);
                    property.objectReferenceValue = mapped;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            // 표시 제어 목록은 이전 외형의 부품 개수에 의존하지 않고 새 Renderer 전체로 갱신한다.
            var visibility = root.GetComponent<VisibilityTarget>();
            if (visibility != null)
            {
                var data = new SerializedObject(visibility);
                var list = data.FindProperty("controlledRenderers");
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                list.arraySize = renderers.Length;
                for (int i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            UnityEngine.Object.DestroyImmediate(oldModel);
            // 일반 적에는 회피 능력을 붙이지 않는다. 기존 FSM과 장비·드롭 기능을 유지한다.
            if (name == "Enemy_Default" && root.GetComponent<EnemyDodgeController>() != null)
                UnityEngine.Object.DestroyImmediate(root.GetComponent<EnemyDodgeController>());
            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab", out success);
            if (!success) throw new InvalidOperationException("게임용 프리팹 저장에 실패했다: " + name);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Transform FindMatching(Transform old, Transform oldRoot, Transform newRoot)
    {
        if (old == oldRoot) return newRoot;
        return newRoot.Find(AnimationUtility.CalculateTransformPath(old, oldRoot));
    }
}
