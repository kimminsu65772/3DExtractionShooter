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

    [MenuItem("Tools/ExtractionShooter/Characters/Create USEC Enemy")] // [USEC 리뷰] 플레이 중 실행되는 코드가 아니라 USEC 자산을 만드는 Editor 메뉴다.
    public static void BuildUsec() // [USEC 리뷰] 기존 일반 적 자산을 보존하면서 선택적 기능을 USEC 복사본에만 붙인다.
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료한다."); // [USEC 리뷰] 런타임 오브젝트와 편집 자산을 섞어 수정하지 못하게 한다.
        const string prefabPath = Folder + "/Enemy_USEC.prefab"; // [USEC 리뷰] 게임에서 사용할 새 USEC 프리팹의 저장 위치다.
        const string controllerPath = Folder + "/Enemy_USEC.controller"; // [USEC 리뷰] 회피 상태를 추가할 독립 Animator Controller 저장 위치다.
        const string overridePath = Folder + "/Enemy_USEC.overrideController"; // [USEC 리뷰] 기존 오리 클립 교체를 유지할 Override Controller 저장 위치다.
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabPath) != null // [USEC 리뷰] 이미 조정한 USEC 프리팹이 있는지 확인한다.
            || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(controllerPath) != null // [USEC 리뷰] USEC Animator가 있으면 재생성으로 덮어쓰지 않는다.
            || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(overridePath) != null) // [USEC 리뷰] 클립 교체 자산 역시 덮어쓰기 보호 대상이다.
            throw new InvalidOperationException("기존 USEC 파일을 덮어쓰지 않는다."); // [USEC 리뷰] 하나라도 존재하면 새 자산을 만들기 전에 중단한다.

        // 일반 적의 기존 조준·발사·이동 연결을 보존하고 USEC 전용 회피 상태만 추가한다.
        var source = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Characters/DuckEnemyCopy/DuckEnemy.overrideController"); // [USEC 리뷰] 이미 검증한 일반 적의 이동·조준·발사 클립 구성을 읽는다.
        var roll = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/DuckPlayerCopy/Clips/Roll.anim"); // [USEC 리뷰] 같은 오리 골격에서 사용하는 기존 구르기 클립을 재사용한다.
        if (source == null || roll == null) throw new InvalidOperationException("기존 적 Controller 또는 회피 클립이 없다."); // [USEC 리뷰] 필수 원본이 없으면 불완전한 자산 생성을 시작하지 않는다.
        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source.runtimeAnimatorController), controllerPath)) // [USEC 리뷰] 일반 적의 공유 Controller를 수정하지 않고 별도 파일로 복사한다.
            throw new InvalidOperationException("USEC Animator 복사에 실패했다."); // [USEC 리뷰] 복사에 실패했으면 원본을 대신 수정하지 않고 중단한다.
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath); // [USEC 리뷰] 이후 추가하는 상태·전이는 이 USEC 복사본에만 들어간다.
        controller.AddParameter("IsDodging", AnimatorControllerParameterType.Bool); // [USEC 리뷰] 회피 시작·종료 전이를 제어할 bool을 추가한다.
        controller.AddParameter("DodgeSpeed", AnimatorControllerParameterType.Float); // [USEC 리뷰] 회피 지속시간에 클립 재생 길이를 맞출 배율을 추가한다.
        controller.parameters = controller.parameters.Select(p => // [USEC 리뷰] 파라미터 배열 복사본의 기본값을 고친 뒤 Controller에 다시 대입한다.
        {
            if (p.name == "DodgeSpeed") p.defaultFloat = 1f; // [USEC 리뷰] 실행 전 재생 속도가 0으로 정지하지 않도록 기본 배율을 1로 설정한다.
            return p; // [USEC 리뷰] 변경 대상이 아닌 기존 파라미터도 그대로 결과 배열에 남긴다.
        }).ToArray(); // [USEC 리뷰] 변환 결과를 배열로 만들어 Controller에 반영한다.
        var machine = controller.layers[0].stateMachine; // [USEC 리뷰] 상체만이 아니라 전신을 제어하는 Base Layer를 편집한다.
        var dodge = machine.AddState("Dodge"); // [USEC 리뷰] USEC 전용 전신 회피 애니메이션 상태를 추가한다.
        dodge.motion = roll; // [USEC 리뷰] 추가한 상태에서 기존 Roll 클립을 재생한다.
        dodge.speedParameterActive = true; // [USEC 리뷰] 고정 재생 속도 대신 파라미터로 속도를 조절하게 한다.
        dodge.speedParameter = "DodgeSpeed"; // [USEC 리뷰] EnemyAnimationController가 전달하는 재생 배율을 이 상태에서 사용한다.
        var enter = machine.AddAnyStateTransition(dodge); // [USEC 리뷰] 이동·대기 중 어디에서든 실제 회피가 시작되면 구르기로 전환할 수 있게 한다.
        enter.hasExitTime = false; // [USEC 리뷰] 이전 동작 클립의 종료를 기다리지 않고 회피 조건에 반응한다.
        enter.duration = 0.04f; // [USEC 리뷰] 회피 자세로 들어가는 짧은 블렌드 시간을 설정한다.
        enter.canTransitionToSelf = false; // [USEC 리뷰] IsDodging이 계속 true여도 매 프레임 구르기 처음으로 돌아가지 않는다.
        enter.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "IsDodging"); // [USEC 리뷰] 실제 회피 중일 때만 이 전이를 허용한다.
        enter.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "IsDead"); // [USEC 리뷰] 사망 상태에서는 회피 연출로 넘어가지 못하게 한다.
        var exit = dodge.AddTransition(machine.defaultState); // [USEC 리뷰] 회피가 끝나면 복사한 기본 이동 상태인 Locomotion으로 돌아가게 한다.
        exit.hasExitTime = false; // [USEC 리뷰] 실제 이동 종료 이후 남은 클립 때문에 회피 자세가 고정되지 않게 한다.
        exit.duration = 0.08f; // [USEC 리뷰] 구르기에서 일반 자세로 돌아오는 블렌드 시간을 설정한다.
        exit.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "IsDodging"); // [USEC 리뷰] 실행기가 회피를 종료하면 기본 이동 상태로 복귀한다.
        var overrides = new AnimatorOverrideController(controller); // [USEC 리뷰] 복사한 상태 구조 위에 기존 오리용 클립 교체 정보를 입힌다.
        var clips = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>(); // [USEC 리뷰] 원래 클립과 교체 클립의 대응을 받을 편집 시 전용 목록이다.
        source.GetOverrides(clips); // [USEC 리뷰] 기존 적에서 사용하던 Idle·조준·발사 등의 교체 대응을 읽는다.
        overrides.ApplyOverrides(clips); // [USEC 리뷰] USEC도 같은 검증된 기존 클립을 사용하게 한다.
        AssetDatabase.CreateAsset(overrides, overridePath); // [USEC 리뷰] 만들어진 교체 구성을 별도 자산으로 저장한다.
        EditorUtility.SetDirty(controller); // [USEC 리뷰] 추가한 파라미터·상태·전이를 이후 SaveAssets의 저장 대상으로 표시한다.

        BuildOne(Folder + "/Enemy_Default.prefab", "Duck_Usec", "Enemy_USEC"); // [USEC 리뷰] 기존 AI·무기·피격·드롭 연결은 복사하고 외형만 USEC으로 교체한다.
        var root = PrefabUtility.LoadPrefabContents(prefabPath); // [USEC 리뷰] 씬 인스턴스가 아닌 방금 만든 프리팹 내용물을 편집한다.
        try // [USEC 리뷰] 중간에 실패하더라도 finally에서 프리팹 편집용 내용을 해제한다.
        {
            root.AddComponent<StaminaComponent>(); // [USEC 리뷰] USEC 회피가 실제 비용을 소비할 수 있도록 자원 담당자를 붙인다.
            root.AddComponent<EnemyDodgeController>(); // [USEC 리뷰] 회피의 이동·무적·시작·종료를 실행하는 기능을 USEC에 붙인다.
            root.AddComponent<EnemyCombatMovement>(); // [USEC 리뷰] 이동 사격 허용 지점이다. Enemy_Default에는 이 컴포넌트를 추가하지 않는다.
            root.AddComponent<EnemyHitDodge>(); // [USEC 리뷰] 피격 시 실행기를 호출할 자동 반응 정책도 USEC에만 붙인다.
            var stats = new SerializedObject(root.GetComponent<CharacterStats>()); // [USEC 리뷰] private 직렬화 필드의 편집 값을 Unity 편집기 API로 다룬다.
            var entries = stats.FindProperty("baseStats"); // [USEC 리뷰] 프리팹이 갖는 기본 스탯 목록을 찾는다.
            for (int i = 0; i < entries.arraySize; i++) // [USEC 리뷰] 저장된 스탯 항목 중 최대 체력 항목을 찾는다.
            {
                var entry = entries.GetArrayElementAtIndex(i); // [USEC 리뷰] 현재 스탯 구조체의 직렬화된 필드를 읽는다.
                if (entry.FindPropertyRelative("statType").enumValueIndex == (int)StatType.MaxHealth) // [USEC 리뷰] 배열 위치를 가정하지 않고 항목 종류로 최대 체력을 구분한다.
                    entry.FindPropertyRelative("baseValue").floatValue = 60f; // [USEC 리뷰] 원본 USEC 참고값을 새 프리팹의 기본 체력에 반영한다.
            }
            stats.ApplyModifiedPropertiesWithoutUndo(); // [USEC 리뷰] 편집한 스탯 값을 프리팹 편집 객체에 적용한다.
            var perception = new SerializedObject(root.GetComponent<EnemyPerception>()); // [USEC 리뷰] USEC 복사본의 개인 감지 설정만 조정한다.
            perception.FindProperty("confirmedDistance").floatValue = 16f; // [USEC 리뷰] 직접 발견 거리의 초기값을 16m로 설정한다.
            perception.FindProperty("suspiciousDistance").floatValue = 20f; // [USEC 리뷰] 프로젝트의 먼 거리 의심 구간은 20m로 설정한다.
            perception.ApplyModifiedPropertiesWithoutUndo(); // [USEC 리뷰] 변경한 감지 설정을 편집 객체에 적용한다.
            var animation = new SerializedObject(root.GetComponent<EnemyAnimationController>()); // [USEC 리뷰] 런타임 Awake에서 사용할 애니메이션 자산 연결도 바꾼다.
            animation.FindProperty("animationSet").objectReferenceValue = overrides; // [USEC 리뷰] 실행 시 공용 Controller로 돌아가지 않도록 USEC 전용 자산을 연결한다.
            animation.ApplyModifiedPropertiesWithoutUndo(); // [USEC 리뷰] 컴포넌트의 직렬화 참조 변경을 적용한다.
            root.GetComponentInChildren<Animator>(true).runtimeAnimatorController = overrides; // [USEC 리뷰] 모델 Animator 자체도 같은 USEC Controller를 사용하게 한다.
            // 외부 씬 참조와 장비 추첨은 생성자가 연결한다. 테스트 장비는 기존 AK·표준탄이다.
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath); // [USEC 리뷰] 완성한 기능 구성과 설정을 USEC 프리팹에 저장한다.
        }
        finally { PrefabUtility.UnloadPrefabContents(root); } // [USEC 리뷰] 편집용 프리팹 내용물을 성공·실패와 무관하게 해제한다.
        AssetDatabase.SaveAssets(); // [USEC 리뷰] 별도 생성한 Animator 관련 자산의 수정도 디스크에 저장한다.
        Debug.Log("USEC 전용 프리팹과 Animator를 생성했다: " + prefabPath); // [USEC 리뷰] 생성한 결과 경로를 Editor Console에 남긴다.
    }

    private static void BuildOne(string sourcePath, string candidate, string name)
    {
        var root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            root.name = name;
            ReplaceVisual(root, "Assets/Characters/DuckCandidates/Prefabs/" + candidate + ".prefab");
            // 일반 적에는 회피 능력을 붙이지 않는다. 기존 FSM과 장비·드롭 기능을 유지한다.
            if (name == "Enemy_Default" && root.GetComponent<EnemyDodgeController>() != null)
                UnityEngine.Object.DestroyImmediate(root.GetComponent<EnemyDodgeController>());
            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab", out success);
            if (!success) throw new InvalidOperationException("게임용 프리팹 저장에 실패했다: " + name);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 단독 적과 분대 구성원 모두 기존 게임 연결을 유지한 채 조립된 외형만 교체한다.
    public static void ReplaceVisual(GameObject root, string visualPrefabPath, bool useOriginalRig = false)
    {
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
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(visualPrefabPath);
            if (source == null) throw new InvalidOperationException("외형 후보가 없다: " + visualPrefabPath);
            var model = UnityEngine.Object.Instantiate(source, oldModel.transform.parent, false);
            model.name = oldModel.name;
            model.transform.localPosition = oldModel.transform.localPosition;
            model.transform.localRotation = oldModel.transform.localRotation;
            model.transform.localScale = oldModel.transform.localScale;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
            var animator = model.GetComponentInChildren<Animator>(true);
            if (!useOriginalRig)
            {
                animator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;
                animator.avatar = oldAnimator.avatar;
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // 외형 후보에는 없는 장착점과 피격 판정만 기존 게임용 모델에서 복제한다.
            foreach (var t in oldModel.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "WeaponMount" || t.GetComponent<HitBox>() != null))
            {
                var parent = FindMatching(t.parent, oldModel.transform, model.transform, useOriginalRig);
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
                var target = FindMatching(script.transform, oldModel.transform, model.transform, useOriginalRig);
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
                    var replacement = FindMatching(oldTransform, oldModel.transform, model.transform, useOriginalRig);
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
    }

    private static Transform FindMatching(Transform old, Transform oldRoot, Transform newRoot, bool useOriginalRig = false)
    {
        if (old == oldRoot) return newRoot;
        var exact = newRoot.Find(AnimationUtility.CalculateTransformPath(old, oldRoot));
        if (exact != null || !useOriginalRig) return exact;
        // 꼬마덕의 전용 골격에는 공통 오리와 이름이 다른 대응 부위만 명시적으로 연결한다.
        if (old.GetComponent<Animator>() != null)
            return newRoot.GetComponentInChildren<Animator>(true).transform;
        string name = old.name == "Spine.004" ? "Spine.003" : old.name == "Wings.R" ? "Hand.R" : old.name;
        var matches = newRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}
