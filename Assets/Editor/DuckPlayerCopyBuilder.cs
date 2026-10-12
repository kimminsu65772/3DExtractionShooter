using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 원본 씬을 복사한 뒤 최신 플레이어의 게임 기능을 보존하며 외형 참조만 교체한다.
public static class DuckPlayerCopyBuilder
{
    private const string Folder = "Assets/Characters/DuckPlayerCopy";
    private const string TestFolder = "Assets/Test/CommonCharacter";
    private const string ScenePath = Folder + "/DuckPlayerIntegration.unity";
    private static readonly Dictionary<UnityEngine.Object, UnityEngine.Object> copies = new();

    [MenuItem("Tools/ExtractionShooter/Player/Create Duck Player Copy")]
    public static void Build()
    {
        if (Application.isPlaying || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("플레이를 중지한다. 기존 복사본은 덮어쓰지 않는다.");
        EnsureFolder(Folder);
        EnsureFolder(Folder + "/VisualAssets");
        EnsureFolder(Folder + "/Clips");
        var source = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        if (!source.isLoaded) source = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        // saveAsCopy는 원본의 저장 위치와 미저장 변경을 보존한다.
        EditorSceneManager.SaveScene(source, ScenePath, true);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerController>(true)).Single().gameObject;
        if (PrefabUtility.IsPartOfPrefabInstance(player))
            PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        player.name = "DuckPlayerRoot";
        var visual = player.transform.Find("CharacterVisual");
        var oldModels = Enumerable.Range(0, visual.childCount).Select(i => visual.GetChild(i).gameObject).ToArray();
        var duckSource = AssetDatabase.LoadAssetAtPath<GameObject>(TestFolder + "/CompleteCommonDuck.prefab");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(duckSource, scene);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = "DuckVisual";
        model.transform.SetParent(visual, false);
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        var animator = model.GetComponentInChildren<Animator>(true);
        var preview = model.GetComponent<CommonCharacterAnimationPreview>();
        var testGun = preview != null ? preview.weapon : null;
        // 외형에는 시험 담당자와 원작의 실행 코드를 남기지 않는다.
        foreach (var c in model.GetComponentsInChildren<MonoBehaviour>(true)) if (c != null) UnityEngine.Object.DestroyImmediate(c);
        foreach (var t in model.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        if (testGun != null) UnityEngine.Object.DestroyImmediate(testGun.gameObject);
        // 보이는 손과 같은 부모에 장비의 기준점을 둔다.
        var hand = model.GetComponentsInChildren<Transform>(true).First(t => t.name == "Wings.R");
        var mount = new GameObject("WeaponMount").transform;
        mount.SetParent(hand, false);
        mount.localPosition = new Vector3(-.05f, .34f, -.09f);
        var aim = CopyClip("Armature_Aim", "Aim");
        AnimationMode.StartAnimationMode();
        try
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, aim, 0);
            AnimationMode.EndSampling();
            var referenceWeapon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapon/AK47.prefab");
            var referenceMuzzle = referenceWeapon.GetComponentInChildren<WeaponView>(true).MuzzleTransform;
            Vector3 barrelAxis = referenceWeapon.transform.InverseTransformDirection(referenceMuzzle.forward);
            // 기존 AK 외형의 자식 회전까지 고려해 대여 루트 기준 총구 전방을 맞춘다.
            mount.localRotation = Quaternion.Inverse(hand.rotation) * player.transform.rotation * Quaternion.FromToRotation(barrelAxis,Vector3.forward);
        }
        finally { AnimationMode.StopAnimationMode(); }
        var bodyBone = animator.transform.Find("Armature/Root/Pelvis/Spine.001/Spine.002");
        var headBone = model.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
        var body = MakeHitBox("DuckBodyHitBox", bodyBone, player.layer, HitPart.Body, new Vector3(.48f,.5f,.4f), Vector3.zero);
        var head = MakeHitBox("DuckHeadHitBox", headBone, player.layer, HitPart.Head, new Vector3(.75f,.65f,.65f), new Vector3(0,.2f,0));
        Set(player.GetComponent<PlayerAnimationController>(), "animator", animator);
        Set(player.GetComponent<PlayerAnimationController>(), "reloadClip", null);
        Set(player.GetComponent<PlayerWeaponHolder>(), "weaponMount", mount);
        var provider = new SerializedObject(player.GetComponent<HitPartProvider>());
        var boxes = provider.FindProperty("hitBoxes");
        boxes.arraySize = 2;
        boxes.GetArrayElementAtIndex(0).objectReferenceValue = head;
        boxes.GetArrayElementAtIndex(1).objectReferenceValue = body;
        provider.ApplyModifiedPropertiesWithoutUndo();
        Set(player.GetComponent<AimPointProvider>(), "headAimPoint", head.transform);
        Set(player.GetComponent<AimPointProvider>(), "bodyAimPoint", body.transform);
        foreach (var old in oldModels) UnityEngine.Object.DestroyImmediate(old);
        // 사용할 자산만 복제해 Test 폴더를 지워도 외형 참조가 끊어지지 않게 한다.
        copies.Clear();
        var assets = new List<string>();
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials) if (m != null) assets.Add(AssetDatabase.GetAssetPath(m));
        foreach (var f in model.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null) assets.Add(AssetDatabase.GetAssetPath(f.sharedMesh));
        foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.sharedMesh != null) assets.Add(AssetDatabase.GetAssetPath(r.sharedMesh));
        assets.Add(AssetDatabase.GetAssetPath(animator.avatar));
        foreach (var path in AssetDatabase.GetDependencies(assets.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToArray(), true).Where(p => p.StartsWith("Assets/Test/") && !p.EndsWith(".cs"))) CopyVisualAsset(path);
        foreach (var asset in copies.Values.Distinct().ToArray()) if (asset is Material) Remap(asset);
        foreach (var component in model.GetComponentsInChildren<Component>(true)) if (component != null) Remap(component);
        animator.runtimeAnimatorController = BuildController(animator, player.GetComponent<PlayerController>());
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var context = player.AddComponent<DuckPlayerAnimationContext>();
        Set(context, "animator", animator);
        Set(context, "weaponHolder", player.GetComponent<PlayerWeaponHolder>());
        PrefabUtility.SaveAsPrefabAssetAndConnect(player, Folder + "/DuckPlayerRoot.prefab", InteractionMode.AutomatedAction);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;
        Debug.Log("오리 플레이어 복사본과 독립 시험 씬을 생성했다. 원본 SampleScene과 PlayerRoot 프리팹은 변경하지 않았다.");
    }

    private static AnimatorController BuildController(Animator animator, PlayerController player)
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/DuckPlayer.controller");
        foreach (string p in new[] {"MoveX","MoveY","MoveAmount","ReloadSpeed"}) controller.AddParameter(p, AnimatorControllerParameterType.Float);
        foreach (string p in new[] {"IsSprinting","IsAiming","IsFiring","HasWeapon"}) controller.AddParameter(p, AnimatorControllerParameterType.Bool);
        controller.AddParameter("ActionState", AnimatorControllerParameterType.Int);
        controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
        var idle = CopyClip("Armature_Idle_1", "Idle");
        var forward = CopyClip("Armature_Walk_Forward_0", "WalkForward");
        var backward = CopyClip("Armature_Walk_Backward", "WalkBackward");
        var left = CopyClip("Armature_Walk_Left", "WalkLeft");
        var right = CopyClip("Armature_Walk_Right", "WalkRight");
        var aim = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Clips/Aim.anim");
        var roll = CopyClip("Armature_DodgeRoll_Forward", "Roll");
        var fire = UnityEngine.Object.Instantiate(aim);
        fire.name = "DuckFireEventPose";
        var settings = AnimationUtility.GetAnimationClipSettings(fire);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(fire, settings);
        AnimationUtility.SetAnimationEvents(fire, new[] { new AnimationEvent { time = .03f, functionName = "OnFireFrame" } });
        AssetDatabase.CreateAsset(fire, Folder + "/Clips/FireEventPose.anim");
        var baseMachine = controller.layers[0].stateMachine;
        var locomotion = baseMachine.AddState("Locomotion");
        baseMachine.defaultState = locomotion;
        var tree = new BlendTree { name = "DirectionMove", blendType = BlendTreeType.SimpleDirectional2D, blendParameter = "MoveX", blendParameterY = "MoveY", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(idle, Vector2.zero);
        tree.AddChild(forward, Vector2.up); tree.AddChild(backward, Vector2.down);
        tree.AddChild(left, Vector2.left); tree.AddChild(right, Vector2.right);
        locomotion.motion = tree;
        // 원본의 달리기 구간과 같은 배속으로 이동 클립을 재사용한다.
        var sprint = baseMachine.AddState("Sprint"); sprint.motion = forward; sprint.speed = 1.4f;
        var enter = locomotion.AddTransition(sprint); Setup(enter); enter.AddCondition(AnimatorConditionMode.If,0,"IsSprinting");
        var exit = sprint.AddTransition(locomotion); Setup(exit); exit.AddCondition(AnimatorConditionMode.IfNot,0,"IsSprinting");
        var mask = new AvatarMask();
        var bones = animator.GetComponentsInChildren<Transform>(true);
        mask.transformCount = bones.Length;
        for (int i=0;i<bones.Length;i++)
        {
            // 마스크도 클립처럼 Animator를 기준으로 한 상대 경로를 사용한다.
            string path = AnimationUtility.CalculateTransformPath(bones[i], animator.transform);
            mask.SetTransformPath(i,path);
            mask.SetTransformActive(i,path.Contains("/UpperArm.R") || path.Contains("/UpperArm.L"));
        }
        AssetDatabase.CreateAsset(mask, Folder + "/DuckArms.mask");
        controller.AddLayer("Arms");
        var layers = controller.layers; layers[1].defaultWeight=1; layers[1].avatarMask=mask; controller.layers=layers;
        var upper=controller.layers[1].stateMachine;
        var empty=upper.AddState("BasePose"); empty.writeDefaultValues=false; upper.defaultState=empty;
        var aiming=upper.AddState("Aim"); aiming.motion=aim;
        var firing=upper.AddState("Fire"); firing.motion=fire;
        foreach (string condition in new[] {"IsAiming","IsFiring"})
        {
            var t=empty.AddTransition(aiming); Setup(t); t.AddCondition(AnimatorConditionMode.If,0,condition); t.AddCondition(AnimatorConditionMode.If,0,"HasWeapon"); t.AddCondition(AnimatorConditionMode.Equals,0,"ActionState");
        }
        var back=aiming.AddTransition(empty); Setup(back); back.AddCondition(AnimatorConditionMode.IfNot,0,"IsAiming"); back.AddCondition(AnimatorConditionMode.IfNot,0,"IsFiring");
        foreach(var s in new[] {aiming,firing})
        {
            var t=s.AddTransition(empty); Setup(t); t.AddCondition(AnimatorConditionMode.NotEqual,0,"ActionState");
            var unarmed=s.AddTransition(empty); Setup(unarmed); unarmed.AddCondition(AnimatorConditionMode.IfNot,0,"HasWeapon");
        }
        var shot=upper.AddAnyStateTransition(firing); Setup(shot); shot.canTransitionToSelf=false;
        shot.AddCondition(AnimatorConditionMode.If,0,"Fire"); shot.AddCondition(AnimatorConditionMode.If,0,"HasWeapon"); shot.AddCondition(AnimatorConditionMode.Equals,0,"ActionState");
        var done=firing.AddTransition(empty); Setup(done); done.hasExitTime=true; done.exitTime=1;
        controller.AddLayer("FullBodyAction");
        layers=controller.layers; layers[2].defaultWeight=1; controller.layers=layers;
        var full=controller.layers[2].stateMachine;
        var free=full.AddState("Free"); free.writeDefaultValues=false; full.defaultState=free;
        var dodge=full.AddState("Dodge"); dodge.motion=roll;
        var duration=new SerializedObject(player).FindProperty("dodgeDuration").floatValue;
        dodge.speed=roll.length / Mathf.Max(duration,.01f);
        var dodgeIn=free.AddTransition(dodge); Setup(dodgeIn); dodgeIn.duration=0; dodgeIn.AddCondition(AnimatorConditionMode.Equals,(int)PlayerActionState.Dodge,"ActionState");
        var dodgeOut=dodge.AddTransition(free); Setup(dodgeOut); dodgeOut.duration=0; dodgeOut.AddCondition(AnimatorConditionMode.NotEqual,(int)PlayerActionState.Dodge,"ActionState");
        return controller;
    }

    private static void Setup(AnimatorStateTransition t) { t.hasExitTime=false; t.hasFixedDuration=true; t.duration=.05f; }
    private static AnimationClip CopyClip(string source, string name)
    {
        var path=Folder+"/Clips/"+name+".anim";
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path); if(existing!=null)return existing;
        AssetDatabase.CopyAsset(TestFolder+"/AnimationClip/"+source+".anim",path);
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }
    private static HitBox MakeHitBox(string name, Transform parent, int layer, HitPart part, Vector3 size, Vector3 center)
    {
        var go=new GameObject(name); go.layer=layer; go.transform.SetParent(parent,false);
        var collider=go.AddComponent<BoxCollider>(); collider.size=size; collider.center=center; collider.isTrigger=true;
        var box=go.AddComponent<HitBox>(); var so=new SerializedObject(box); so.FindProperty("hitPart").enumValueIndex=(int)part; so.ApplyModifiedPropertiesWithoutUndo(); return box;
    }
    private static void Set(UnityEngine.Object target,string property,UnityEngine.Object value)
    { var so=new SerializedObject(target); so.FindProperty(property).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void CopyVisualAsset(string source)
    {
        var path=Folder+"/VisualAssets/"+source.Substring("Assets/Test/".Length);
        EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\','/'));
        if(!AssetDatabase.CopyAsset(source,path)) throw new InvalidOperationException("자산 복사 실패: "+source);
        var originals=AssetDatabase.LoadAllAssetsAtPath(source);
        var targets=AssetDatabase.LoadAllAssetsAtPath(path);
        foreach(var original in originals)
        {
            string guid; long id; AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original,out guid,out id);
            var target=targets.FirstOrDefault(o=> {string g;long n;AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out g,out n);return id==n && o.GetType()==original.GetType();});
            if(target!=null) copies[original]=target;
        }
    }
    private static void Remap(UnityEngine.Object target)
    {
        var so=new SerializedObject(target); var it=so.GetIterator();
        while(it.Next(true)) if(it.propertyType==SerializedPropertyType.ObjectReference && it.objectReferenceValue!=null && copies.TryGetValue(it.objectReferenceValue,out var copy)) it.objectReferenceValue=copy;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        var parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path));
    }
}
