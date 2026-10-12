using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 기존 적의 행동과 데이터 연결은 보존하고 복사본의 모델 참조만 바꾼다.
public static class DuckEnemyCopyBuilder
{
    public const string Folder = "Assets/Characters/DuckEnemyCopy";
    public const string ScenePath = Folder + "/DuckEnemyIntegration.unity";
    private const string PlayerFolder = "Assets/Characters/DuckPlayerCopy";

    public static void Build()
    {
        if (Application.isPlaying || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("플레이를 중지한다. 기존 적 복사본은 덮어쓰지 않는다.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Characters", "DuckEnemyCopy");
        var source = SceneManager.GetSceneByPath(PlayerFolder + "/DuckPlayerIntegration.unity");
        if (!source.isLoaded) source = EditorSceneManager.OpenScene(PlayerFolder + "/DuckPlayerIntegration.unity", OpenSceneMode.Additive);
        // 원본의 저장 경로와 미저장 변경을 건드리지 않는 별도 씬을 만든다.
        if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new InvalidOperationException("적 시험 씬 복사에 실패했다.");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var enemy = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyRangedBrain>(true)).Single().gameObject;
        if (PrefabUtility.IsPartOfPrefabInstance(enemy)) PrefabUtility.UnpackPrefabInstance(enemy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        enemy.name = "DuckEnemyRoot";
        var sourcePlayer = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFolder + "/DuckPlayerRoot.prefab");
        var visual = UnityEngine.Object.Instantiate(sourcePlayer.transform.Find("CharacterVisual").gameObject);
        visual.name = "CharacterVisual";
        SceneManager.MoveGameObjectToScene(visual, scene);
        visual.transform.SetParent(enemy.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        var animator = visual.GetComponentInChildren<Animator>(true);
        var mount = visual.GetComponentsInChildren<Transform>(true).Single(t => t.name == "WeaponMount");
        // 히트박스만 복제하며 플레이어의 행동 담당자는 적에게 가져오지 않는다.
        var hitBoxes = visual.GetComponentsInChildren<HitBox>(true);
        foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = enemy.layer;
        var provider = new SerializedObject(enemy.GetComponent<HitPartProvider>());
        var boxes = provider.FindProperty("hitBoxes"); boxes.arraySize = hitBoxes.Length;
        for (int i = 0; i < hitBoxes.Length; i++) boxes.GetArrayElementAtIndex(i).objectReferenceValue = hitBoxes[i];
        provider.ApplyModifiedPropertiesWithoutUndo();
        Set(enemy.GetComponent<EnemyWeaponLoadout>(), "weaponMount", mount);
        var visibility = enemy.GetComponent<VisibilityTarget>();
        Set(visibility, "visibilityPoint", hitBoxes.Single(b => b.HitPart == HitPart.Body).transform);
        var visibilityData = new SerializedObject(visibility);
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        var list = visibilityData.FindProperty("controlledRenderers"); list.arraySize = renderers.Length;
        for (int i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        visibilityData.ApplyModifiedPropertiesWithoutUndo();
        // 시야 방향은 애니메이션 본이 아니라 적 루트의 전방을 계속 기준으로 삼는다.
        foreach (string oldName in new[] { "Head", "Body", "Weapon Mount" })
        {
            var old = enemy.transform.Find(oldName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        var animationSet = BuildOverride();
        animator.runtimeAnimatorController = animationSet;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var animation = enemy.GetComponent<EnemyAnimationController>();
        if (animation == null) animation = enemy.AddComponent<EnemyAnimationController>();
        Set(animation, "brain", enemy.GetComponent<EnemyRangedBrain>());
        Set(animation, "motor", enemy.GetComponent<EnemyMotor>());
        Set(animation, "attackController", enemy.GetComponent<EnemyAttackController>());
        Set(animation, "animator", animator);
        Set(animation, "animationSet", animationSet);
        // 피격과 전리품 수집이 같은 보호 장비 객체를 사용하도록 담당자를 연결한다.
        var protection = enemy.GetComponent<EnemyProtectionLoadout>();
        if (protection == null) protection = enemy.AddComponent<EnemyProtectionLoadout>();
        var equipment = enemy.GetComponent<EnemyEquipmentLoadout>();
        if (equipment == null) equipment = enemy.AddComponent<EnemyEquipmentLoadout>();
        Set(equipment, "weaponLoadout", enemy.GetComponent<EnemyWeaponLoadout>());
        Set(equipment, "protectionLoadout", protection);
        Set(enemy.GetComponent<EnemyLootDropper>(), "equipmentLoadout", equipment);

        // 씬 외부의 플레이어와 데이터 참조는 프리팹 저장 후 인스턴스 오버라이드로 보존한다.
        PrefabUtility.SaveAsPrefabAssetAndConnect(enemy, Folder + "/DuckEnemyRoot.prefab", InteractionMode.AutomatedAction);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = enemy;
    }

    private static AnimatorOverrideController BuildOverride()
    {
        var source = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyAnimatorAssetBuilder.ControllerPath);
        if (source == null) throw new InvalidOperationException("공통 적 Animator가 필요하다.");
        // 기존 공통 자산에는 이동 트리 참조가 없을 수 있다. 전환 틀을 복제하고 이동 트리를 명시적으로 저장한다.
        string controllerPath = Folder + "/DuckEnemy.controller";
        if (!AssetDatabase.CopyAsset(EnemyAnimatorAssetBuilder.ControllerPath, controllerPath)) throw new InvalidOperationException("적 Animator 복사에 실패했다.");
        source = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var movement = new BlendTree { name = "DuckEnemyMovement", blendType = BlendTreeType.Simple1D, blendParameter = "MoveSpeed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(movement, source);
        foreach (var entry in new[] { new { name = "Idle", threshold = 0f }, new { name = "Walk", threshold = .5f }, new { name = "Run", threshold = 1f } })
            movement.AddChild(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Enemy/Slots/Enemy_" + entry.name + ".anim"), entry.threshold);
        // 원본처럼 같은 이동 클립을 느린 걸음과 달리기의 서로 다른 재생 속도로 사용한다.
        var children = movement.children;
        children[1].timeScale = .8f;
        children[2].timeScale = 1.4f;
        movement.children = children;
        source.layers[0].stateMachine.states.Single(s => s.state.name == "Locomotion").state.motion = movement;
        EditorUtility.SetDirty(source);
        var result = new AnimatorOverrideController(source) { name = "DuckEnemyAnimationSet" };
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(); result.GetOverrides(overrides);
        var mapping = new Dictionary<string, string> { { "Enemy_Idle", "Idle" }, { "Enemy_Walk", "WalkForward" }, { "Enemy_Run", "WalkForward" }, { "Enemy_Aim", "Aim" }, { "Enemy_Fire", "Aim" } };
        for (int i = 0; i < overrides.Count; i++)
        {
            if (!mapping.TryGetValue(overrides[i].Key.name, out var name)) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerFolder + "/Clips/" + name + ".anim");
            if (clip == null) throw new InvalidOperationException("오리 애니메이션이 없다: " + name);
            // 적은 발사 성공 뒤 자세만 재생한다. 플레이어의 OnFireFrame 이벤트 클립을 쓰지 않는다.
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
        }
        result.ApplyOverrides(overrides);
        AssetDatabase.CreateAsset(result, Folder + "/DuckEnemy.overrideController");
        return result;
    }

    private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(property).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
