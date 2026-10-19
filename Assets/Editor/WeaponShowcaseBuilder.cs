using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using ExtractionShooter.DataPipeline;

// 원본 외형을 게임용 WeaponView로 바꾸고 네 자세를 비교하는 독립 씬을 만든다.
public static class WeaponShowcaseBuilder
{
    public const string Folder = "Assets/Art/Weapons/Duckov";
    public const string ScenePath = "Assets/Test/WeaponShowcase/WeaponShowcase.unity";
    private const string Source = "C:/Users/admin/Desktop/AssetRipper_export_20261007_063805/ExportedProject/Assets/GameObject/";
    private static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();

    [MenuItem("Tools/ExtractionShooter/Weapons/Build CSV Weapon Showcase")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료한다.");
        if (File.Exists(ScenePath)) throw new InvalidOperationException("기존 비교 씬은 덮어쓰지 않는다.");
        var report = new DataReport();
        WeaponDefinition[] rows;
        using (var text = File.OpenText("Assets/GameData/Weapons/weapons.csv"))
            rows = new CsvTableLoader<WeaponDefinition>().Load(new CsvHelperRecordReader().Read(text, "weapons.csv", report),
                new GeneratedWeaponBinding(), new WeaponValidator(), report).ToArray();
        if (report.HasErrors) throw new InvalidOperationException(string.Join("; ", report.Issues.Select(i => i.Message)));
        var names = new Dictionary<string, string> { { "AK-47", "AK47" }, { "Glick", "GLick" }, { "MP-155", "MP155" }, { "MF", "MF" } };
        if (rows.Any(r => !names.ContainsKey(r.displayName))) throw new InvalidOperationException("새 CSV 무기의 원본 외형 매핑을 추가한다.");
        Directory.CreateDirectory(Folder + "/Prefabs");
        Directory.CreateDirectory(Folder + "/Materials");
        AssetDatabase.Refresh();
        Materials.Clear();
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("Addressables 설정이 없다.");
        var addresses = new string[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            string name = names[rows[i].displayName];
            string path = Folder + "/Prefabs/" + name + ".prefab";
            // 원본 GUID를 사용해 이미 가져온 의존성도 재사용한다. 사용자 무기 프리팹은 수정하지 않는다.
            string guid = File.ReadLines(Source + "IG_Gun_" + name + ".prefab.meta").First(l => l.StartsWith("guid: ")).Substring(6);
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (original == null) throw new InvalidOperationException("원본 무기 또는 의존성 임포트가 필요하다: " + name);
            if (!File.Exists(path)) Assemble(original, path, name);
            addresses[i] = "duckov/weapons/" + rows[i].weaponId;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), settings.DefaultGroup);
            entry.address = addresses[i];
        }
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true);
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var showcase = new GameObject("Weapon Showcase", typeof(WeaponShowcase)).GetComponent<WeaponShowcase>();
            showcase.weaponCsv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/GameData/Weapons/weapons.csv");
            showcase.ammoCsv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/GameData/Ammo/ammo.csv");
            showcase.viewAddresses = addresses;
            showcase.inactiveRoot = new GameObject("Inactive Pool").transform;
            showcase.inactiveRoot.gameObject.SetActive(false);
            showcase.stations = new WeaponShowcaseStation[4];
            var sourcePlayer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/GameCharacters/Player_YellowDuck.prefab");
            if (sourcePlayer == null) throw new InvalidOperationException("노란 오리 플레이어 프리팹이 없다.");
            string[] labels = { "IDLE", "RUN", "AIM", "SHOT" };
            for (int i = 0; i < 4; i++)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(sourcePlayer, scene);
                PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                player.name = labels[i] + " - Yellow Player";
                // 복사본에서 입력과 게임 로직을 제거해 네 Animator가 서로 간섭하지 않게 한다.
                // RequireComponent의 역방향 의존 때문에 첫 제거가 거부된 컴포넌트도 다시 제거한다.
                for (int pass = 0; pass < 8 && player.GetComponentsInChildren<MonoBehaviour>(true).Length > 0; pass++)
                    foreach (var script in player.GetComponentsInChildren<MonoBehaviour>(true).Reverse()) UnityEngine.Object.DestroyImmediate(script);
                if (player.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    throw new InvalidOperationException("비교용 복사본에 게임 스크립트가 남았다.");
                foreach (var collider in player.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var t in player.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
                var animator = player.GetComponentInChildren<Animator>(true);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var mount = player.GetComponentsInChildren<Transform>(true).Single(t => t.name == "WeaponMount");
                // 조준 자세에서 홀더 축을 총구 전방과 맞추고 로컬 회전으로 저장한다.
                // 런타임에는 손 본의 움직임을 그대로 따라가며 매 프레임 방향을 덮어쓰지 않는다.
                var aim = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/DuckPlayerCopy/Clips/Aim.anim");
                aim.SampleAnimation(animator.gameObject, 0);
                mount.rotation = Quaternion.LookRotation(player.transform.forward, player.transform.up);
                var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/DuckPlayerCopy/Clips/Idle.anim");
                idle.SampleAnimation(animator.gameObject, 0);
                player.transform.position = new Vector3((i - 1.5f) * 2.7f, 0, 0);
                var station = animator.gameObject.AddComponent<WeaponShowcaseStation>();
                station.animator = animator;
                station.mount = mount;
                station.showcase = showcase;
                station.pose = i;
                showcase.stations[i] = station;
            }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Preview Floor";
            floor.transform.position = new Vector3(0, -0.15f, 1);
            floor.transform.localScale = new Vector3(17, 0.3f, 12);
            var floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMaterial.SetColor("_BaseColor", new Color(0.22f, 0.25f, 0.28f));
            string floorPath = "Assets/Test/WeaponShowcase/Floor.mat";
            if (!File.Exists(floorPath)) AssetDatabase.CreateAsset(floorMaterial, floorPath);
            else { UnityEngine.Object.DestroyImmediate(floorMaterial); floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(floorPath); }
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            var camera = new GameObject("Showcase Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(5, 4.5f, 10);
            camera.transform.LookAt(new Vector3(0, 0.9f, 0));
            camera.orthographic = true; camera.orthographicSize = 4.2f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.1f, 0.12f, 0.16f);
            showcase.previewCamera = camera;
            showcase.stationCameras = new Camera[4];
            for (int i = 0; i < 4; i++)
            {
                var stationCamera = i == 0 ? camera : new GameObject("Camera " + labels[i], typeof(Camera)).GetComponent<Camera>();
                Vector3 center = showcase.stations[i].transform.root.position + new Vector3(0, 0.55f, 0);
                stationCamera.transform.position = center + new Vector3(1.6f, 0.9f, 3);
                stationCamera.transform.LookAt(center);
                stationCamera.orthographic = true; stationCamera.orthographicSize = 0.9f;
                stationCamera.rect = new Rect(i * 0.25f, 0, 0.25f, 0.7f);
                stationCamera.clearFlags = CameraClearFlags.SolidColor;
                stationCamera.backgroundColor = new Color(0.12f + i * 0.01f, 0.14f, 0.18f);
                showcase.stationCameras[i] = stationCamera;
            }
            var light = new GameObject("Showcase Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("WeaponShowcase built: " + string.Join(", ", rows.Select(r => r.weaponId + " " + r.displayName)));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    public static void Assemble(GameObject source, string path, string name)
    {
        var root = new GameObject(name);
        try
        {
            var visual = UnityEngine.Object.Instantiate(source, root.transform, false);
            visual.name = "Original Visual";
            RemoveEmptyMeshHolders(source, visual);
            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach (var script in t.GetComponents<MonoBehaviour>()) UnityEngine.Object.DestroyImmediate(script);
                t.gameObject.layer = 0;
            }
            foreach (var particle in visual.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.DestroyImmediate(particle.gameObject);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(ConvertMaterial).ToArray();
            foreach (var mesh in visual.GetComponentsInChildren<MeshFilter>(true))
                if (mesh.sharedMesh == null) throw new InvalidOperationException("메시 의존성이 없다: " + name);
            var muzzle = visual.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Muzzle");
            // 원작의 루트 원점이 손의 장착 기준이다. 외형 내부 오프셋과 총구를 함께 회전한다.
            visual.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(muzzle.forward, muzzle.up));
            var grip = new GameObject("Grip").transform;
            grip.SetParent(root.transform, false);
            var view = root.AddComponent<WeaponView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("muzzleTransform").objectReferenceValue = muzzle;
            serialized.FindProperty("gripTransform").objectReferenceValue = grip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // 원본에 처음부터 메시가 없는 장착물 표시용 자리만 정리한다. 끊어진 메시 참조는 숨기지 않는다.
    public static void RemoveEmptyMeshHolders(GameObject source, GameObject instance)
    {
        string text = File.ReadAllText(AssetDatabase.GetAssetPath(source));
        foreach (var original in source.GetComponentsInChildren<MeshFilter>(true))
        {
            if (original.sharedMesh != null) continue;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string guid, out long id);
            var document = System.Text.RegularExpressions.Regex.Match(text, @"--- !u!33 &" + id + @"\r?\n[\s\S]*?(?=--- !u!|$)").Value;
            if (!System.Text.RegularExpressions.Regex.IsMatch(document, @"m_Mesh: \{fileID: 0\}"))
                throw new InvalidOperationException("원본 메시 참조가 끊겼다: " + original.name);
            string path = AnimationUtility.CalculateTransformPath(original.transform, source.transform);
            var holder = string.IsNullOrEmpty(path) ? instance.transform : instance.transform.Find(path);
            var renderer = holder.GetComponent<MeshRenderer>();
            if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(holder.GetComponent<MeshFilter>());
        }
    }

    private static Material ConvertMaterial(Material source)
    {
        if (source == null) throw new InvalidOperationException("원본 머티리얼이 없다.");
        if (Materials.TryGetValue(source, out var cached)) return cached;
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        string path = Folder + "/Materials/" + source.name + "_" + guid.Substring(0, 8) + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var saved = new SerializedObject(source).FindProperty("m_SavedProperties.m_TexEnvs");
            for (int i = 0; saved != null && i < saved.arraySize; i++)
            {
                var pair = saved.GetArrayElementAtIndex(i);
                string property = pair.FindPropertyRelative("first").stringValue;
                var texture = pair.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                if (property == "_MainTex" || property == "_BaseMap") material.SetTexture("_BaseMap", texture);
                if (property == "_NormalMap" || property == "_BumpMap")
                { material.SetTexture("_BumpMap", texture); if (texture != null) material.EnableKeyword("_NORMALMAP"); }
            }
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, path);
        }
        Materials[source] = material;
        return material;
    }
}
