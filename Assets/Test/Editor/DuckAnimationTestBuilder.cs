using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DuckAnimationTestBuilder
{
    private const string Folder = "Assets/Test";
    private const string ScenePath = Folder + "/DuckAnimationTest.unity";
    private static GameObject preview;
    private static AnimationClip previewClip;
    private static double started;

    [MenuItem("Tools/Duck Animation Test/Build")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Test scene already exists. Existing work is not overwritten.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Third_Party_Assets/DuckovImported/GameObject/0_CharacterModel_Custom_Enemy.prefab");
        if (source == null) throw new InvalidOperationException("Original enemy prefab is missing.");
        // 기존 씬의 저장하지 않은 변경을 보존하기 위해 별도 씬을 추가로 연다.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        preview = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        preview.name = "Duck Animation Preview";
        PrefabUtility.UnpackPrefabInstance(preview, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        int removed = 0;
        foreach (var t in preview.GetComponentsInChildren<Transform>(true))
        {
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            // 복사본의 외부 게임 스크립트만 제거한다.
            foreach (var behaviour in t.GetComponents<MonoBehaviour>())
                if (behaviour != null) UnityEngine.Object.DestroyImmediate(behaviour);
        }
        preview.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var animator = preview.GetComponentInChildren<Animator>(true);
        if (animator == null) throw new InvalidOperationException("Animator is missing.");
        // 원본을 변경하지 않고 테스트용 메시와 아바타를 저장한다.
        var copies = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        foreach (var r in preview.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            r.sharedMesh = Copy(r.sharedMesh, copies);
            r.updateWhenOffscreen = true;
        }
        foreach (var f in preview.GetComponentsInChildren<MeshFilter>(true)) f.sharedMesh = Copy(f.sharedMesh, copies);
        animator.avatar = Copy(animator.avatar, copies);
        var paths = new[] { "Armature_Idle_1.anim", "Armature_Walk_Forward.anim", "Armature_Aim.anim" };
        var labels = new[] { "Idle", "Walk", "Aim" };
        var clips = paths.Select(p => Copy(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Third_Party_Assets/DuckovImported/AnimationClip/" + p), copies)).ToArray();
        if (clips.Any(c => c == null)) throw new InvalidOperationException("A required animation clip is missing.");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/DuckPreview.controller");
        for (int i = 0; i < clips.Length; i++)
        {
            // 클립의 원래 이벤트는 게임 코드에 의존하므로 테스트 복사본에서만 제거한다.
            AnimationUtility.SetAnimationEvents(clips[i], Array.Empty<AnimationEvent>());
            var state = controller.layers[0].stateMachine.AddState(labels[i]);
            state.motion = clips[i];
            if (i == 0) controller.layers[0].stateMachine.defaultState = state;
        }
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // 원본 머티리얼은 공유 참조만 사용한다. 원본 외형은 수정하지 않는다.
        var driver = preview.AddComponent<DuckAnimationPreview>();
        driver.animator = animator;
        driver.clips = clips;
        driver.labels = labels;
        var renderers = preview.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("No renderers found.");
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
        var camera = new GameObject("Test Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = bounds.center + new Vector3(0, radius * 0.5f, radius * 2.8f);
        camera.transform.LookAt(bounds.center);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = Mathf.Max(100, radius * 20);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.16f, 0.2f, 0.24f);
        var light = new GameObject("Test Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
        light.transform.rotation = Quaternion.Euler(40, 140, 0);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Test Ground";
        ground.transform.position = new Vector3(0, bounds.min.y - 0.02f, 0);
        ground.transform.localScale = Vector3.one * Mathf.Max(radius, 1);
        PrefabUtility.SaveAsPrefabAsset(preview, Folder + "/DuckPreview.prefab");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = preview;
        SceneView.lastActiveSceneView?.Frame(bounds, false);
        var checks = new List<string>();
        foreach (var clip in clips)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            int matched = bindings.Count(b => string.IsNullOrEmpty(b.path) || animator.transform.Find(b.path) != null);
            clip.SampleAnimation(animator.gameObject, 0);
            var bones = animator.GetComponentsInChildren<Transform>(true);
            var rotations = bones.Select(b => b.localRotation).ToArray();
            clip.SampleAnimation(animator.gameObject, clip.length * 0.45f);
            int moving = bones.Where((b, i) => Quaternion.Angle(rotations[i], b.localRotation) > 0.01f).Count();
            checks.Add(clip.name + ": bindings=" + matched + "/" + bindings.Length + ", animatedBones=" + moving);
        }
        clips[0].SampleAnimation(animator.gameObject, 0);
        Debug.Log("DuckAnimationTest built. Removed missing scripts=" + removed + "; renderers=" + renderers.Length + "; " + string.Join("; ", checks));
    }

    private static T Copy<T>(T original, Dictionary<UnityEngine.Object, UnityEngine.Object> copies) where T : UnityEngine.Object
    {
        if (original == null) return null;
        if (copies.TryGetValue(original, out var existing)) return (T)existing;
        var clone = UnityEngine.Object.Instantiate(original);
        clone.name = original.name;
        AssetDatabase.CreateAsset(clone, AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + original.GetType().Name + "_" + original.name.Replace('|', '_').Replace('/', '_') + ".asset"));
        copies.Add(original, clone);
        return clone;
    }

    [MenuItem("Tools/Duck Animation Test/Start Walk Preview")]
    public static void StartPreview()
    {
        preview = GameObject.Find("Sniper Animation Preview") ?? GameObject.Find("Duck Animation Preview");
        if (preview == null) throw new InvalidOperationException("Build the test scene first.");
        previewClip = preview.GetComponent<DuckAnimationPreview>().clips[1];
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Duck Animation Test/Stop Preview")]
    public static void StopPreview()
    {
        EditorApplication.update -= Tick;
        if (preview != null)
        {
            preview.GetComponent<DuckAnimationPreview>().clips[0].SampleAnimation(preview.GetComponentInChildren<Animator>().gameObject, 0);
            preview.GetComponent<SniperHeadPreview>()?.Apply();
        }
    }

    private static void Tick()
    {
        if (preview == null || previewClip == null || EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        // 플레이 모드에 들어가지 않고 실제 뼈대를 샘플링해 기존 씬의 게임 로직 실행을 막는다.
        var animator = preview.GetComponentInChildren<Animator>();
        previewClip.SampleAnimation(animator.gameObject, (float)((EditorApplication.timeSinceStartup - started) % previewClip.length));
        preview.GetComponent<SniperHeadPreview>()?.Apply();
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Duck Animation Test/Audit")]
    public static void Audit()
    {
        var root = GameObject.Find("Sniper Animation Preview") ?? GameObject.Find("Duck Animation Preview");
        var a = root.GetComponentInChildren<Animator>();
        foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            Debug.Log("DuckAudit renderer=" + r.name + "; active=" + r.gameObject.activeInHierarchy + "; mesh=" + r.sharedMesh.name + "; vertices=" + r.sharedMesh.vertexCount + "; blendShapes=" + r.sharedMesh.blendShapeCount + "; materials=" + string.Join(",", r.sharedMaterials.Select(m => m == null ? "NULL" : m.name + "/" + m.shader.name)));
        foreach (var c in root.GetComponent<DuckAnimationPreview>().clips)
            Debug.Log("DuckAudit missing paths " + c.name + ": " + string.Join(",", AnimationUtility.GetCurveBindings(c).Where(b => !string.IsNullOrEmpty(b.path) && a.transform.Find(b.path) == null).Select(b => b.path).Distinct()));
    }

    [MenuItem("Tools/Duck Animation Test/Use Preview Materials")]
    public static void PreviewMaterials()
    {
        // 추출 셰이더의 재현 여부와 애니메이션 재생을 분리해 확인한다.
        var root = GameObject.Find("Duck Animation Preview");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader not found.");
        var map = new Dictionary<Material, Material>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var original = materials[i];
                if (original == null) continue;
                if (!map.TryGetValue(original, out var material))
                {
                    material = new Material(shader);
                    material.name = "Preview_" + original.name;
                    material.color = original.name.Contains("Eye") ? Color.black : original.name.Contains("Mouth") ? new Color(1f, 0.65f, 0.1f) : new Color(0.65f, 0.75f, 0.8f);
                    AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + material.name + ".mat"));
                    map.Add(original, material);
                }
                materials[i] = material;
            }
            r.sharedMaterials = materials;
            Debug.Log("DuckAudit visible=" + r.name + "; active=" + r.gameObject.activeInHierarchy + "; bounds=" + r.bounds);
        }
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/DuckPreview.prefab");
        EditorSceneManager.SaveScene(root.scene, ScenePath);
        AssetDatabase.SaveAssets();
    }
}
