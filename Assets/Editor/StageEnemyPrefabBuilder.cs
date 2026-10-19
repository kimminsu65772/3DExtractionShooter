using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 저장된 테스트 분대는 보존하고, 외부 씬 참조를 제거한 스폰용 복사본만 만든다.
public static class StageEnemyPrefabBuilder
{
    public const string Folder = "Assets/Characters/GameCharacters/Spawning";
    public const string DataFolder = "Assets/GameData/EnemySpawns";
    private const string SourceScene = "Assets/Scenes/Level_GroundZero/Level_GroundZero_1.unity";
    private const string PatoFolder = "Assets/Characters/PatoChapo";

    [MenuItem("Tools/ExtractionShooter/Characters/Create Stage Spawn Prefabs")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 실행한다.");
        foreach (string name in new[] { "Enemy_Ranged", "Enemy_USEC", "Squad_PatoChapo" })
            if (AssetDatabase.LoadMainAssetAtPath(Folder + "/" + name + ".prefab") != null ||
                AssetDatabase.LoadMainAssetAtPath(DataFolder + "/" + name + ".asset") != null)
                throw new InvalidOperationException("기존 스폰 자산을 덮어쓰지 않는다: " + name);
        EnsureFolder(Folder);
        EnsureFolder(DataFolder);
        CopyIndividual("Enemy_Default", "Enemy_Ranged");
        CopyIndividual("Enemy_USEC", "Enemy_USEC");
        BuildSquad();
        foreach (string name in new[] { "Enemy_Ranged", "Enemy_USEC", "Squad_PatoChapo" })
        {
            var definition = ScriptableObject.CreateInstance<EnemySpawnDefinition>();
            var data = new SerializedObject(definition);
            data.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".prefab");
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, DataFolder + "/" + name + ".asset");
        }
    }

    private static void CopyIndividual(string sourceName, string name)
    {
        var root = PrefabUtility.LoadPrefabContents(DuckGameplayPrefabBuilder.Folder + "/" + sourceName + ".prefab");
        try
        {
            root.name = name;
            root.SetActive(false);
            ClearExternalSceneReferences(root);
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void BuildSquad()
    {
        var scene = EditorSceneManager.OpenPreviewScene(SourceScene);
        try
        {
            var commander = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SquadCommander>(true)).Single();
            var root = commander.gameObject;
            root.SetActive(false);
            root.name = "Squad_PatoChapo";
            var members = root.GetComponentsInChildren<SquadMemberBrain>(true);
            var anchor = commander.Leader.transform.position;
            var positions = members.ToDictionary(m => m, m => m.transform.position - anchor);
            root.transform.position = Vector3.zero;
            foreach (var member in members)
            {
                member.transform.position = positions[member];
                if (PrefabUtility.IsPartOfPrefabInstance(member))
                    PrefabUtility.UnpackPrefabInstance(member.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                bool isLeader = member == commander.Leader;
                DuckGameplayPrefabBuilder.ReplaceVisual(member.gameObject,
                    PatoFolder + "/Prefabs/" + (isLeader ? "Duck_PatoChapo" : "Duck_PatoChapoGuard") + ".prefab", isLeader);
                // 외형이 USEC와 같아도 회피 능력을 상속하지 않는다. 분대 BT가 이동·공격을 판단한다.
                foreach (var dodge in member.GetComponents<EnemyHitDodge>()) UnityEngine.Object.DestroyImmediate(dodge);
                foreach (var dodge in member.GetComponents<EnemyDodgeController>()) UnityEngine.Object.DestroyImmediate(dodge);
                member.GetComponent<EnemyRangedBrain>().enabled = false;
                member.enabled = true;
                var animation = new SerializedObject(member.GetComponent<EnemyAnimationController>());
                animation.FindProperty("weaponLoadout").objectReferenceValue = member.GetComponent<EnemyWeaponLoadout>();
                if (isLeader)
                {
                    var controller = BuildPatoAnimation(member.GetComponentInChildren<Animator>(true));
                    animation.FindProperty("animationSet").objectReferenceValue = controller;
                }
                animation.ApplyModifiedPropertiesWithoutUndo();
            }
            ClearExternalSceneReferences(root);
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Squad_PatoChapo.prefab");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static RuntimeAnimatorController BuildPatoAnimation(Animator animator)
    {
        var common = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Characters/DuckEnemyCopy/DuckEnemy.overrideController");
        string controllerPath = Folder + "/PatoChapo_Gameplay.controller";
        string maskPath = Folder + "/PatoChapo_UpperBody.mask";
        string overridePath = Folder + "/PatoChapo_Gameplay.overrideController";
        if (AssetDatabase.LoadMainAssetAtPath(controllerPath) != null || AssetDatabase.LoadMainAssetAtPath(maskPath) != null ||
            AssetDatabase.LoadMainAssetAtPath(overridePath) != null)
            throw new InvalidOperationException("기존 꼬마덕 애니메이션 자산을 덮어쓰지 않는다.");
        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(common.runtimeAnimatorController), controllerPath))
            throw new InvalidOperationException("꼬마덕 게임용 Controller 복사에 실패했다.");
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
        var mask = new AvatarMask();
        var bones = animator.GetComponentsInChildren<Transform>(true);
        mask.transformCount = bones.Length;
        for (int i = 0; i < bones.Length; i++)
        {
            string path = AnimationUtility.CalculateTransformPath(bones[i], animator.transform);
            mask.SetTransformPath(i, path);
            mask.SetTransformActive(i, path.Contains("/Arm.Root."));
        }
        AssetDatabase.CreateAsset(mask, maskPath);
        var layers = controller.layers;
        for (int i = 0; i < layers.Length; i++)
            if (layers[i].name == "UpperBody") layers[i].avatarMask = mask;
        controller.layers = layers;

        var mappings = new Dictionary<string, string>
        {
            { "Idle", "Duck_Armature_Idle" },
            { "WalkForward", "Duck_Armature_Walk.Forward_0" },
            { "WalkBackward", "Duck_Armature_Walk.Backward" },
            { "WalkLeft", "Duck_Armature_Walk.Left" },
            { "WalkRight", "Duck_Armature_Walk.Right" },
            { "Aim", "Duck_Armature_Idle.RightHandOut" }
        };
        var clips = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        common.GetOverrides(clips);
        // 상태 전이는 기존 게임 코드를 사용하고, 실제 모션은 전용 리그에 맞는 원본 클립을 사용한다.
        for (int i = 0; i < clips.Count; i++)
        {
            var effective = clips[i].Value != null ? clips[i].Value : clips[i].Key;
            if (mappings.TryGetValue(effective.name, out string clipName))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PatoFolder + "/Animations/" + clipName + ".anim");
                if (clip == null) throw new InvalidOperationException("꼬마덕 클립이 없다: " + clipName);
                clips[i] = new KeyValuePair<AnimationClip, AnimationClip>(clips[i].Key, clip);
            }
        }
        var overrides = new AnimatorOverrideController(controller);
        overrides.ApplyOverrides(clips);
        AssetDatabase.CreateAsset(overrides, overridePath);
        AssetDatabase.SaveAssetIfDirty(controller);
        animator.runtimeAnimatorController = overrides;
        return overrides;
    }

    private static void ClearExternalSceneReferences(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) throw new InvalidOperationException("누락된 스크립트가 있다.");
            var data = new SerializedObject(component);
            var property = data.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var value = property.objectReferenceValue;
                if (value == null || EditorUtility.IsPersistent(value)) continue;
                var referenced = value as Component;
                var referencedObject = value as GameObject;
                var target = referenced != null ? referenced.transform : referencedObject != null ? referencedObject.transform : null;
                if (target != null && target != root.transform && !target.IsChildOf(root.transform))
                    property.objectReferenceValue = null;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        int separator = folder.LastIndexOf('/');
        string parent = folder.Substring(0, separator);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder.Substring(separator + 1));
    }
}
