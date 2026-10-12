using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 생성 과정을 편집기 도구로 분리해 원본 모델과 게임 씬을 수정하지 않는다.
public static class SniperFiringTestBuilder
{
    private const string Folder = "Assets/Test";
    private const string ScenePath = Folder + "/SniperFiringTest.unity";

    [MenuItem("Tools/Duck Animation Test/Build Firing Test")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop play mode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new InvalidOperationException("Existing firing scene is not overwritten.");
        DuckAnimationTestBuilder.StopPreview();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SniperPreview.prefab");
        var gunSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Third_Party_Assets/DuckovImported/GameObject/IG_Gun_VPO_Deng.prefab");
        if (source == null || gunSource == null) throw new InvalidOperationException("Model or weapon visual is missing.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = "Sniper Firing Preview";
        // 함께 열려 있는 AITest와 물리적으로 겹치지 않게 테스트 공간을 분리한다.
        model.transform.position = new Vector3(100, 0, 100);
        var animationPreview = model.GetComponent<DuckAnimationPreview>();
        var animator = animationPreview.animator;
        animationPreview.clips[2].SampleAnimation(animator.gameObject, 0f);
        UnityEngine.Object.DestroyImmediate(animationPreview);
        model.GetComponent<SniperHeadPreview>()?.Apply();
        var socket = model.GetComponentsInChildren<Transform>(true).First(t => t.name == "RightHandSocket");
        var gun = (GameObject)PrefabUtility.InstantiatePrefab(gunSource, scene);
        PrefabUtility.UnpackPrefabInstance(gun, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        gun.name = "Preview VPO Deng";
        foreach (var t in gun.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var script in t.GetComponents<MonoBehaviour>()) if (script != null) UnityEngine.Object.DestroyImmediate(script);
        }
        // 발사음과 탄피 등 원작 스크립트에 의존하는 부가 연출은 실행하지 않는다.
        foreach (var p in gun.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.DestroyImmediate(p);
        foreach (var r in gun.GetComponentsInChildren<ParticleSystemRenderer>(true)) UnityEngine.Object.DestroyImmediate(r);
        foreach (var c in gun.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (var r in gun.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = Enumerable.Repeat(Material("FiringGun", new Color(0.2f, 0.22f, 0.24f)), Math.Max(1, r.sharedMaterials.Length)).ToArray();
        foreach (var f in gun.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            var copy = UnityEngine.Object.Instantiate(f.sharedMesh);
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(Folder + "/FiringGunMesh.asset"));
            f.sharedMesh = copy;
        }
        gun.transform.SetParent(socket, false);
        gun.transform.localPosition = Vector3.zero;
        gun.transform.localRotation = Quaternion.identity;
        Vector3 scale = socket.lossyScale;
        if (Mathf.Abs(scale.x * scale.y * scale.z) < 0.000001f) throw new InvalidOperationException("Invalid socket scale.");
        gun.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
        var muzzle = gun.GetComponentsInChildren<Transform>(true).First(t => t.name == "Muzzle");
        var targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        targetObject.name = "Firing Damage Target";
        targetObject.transform.position = model.transform.position + new Vector3(0, 0.8f, 6);
        targetObject.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f);
        targetObject.GetComponent<Renderer>().sharedMaterial = Material("FiringTarget", Color.cyan);
        var health = targetObject.AddComponent<HealthComponent>();

        var bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "Preview real Projectile";
        UnityEngine.Object.DestroyImmediate(bullet.GetComponent<Collider>());
        bullet.transform.localScale = Vector3.one * 0.08f;
        bullet.GetComponent<Renderer>().sharedMaterial = Material("FiringBullet", Color.yellow, true);
        var projectile = bullet.AddComponent<Projectile>();
        bullet.SetActive(false);
        var projectileAsset = PrefabUtility.SaveAsPrefabAsset(bullet, Folder + "/FiringProjectile.prefab").GetComponent<Projectile>();
        UnityEngine.Object.DestroyImmediate(bullet);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Firing Test Ground";
        ground.transform.position = model.transform.position + new Vector3(0, -0.05f, 3);
        ground.transform.localScale = Vector3.one;
        ground.GetComponent<Renderer>().sharedMaterial = Material("FiringGround", new Color(0.2f, 0.25f, 0.3f));
        var camera = new GameObject("Firing Test Camera").AddComponent<Camera>();
        camera.transform.position = model.transform.position + new Vector3(8, 5, -4);
        camera.transform.LookAt(model.transform.position + new Vector3(0, 0.8f, 2.5f));
        camera.fieldOfView = 45;
        camera.nearClipPlane = 0.01f;
        camera.depth = 100;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.1f, 0.14f, 0.18f);
        var light = new GameObject("Firing Test Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2;
        light.transform.rotation = Quaternion.Euler(45, 140, 0);
        var driver = model.AddComponent<SniperFiringPreview>();
        driver.animator = animator;
        driver.weaponVisual = gun.transform;
        driver.muzzle = muzzle;
        driver.target = health;
        driver.previewCamera = camera;
        driver.projectilePrefab = projectileAsset;
        // 원작의 밸런스 값을 변환한 데이터가 아니라 연결 검증에 필요한 명시적인 시험값이다.
        driver.definition = new WeaponDefinition
        {
            weaponId = 437, displayName = "VPO Deng connection preview", weaponType = WeaponType.Ranged,
            ammoType = AmmoType.Rifle, magazineCapacity = 3, reloadDuration = 2, fireRate = 1,
            baseDamage = 20, projectileSpeed = 20, projectileRadius = 0.025f,
            optimalRange = 10, effectiveRange = 20, minDamageRange = 30,
            projectileAddress = "Preview only", bulletsPerShot = 1
        };
        Physics.SyncTransforms();
        PrefabUtility.SaveAsPrefabAsset(model, Folder + "/SniperFiringPreview.prefab");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = model;
        Debug.Log("SniperFiring scene built: original VPO visual + project Projectile + HealthComponent. Target starts at 100 HP; three 20-damage shots expected.");
    }

    private static Material Material(string name, Color color, bool unlit = false)
    {
        string path = Folder + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
