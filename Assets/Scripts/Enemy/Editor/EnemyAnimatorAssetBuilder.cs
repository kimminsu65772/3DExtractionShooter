using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 모델과 무관한 상태 전환 틀을 만들고 실제 자세는 Override의 교체 클립에 맡긴다.
public static class EnemyAnimatorAssetBuilder
{
    public const string ControllerPath = "Assets/Animations/Enemy/EnemyAnimator.controller";
    private const string SlotFolder = "Assets/Animations/Enemy/Slots";

    [MenuItem("Tools/ExtractionShooter/Enemy/Create Common Animator")]
    public static void CreateCommonAnimator()
    {
        // 사용자가 수정한 상태와 전환을 다시 생성하여 덮어쓰지 않는다.
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
        {
            Debug.Log("기존 EnemyAnimator를 유지한다.");
            return;
        }

        EnsureFolder("Assets/Animations");
        EnsureFolder("Assets/Animations/Enemy");
        EnsureFolder(SlotFolder);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsReloading", AnimatorControllerParameterType.Bool);
        controller.AddParameter("ReloadSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);
        AnimatorControllerParameter[] parameters = controller.parameters;
        foreach (AnimatorControllerParameter parameter in parameters)
            if (parameter.name == "ReloadSpeed") parameter.defaultFloat = 1f;
        controller.parameters = parameters;

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        machine.anyStatePosition = new Vector3(40f, 40f);
        machine.entryPosition = new Vector3(40f, 180f);
        machine.exitPosition = new Vector3(820f, 300f);
        AnimatorState locomotion = machine.AddState("Locomotion", new Vector3(260f, 180f));
        AnimatorState aim = machine.AddState("Aim", new Vector3(520f, 180f));
        AnimatorState fire = machine.AddState("Fire", new Vector3(520f, 40f));
        AnimatorState reload = machine.AddState("Reload", new Vector3(260f, 340f));
        AnimatorState dead = machine.AddState("Dead", new Vector3(780f, 40f));
        machine.defaultState = locomotion;

        // 실제 속도에 따라 대기·걷기·달리기를 섞어 FSM의 이동 이름과 자세를 분리한다.
        BlendTree movement = new BlendTree { name = "Enemy Movement", blendType = BlendTreeType.Simple1D, blendParameter = "MoveSpeed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(movement, controller);
        movement.AddChild(CreateSlot("Idle", true), 0f);
        movement.AddChild(CreateSlot("Walk", true), 0.5f);
        movement.AddChild(CreateSlot("Run", true), 1f);
        locomotion.motion = movement;
        aim.motion = CreateSlot("Aim", true);
        fire.motion = CreateSlot("Fire", false);
        reload.motion = CreateSlot("Reload", false);
        dead.motion = CreateSlot("Dead", false);
        reload.speedParameter = "ReloadSpeed";
        reload.speedParameterActive = true;
        dead.AddStateMachineBehaviour<EnemyDeathAnimationBehaviour>();

        // 사망이 가장 먼저 판단되도록 등록하고 같은 사망 상태로 재진입하지 않는다.
        AnimatorStateTransition deathTransition = Configure(machine.AddAnyStateTransition(dead), 0f);
        deathTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
        AnimatorStateTransition reloadTransition = Configure(machine.AddAnyStateTransition(reload), 0.05f);
        reloadTransition.canTransitionToSelf = true;
        reloadTransition.AddCondition(AnimatorConditionMode.If, 0f, "Reload");
        reloadTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsReloading");
        reloadTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        AnimatorStateTransition fireTransition = Configure(machine.AddAnyStateTransition(fire), 0.03f);
        fireTransition.canTransitionToSelf = true;
        fireTransition.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
        fireTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming");
        fireTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsReloading");
        fireTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");

        AddTransition(locomotion, aim, "IsAiming", true);
        AddTransition(aim, locomotion, "IsAiming", false);
        AnimatorStateTransition fireToAim = AddTransition(fire, aim, "IsAiming", true);
        fireToAim.hasExitTime = true;
        fireToAim.exitTime = 1f;
        AddTransition(fire, locomotion, "IsAiming", false);
        AnimatorStateTransition reloadToAim = AddTransition(reload, aim, "IsReloading", false);
        reloadToAim.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming");
        AnimatorStateTransition reloadToMove = AddTransition(reload, locomotion, "IsReloading", false);
        reloadToMove.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("공통 적 Animator와 7개 클립 교체 자리를 생성했다.");
    }

    [MenuItem("Tools/ExtractionShooter/Enemy/Create Animation Override")]
    public static void CreateAnimationOverride()
    {
        CreateCommonAnimator();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var overrides = new AnimatorOverrideController(controller) { name = "EnemyAnimationSet" };
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Animations/Enemy/EnemyAnimationSet.overrideController");
        AssetDatabase.CreateAsset(overrides, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = overrides;
        Debug.Log($"모델별 클립을 연결할 Override를 생성했다: {path}");
    }

    // 별도의 검증용 Unity 프로젝트에서도 같은 자산 생성 코드를 실행한다.
    public static void RunBatch()
    {
        try
        {
            CreateCommonAnimator();
            CreateAnimationOverride();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static AnimationClip CreateSlot(string slotName, bool loop)
    {
        string path = $"{SlotFolder}/Enemy_{slotName}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null) return clip;
        clip = new AnimationClip { name = $"Enemy_{slotName}", frameRate = 30f };
        // 존재하지 않는 전용 경로에 길이만 만든다. 실제 모델의 위치나 본은 움직이지 않는다.
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("__EnemyAnimationPlaceholder", typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Constant(0f, 1f, 0f));
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimatorStateTransition AddTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
    {
        AnimatorStateTransition transition = Configure(from.AddTransition(to), 0.05f);
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        return transition;
    }

    private static AnimatorStateTransition Configure(AnimatorStateTransition transition, float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        // 새 사망·장전 요청이 짧은 전환 중에도 기존 모션에 막히지 않게 한다.
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
        // 같은 조건의 전환이 매 프레임 자신을 다시 시작하지 않도록 현재 전환에서 탐색을 끝낸다.
        transition.orderedInterruption = true;
        return transition;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
}
