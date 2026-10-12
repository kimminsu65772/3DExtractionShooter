using System;
using System.Collections.Generic;
using System.Linq;
using KINEMATION.KAnimationCore.Runtime.Rig;
using KINEMATION.MagicBlend.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 완성된 외형을 저장하고 원작의 애니메이션 관련 의존성만 연결한다.
public static class CommonCharacterAnimationTestBuilder
{
    private const string Folder = "Assets/Test/CommonCharacter";
    private const string ScenePath = Folder + "/CommonCharacterAnimationTest.unity";

    [MenuItem("Tools/Duck Animation Test/Build Common Character MagicBlend Test")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("플레이 모드를 먼저 종료한다.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("기존 시험 씬은 덮어쓰지 않는다.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Test/SniperPreview.prefab");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/AnimatorController/CustomCharacterAnimationController_MagicBlend.controller");
        var rigAsset = AssetDatabase.LoadAssetAtPath<KRig>(Folder + "/MonoBehaviour/Rig_0_CharacterModel_Custom_Template.asset");
        if (source == null || controller == null || rigAsset == null) throw new InvalidOperationException("필수 시험 자산을 찾을 수 없다.");
        DuckAnimationTestBuilder.StopPreview();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = "Complete Common Duck";
        model.transform.position = new Vector3(200, 0, 200);
        var oldDriver = model.GetComponent<DuckAnimationPreview>();
        var animator = oldDriver.animator;
        model.GetComponent<SniperHeadPreview>()?.Apply();
        // 눈·날개·발은 이미 조립된 자식으로 남기고 런타임 외형 조립 코드는 제거한다.
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var script in t.GetComponents<MonoBehaviour>())
                if (script != null) UnityEngine.Object.DestroyImmediate(script);
            foreach (var p in t.GetComponents<ParticleSystem>()) UnityEngine.Object.DestroyImmediate(p);
            foreach (var p in t.GetComponents<ParticleSystemRenderer>()) UnityEngine.Object.DestroyImmediate(p);
        }
        animator.runtimeAnimatorController = controller;
        animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(Folder + "/Avatar/DuckV3Avatar.asset");
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var rig = animator.gameObject.AddComponent<KRigComponent>();
        var serializedRig = new SerializedObject(rig);
        var hierarchy = serializedRig.FindProperty("hierarchy");
        hierarchy.arraySize = rigAsset.rigHierarchy.Count;
        var bones = animator.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < rigAsset.rigHierarchy.Count; i++)
        {
            var element = rigAsset.rigHierarchy[i];
            var matches = bones.Where(t => t.name == element.name).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("리그 뼈 이름이 유일하지 않다: " + element.name);
            hierarchy.GetArrayElementAtIndex(i).objectReferenceValue = matches[0];
        }
        serializedRig.ApplyModifiedPropertiesWithoutUndo();
        var blend = animator.gameObject.AddComponent<MagicBlending>();
        var blendSettings = new SerializedObject(blend);
        blendSettings.FindProperty("blendAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MagicBlendAsset>(Folder + "/MonoBehaviour/MagicBlend_Empty.asset");
        blendSettings.ApplyModifiedPropertiesWithoutUndo();

        var notes = new List<string>();
        // 원본을 수정하지 않고 복사본에서 게임 이벤트와 외형 보정 충돌만 제거한다.
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { Folder }))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var bone = animator.transform.Find(binding.path);
                if (bone == null && binding.path.Length != 0) notes.Add(clip.name + ": " + binding.path);
                if (binding.type == typeof(Transform) && bone != null &&
                    ((bone.name == "Head" && binding.propertyName.StartsWith("m_LocalScale")) ||
                     ((bone.name == "HeadTip" || bone.name == "HairSocket") &&
                       (binding.propertyName.StartsWith("m_LocalPosition") || binding.propertyName.StartsWith("m_LocalScale")))))
                    AnimationUtility.SetEditorCurve(clip, binding, null);
            }
            EditorUtility.SetDirty(clip);
        }
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Common Animation Ground";
        ground.transform.position = model.transform.position + Vector3.down * .05f;
        ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Test/FiringGround.mat");
        var camera = new GameObject("Common Animation Camera").AddComponent<Camera>();
        camera.transform.position = model.transform.position + new Vector3(3, 2, -4);
        camera.transform.LookAt(model.transform.position + Vector3.up * .8f);
        camera.fieldOfView = 40;
        camera.nearClipPlane = .01f;
        camera.depth = 200;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.13f, .16f, .2f);
        var light = new GameObject("Common Animation Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2;
        light.transform.rotation = Quaternion.Euler(40, 130, 0);
        var driver = model.AddComponent<CommonCharacterAnimationPreview>();
        driver.animator = animator;
        driver.rig = rig;
        driver.blending = blend;
        driver.previewCamera = camera;
        driver.overlays = AssetDatabase.FindAssets("t:MagicBlendAsset", new[] { Folder }).Select(g => AssetDatabase.LoadAssetAtPath<MagicBlendAsset>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(a => a.name).ToArray();
        driver.baseStates = States(controller.layers[0].stateMachine, controller.layers[0].name).ToArray();
        int handLayer = Array.FindIndex(controller.layers, l => l.name == "Hand");
        driver.handStates = States(controller.layers[handLayer].stateMachine, "Hand").ToArray();
        // 총 외형은 앞선 시험에서 정리한 메시만 가져오고 사격 제어는 복사하지 않는다.
        var firingSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Test/SniperFiringPreview.prefab");
        var gunSource = firingSource.GetComponentsInChildren<Transform>(true).First(t => t.name == "Preview VPO Deng");
        var gun = UnityEngine.Object.Instantiate(gunSource.gameObject);
        SceneManager.MoveGameObjectToScene(gun, scene);
        var socket = bones.First(t => t.name == "RightHandSocket");
        gun.transform.SetParent(socket, false);
        gun.transform.localPosition = new Vector3(-.04f, -.08f, -.28f);
        gun.transform.localRotation = Quaternion.identity;
        var scale = socket.lossyScale;
        gun.transform.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
        driver.weapon = gun.transform;
        // 재사용 프리팹에는 씬 카메라 참조를 저장하지 않는다.
        driver.previewCamera = null;
        PrefabUtility.SaveAsPrefabAsset(model, Folder + "/CompleteCommonDuck.prefab");
        driver.previewCamera = camera;
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = model;
        Debug.Log("Common MagicBlend test prepared. Unmatched curve paths (not runtime validation):\n" + string.Join("\n", notes.Distinct()));
    }

    [MenuItem("Tools/Duck Animation Test/Build Visible Hand Mount Test")]
    public static void BuildVisibleHandMountTest()
    {
        const string path = Folder + "/VisibleHandMountTest.unity";
        if (Application.isPlaying) throw new InvalidOperationException("플레이 모드를 먼저 종료한다.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            throw new InvalidOperationException("기존 비교 시험은 덮어쓰지 않는다.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CompleteCommonDuck.prefab");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = "Visible Hand Mount Preview";
        model.transform.position = new Vector3(300, 0, 300);
        var driver = model.GetComponent<CommonCharacterAnimationPreview>();
        var bones = driver.animator.GetComponentsInChildren<Transform>(true);
        var visibleHand = bones.First(t => t.name == "Wings.R");
        var oldSocket = bones.First(t => t.name == "RightHandSocket");
        // 보이는 손과 총이 같은 부모의 움직임을 따르게 한다. 매 프레임 보정하지 않는다.
        var holder = new GameObject("VisibleHandWeaponHolder").transform;
        holder.SetParent(visibleHand, false);
        holder.localPosition = Vector3.zero;
        // 비교 시작 시 총의 기존 축만 보존하고 이전 시험 위치 오프셋은 제거한다.
        holder.rotation = oldSocket.rotation;
        var gun = driver.weapon;
        gun.SetParent(holder, false);
        gun.localPosition = Vector3.zero;
        gun.localRotation = Quaternion.identity;
        var scale = holder.lossyScale;
        gun.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
        // VPO 원본 장착은 프리팹 원점을 소켓에 맞춘다. 별도 그립은 이 원점을 표시한다.
        var grip = new GameObject("Grip_OriginalMountOrigin").transform;
        grip.SetParent(gun, false);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Visible Hand Test Ground";
        ground.transform.position = model.transform.position + Vector3.down * .05f;
        ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Test/FiringGround.mat");
        var camera = new GameObject("Visible Hand Test Camera").AddComponent<Camera>();
        camera.transform.position = model.transform.position + new Vector3(3, 2, -4);
        camera.transform.LookAt(model.transform.position + Vector3.up * .8f);
        camera.fieldOfView = 40;
        camera.nearClipPlane = .01f;
        camera.depth = 300;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.13f, .16f, .2f);
        var light = new GameObject("Visible Hand Test Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2;
        light.transform.rotation = Quaternion.Euler(40, 130, 0);
        driver.previewCamera = camera;
        // 카메라를 고유 씬에 두고 이전 시험 씬은 그대로 보존한다.
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = model;
        Debug.Log("Visible hand mount prepared: Wings.R > VisibleHandWeaponHolder > weapon. Grip matches holder; visual grip fit remains to be checked.");
        // 원본 상태를 복원하는 편집기 샘플링으로 움직임 동안 상대 배치가 유지되는지 검사한다.
        float maxError = 0;
        AnimationMode.StartAnimationMode();
        try
        {
            foreach (string name in new[] { "Armature_Idle_1", "Armature_Walk_Forward_0", "Armature_Walk_Right", "Armature_Pose_Aim" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/AnimationClip/" + name + ".anim");
                for (int i = 0; i < 3; i++)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(driver.animator.gameObject, clip, clip.length * i / 3);
                    AnimationMode.EndSampling();
                    maxError = Mathf.Max(maxError, Vector3.Distance(grip.position, holder.position));
                    if (Vector3.Distance(visibleHand.position, holder.position) > .0001f ||
                        Quaternion.Angle(grip.rotation, holder.rotation) > .01f)
                        throw new InvalidOperationException("손 외형 기준과 무기 장착 기준이 분리됐다.");
                }
            }
        }
        finally { AnimationMode.StopAnimationMode(); }
        Debug.Log("VisibleHand structural samples=12; maximum grip/holder distance=" + maxError + ". This checks hierarchy following, not visual contact or runtime MagicBlend.");
    }

    [MenuItem("Tools/Duck Animation Test/Use Project Weapon Mount")]
    public static void UseProjectWeaponMount()
    {
        if (Application.isPlaying) throw new InvalidOperationException("플레이 모드를 먼저 종료한다.");
        var scene = SceneManager.GetActiveScene();
        var preview = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<CommonCharacterAnimationPreview>(true)).First();
        var hand = preview.animator.GetComponentsInChildren<Transform>(true).First(t => t.name == "Wings.R");
        var aim = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/AnimationClip/Armature_Aim.anim");
        Quaternion mountLocalRotation;
        // 조준 클립의 손 회전으로부터 캐릭터 전방을 향하는 고정 장착 축을 구한다.
        AnimationMode.StartAnimationMode();
        try
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(preview.animator.gameObject, aim, 0);
            AnimationMode.EndSampling();
            var forward = Vector3.ProjectOnPlane(preview.transform.forward, Vector3.up).normalized;
            mountLocalRotation = Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(forward, Vector3.up);
        }
        finally { AnimationMode.StopAnimationMode(); }
        var mount = hand.Find("ProjectWeaponMount");
        if (mount == null) { mount = new GameObject("ProjectWeaponMount").transform; mount.SetParent(hand, false); }
        // 날개 원점인 어깨가 아니라 손 끝 쪽에 장착 지점을 둔다. 접촉은 육안 미세 조정 대상이다.
        mount.localPosition = new Vector3(-.05f, .34f, -.09f);
        mount.localRotation = mountLocalRotation;
        var gun = preview.weapon;
        var grip = gun.Find("ProjectGrip");
        if (grip == null) { grip = new GameObject("ProjectGrip").transform; grip.SetParent(gun, false); }
        grip.localPosition = new Vector3(0, -.03f, .02f);
        grip.localRotation = Quaternion.identity;
        gun.SetParent(mount, false);
        gun.localScale = Vector3.one;
        gun.localRotation = Quaternion.identity;
        gun.localPosition = -grip.localPosition;
        var view = gun.GetComponent<WeaponView>() ?? gun.gameObject.AddComponent<WeaponView>();
        var fields = new SerializedObject(view);
        fields.FindProperty("gripTransform").objectReferenceValue = grip;
        fields.FindProperty("muzzleTransform").objectReferenceValue = gun.Find("Sockets/Muzzle");
        fields.ApplyModifiedPropertiesWithoutUndo();
        var driver = preview.GetComponent<ProjectWeaponMountPreview>() ?? preview.gameObject.AddComponent<ProjectWeaponMountPreview>();
        driver.weaponView = view;
        driver.weaponMount = mount;
        EditorUtility.SetDirty(driver);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = mount.gameObject;
        Debug.Log("Project WeaponView mount prepared. Mount local rotation=" + mount.localEulerAngles + "; position=" + mount.localPosition);
    }

    private static IEnumerable<string> States(AnimatorStateMachine machine, string path)
    {
        foreach (var state in machine.states) yield return path + "." + state.state.name;
        foreach (var child in machine.stateMachines)
            foreach (string state in States(child.stateMachine, path + "." + child.stateMachine.name)) yield return state;
    }
}
