using System;
using System.Linq;
using KINEMATION.KAnimationCore.Runtime.Rig;
using KINEMATION.MagicBlend.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 기존 비교 씬을 덮어쓰지 않고 직접 클립 재생과 프로젝트 무기 장착만 연결한다.
public static class DirectClipWeaponTestBuilder
{
    private const string Folder = "Assets/Test/CommonCharacter";
    [MenuItem("Tools/Duck Animation Test/Extend Direct Clip Weapon Test")]
    public static void Extend()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != Folder + "/DirectClipWeaponTest.unity")
            throw new InvalidOperationException("직접 클립 시험 씬을 열고 플레이를 중지한다.");
        var driver = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DirectClipWeaponPreview>()).Single();
        var controller = (AnimatorController)driver.animator.runtimeAnimatorController;
        var machine = controller.layers[0].stateMachine;
        string[] names = { "WalkLeft", "WalkRight", "WalkBackward", "Roll" };
        string[] clips = { "Armature_Walk_Left", "Armature_Walk_Right", "Armature_Walk_Backward", "Armature_DodgeRoll_Forward" };
        for (int i = 0; i < names.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/AnimationClip/" + clips[i] + ".anim");
            if (clip == null) throw new InvalidOperationException("필수 클립이 없다: " + clips[i]);
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == names[i]) ?? machine.AddState(names[i]);
            state.motion = clip;
        }
        var shooter = driver.GetComponent<DirectClipShotPreview>() ?? driver.gameObject.AddComponent<DirectClipShotPreview>();
        shooter.weaponView = driver.GetComponent<ProjectWeaponMountPreview>().weaponView;
        shooter.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Test/FiringProjectile.prefab").GetComponent<Projectile>();
        if (shooter.target == null)
        {
            var targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = "Direct Clip Damage Target";
            targetObject.transform.position = driver.transform.position + new Vector3(0, .8f, 6);
            targetObject.transform.localScale = new Vector3(.8f, 1.6f, .8f);
            targetObject.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Test/FiringTarget.mat");
            shooter.target = targetObject.AddComponent<HealthComponent>();
        }
        // 발사 연결만 검증하는 시험값이며 실제 무기 CSV를 변경하지 않는다.
        shooter.definition = new WeaponDefinition
        {
            weaponId = 437, displayName = "Direct attachment trial", weaponType = WeaponType.Ranged,
            ammoType = AmmoType.Rifle, magazineCapacity = 3, reloadDuration = 2, fireRate = 1,
            baseDamage = 20, projectileSpeed = 20, projectileRadius = .025f,
            optimalRange = 10, effectiveRange = 20, minDamageRange = 30,
            projectileAddress = "Preview only", bulletsPerShot = 1
        };
        driver.shotPreview = shooter;
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Tools/Duck Animation Test/Build Direct Clip Weapon Test")]
    public static void Build()
    {
        string path = Folder + "/DirectClipWeaponTest.unity";
        if (Application.isPlaying || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            throw new InvalidOperationException("플레이 중이거나 기존 시험 씬이 있으면 생성하지 않는다.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CompleteCommonDuck.prefab");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = "Direct Clip Weapon Preview";
        model.transform.position = new Vector3(400, 0, 400);
        var old = model.GetComponent<CommonCharacterAnimationPreview>();
        var animator = old.animator;
        var gun = old.weapon;
        UnityEngine.Object.DestroyImmediate(old);
        foreach (var component in model.GetComponentsInChildren<MagicBlending>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in model.GetComponentsInChildren<KRigComponent>(true)) UnityEngine.Object.DestroyImmediate(component);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/DirectClipWeapon.controller");
        var machine = controller.layers[0].stateMachine;
        var names = new[] { "Idle", "Walk", "Aim" };
        var clips = new[] { "Armature_Idle_1", "Armature_Walk_Forward_0", "Armature_Aim" };
        AnimationClip aim = null;
        for (int i = 0; i < names.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/AnimationClip/" + clips[i] + ".anim");
            if (clip == null) throw new InvalidOperationException("필수 클립이 없다: " + clips[i]);
            var state = machine.AddState(names[i]);
            state.motion = clip;
            if (i == 0) machine.defaultState = state;
            if (i == 2) aim = clip;
        }
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        var hand = animator.GetComponentsInChildren<Transform>(true).First(t => t.name == "Wings.R");
        Quaternion mountRotation;
        AnimationMode.StartAnimationMode();
        try
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, aim, 0);
            AnimationMode.EndSampling();
            mountRotation = Quaternion.Inverse(hand.rotation) * model.transform.rotation;
        }
        finally { AnimationMode.StopAnimationMode(); }
        var mount = new GameObject("ProjectWeaponMount").transform;
        mount.SetParent(hand, false);
        mount.localPosition = new Vector3(-.05f, .34f, -.09f);
        mount.localRotation = mountRotation;
        gun.SetParent(mount, false);
        gun.localRotation = Quaternion.identity;
        gun.localScale = Vector3.one;
        var grip = new GameObject("ProjectGrip").transform;
        grip.SetParent(gun, false);
        grip.localPosition = new Vector3(0, -.03f, .02f);
        gun.localPosition = -grip.localPosition;
        var view = gun.gameObject.AddComponent<WeaponView>();
        var settings = new SerializedObject(view);
        settings.FindProperty("gripTransform").objectReferenceValue = grip;
        settings.FindProperty("muzzleTransform").objectReferenceValue = gun.Find("Sockets/Muzzle");
        settings.ApplyModifiedPropertiesWithoutUndo();
        var attachment = model.AddComponent<ProjectWeaponMountPreview>();
        attachment.weaponView = view;
        attachment.weaponMount = mount;
        var camera = new GameObject("Direct Clip Test Camera").AddComponent<Camera>();
        camera.transform.position = model.transform.position + new Vector3(3, 2, -4);
        camera.transform.LookAt(model.transform.position + Vector3.up * .8f);
        camera.fieldOfView = 40;
        camera.nearClipPlane = .01f;
        camera.depth = 400;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.1f, .12f, .14f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.position = model.transform.position + Vector3.down * .05f;
        ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Test/FiringGround.mat");
        var light = new GameObject("Direct Clip Test Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2;
        light.transform.rotation = Quaternion.Euler(40, 130, 0);
        var driver = model.AddComponent<DirectClipWeaponPreview>();
        driver.animator = animator;
        driver.previewCamera = camera;
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.SaveAssets();
        Extend();
        Selection.activeGameObject = mount.gameObject;
        Debug.Log("Direct Clip Weapon Test prepared: no MagicBlend, Animator Idle/Walk/Aim and project WeaponView attachment.");
    }
}
