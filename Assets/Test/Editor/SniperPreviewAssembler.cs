using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 추출 DLL을 실행하지 않고 확인한 외형 배치 규칙만 테스트 복사본에 적용한다.
public static class SniperPreviewAssembler
{
    private const string Folder = "Assets/Test";
    private const string Source = "Assets/Third_Party_Assets/DuckovImported/GameObject/0_CharacterModel_Custom_Enemy _Boss_Sniper.prefab";
    private const string Export = "C:/Users/admin/Desktop/AssetRipper_export_20261007_063805/ExportedProject/Assets";
    private static readonly Dictionary<UnityEngine.Object, UnityEngine.Object> Copies = new Dictionary<UnityEngine.Object, UnityEngine.Object>();

    [MenuItem("Tools/Duck Animation Test/Assemble Sniper")]
    public static void Assemble()
    {
        var old = GameObject.Find("Duck Animation Preview");
        if (old == null || old.scene.path != Folder + "/DuckAnimationTest.unity") throw new InvalidOperationException("Open the generated test scene first.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SniperPreview.prefab") != null) throw new InvalidOperationException("Existing sniper preview is not overwritten.");
        DuckAnimationTestBuilder.StopPreview();
        var previous = old.GetComponent<DuckAnimationPreview>();
        var clips = previous.clips;
        var labels = previous.labels;
        var controller = previous.animator.runtimeAnimatorController;
        var avatar = previous.animator.avatar;
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source), old.scene);
        root.name = "Sniper Animation Preview";
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        try
        {
            var map = new Yaml(File.ReadAllText(Source));
            string face = map.Blocks.Values.First(b => b.Contains("  foreheadDefaultHeight:"));
            var preset = File.ReadAllText(Export + "/MonoBehaviour/CustomFacePreset_Boss_Sniper.asset");
            Strip(root);
            var animator = root.GetComponentInChildren<Animator>(true);
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.sharedMesh = Copy(r.sharedMesh);
                r.updateWhenOffscreen = true;
                // 원작의 뒤쪽 표시용 추가 패스는 프리뷰에서는 중복 렌더링하지 않는다.
                r.sharedMaterials = new[] { Material("SniperBody", ColorOf(Section(preset, "headSetting"), "mainColor")) };
            }
            var driver = root.AddComponent<DuckAnimationPreview>();
            driver.animator = animator;
            driver.clips = clips;
            driver.labels = labels;
            var shape = root.AddComponent<SniperHeadPreview>();
            shape.head = map.Resolve(root.transform, Reference(face, "headJoint"));
            shape.forehead = map.Resolve(root.transform, Reference(face, "foreheadJoint"));
            shape.hairSocket = map.Resolve(root.transform, Reference(face, "hairSocket"));
            shape.headScaleOffset = Number(Section(preset, "headSetting"), "headScaleOffset");
            shape.foreheadHeight = Number(face, "foreheadDefaultHeight") + Number(Section(preset, "headSetting"), "foreheadHeight");
            shape.foreheadRound = Number(Section(preset, "headSetting"), "foreheadRound");
            shape.Apply();
            // ID와 실제 파츠 이름은 새 추출본의 CustomFaceData GUID 대응을 확인한 결과다.
            AddPart(root, map, face, preset, "hair", "hairPart", "HeadPart 04", false);
            AddPart(root, map, face, preset, "eye", "eyePart", "TestEyePart 1_a", false);
            AddPart(root, map, face, preset, "eyebrow", "eyebrowPart", "TestEyebrowPart 0", false);
            AddPart(root, map, face, preset, "mouth", "mouthPart", "Mouth_12", false);
            AddPart(root, map, face, preset, "tail", "tailPart", "Tail_0", false);
            AddPart(root, map, face, preset, "foot", "footLPart", "0_FootDefault_L", false);
            AddPart(root, map, face, preset, "foot", "footRPart", "0_FootDefault_L", true);
            AddPart(root, map, face, preset, "wing", "wingLPart", "0_Wing_default", false);
            AddPart(root, map, face, preset, "wing", "wingRPart", "0_Wing_default", true);
            clips[0].SampleAnimation(animator.gameObject, 0);
            shape.Apply();
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var camera = GameObject.Find("Test Camera").GetComponent<Camera>();
            float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
            camera.transform.position = bounds.center + new Vector3(radius * 0.8f, radius * 0.4f, radius * 2.8f);
            camera.transform.LookAt(bounds.center);
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/SniperPreview.prefab");
            // 준비가 끝난 뒤 기존 테스트 복사본만 제거한다. 실제 게임 오브젝트는 대상이 아니다.
            UnityEngine.Object.DestroyImmediate(old);
            EditorSceneManager.SaveScene(root.scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.Frame(bounds, false);
            Debug.Log("SniperPreview assembled: 9 part instances, renderers=" + renderers.Length + "; controller=" + controller.name + "; no original game DLL loaded.");
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw;
        }
    }

    private static void AddPart(GameObject root, Yaml model, string face, string preset, string category, string utility, string name, bool mirror)
    {
        string path = "Assets/Third_Party_Assets/DuckovImported/GameObject/" + name + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing part " + path);
        var socket = model.Resolve(root.transform, Reference(Section(face, utility), "socket"));
        if (socket == null) throw new InvalidOperationException("Missing socket " + utility);
        var part = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
        PrefabUtility.UnpackPrefabInstance(part, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        part.name = "Preview_" + utility;
        part.transform.SetParent(socket, false);
        part.transform.localPosition = Vector3.zero;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = new Vector3(mirror ? -1 : 1, 1, 1);
        var yaml = new Yaml(File.ReadAllText(path));
        string block = yaml.Blocks.Values.First(b => b.Contains("  centerObject:"));
        string info = Section(preset, category + "Info");
        float height = Number(info, "height") + Number(info, "heightOffset");
        if (category == "eyebrow") height = Number(info, "height") + Number(Section(preset, "eyeInfo"), "height");
        Place(yaml.Resolve(part.transform, Reference(block, "centerObject")), info, height, 0, 0);
        Place(yaml.Resolve(part.transform, Reference(block, "leftObject")), info, height, -1, 1);
        Place(yaml.Resolve(part.transform, Reference(block, "rightObject")), info, height, 1, -1);
        Strip(part);
        Color color = ColorOf(info, "color");
        color.a = 1;
        foreach (var r in part.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = Enumerable.Repeat(Material("Sniper_" + category, color), Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
        foreach (var mesh in part.GetComponentsInChildren<MeshFilter>(true)) mesh.sharedMesh = Copy(mesh.sharedMesh);
        // 입은 원작에서 데칼로 그리므로 전용 데칼 머티리얼의 텍스처를 작은 표면으로 표시한다.
        if (category == "mouth") AddMouthSurface(part, yaml);
        Debug.Log("SniperPart " + utility + " <- " + name + "; socket=" + socket.name + "; mirror=" + mirror);
    }

    private static void Place(Transform target, string info, float height, int side, int twistSign)
    {
        if (target == null) return;
        float angle = -Number(info, "leftRightAngle") + side * Number(info, "distanceAngle");
        var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
        target.localPosition = direction * Number(info, "radius") + Vector3.up * height;
        target.localRotation = Quaternion.LookRotation(direction, Vector3.up);
        target.localRotation = side == 0
            ? Quaternion.Euler(0, 0, Number(info, "twist")) * target.localRotation
            : Quaternion.AngleAxis(twistSign * Number(info, "twist"), direction) * target.localRotation;
        target.localScale = Vector3.one * Number(info, "scale");
    }

    private static void AddMouthSurface(GameObject part, Yaml yaml)
    {
        string decal = yaml.Blocks.Values.FirstOrDefault(b => b.Contains("  m_Material:"));
        if (decal == null) return;
        string guid = Regex.Match(decal, @"m_Material:.*guid: (\w+)").Groups[1].Value;
        var source = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
        if (source == null) return;
        var texture = source.GetTexturePropertyNames().Select(n => source.GetTexture(n)).FirstOrDefault(t => t is Texture2D);
        if (texture == null) { Debug.LogWarning("Sniper mouth decal has no available texture."); return; }
        var center = part.transform.Find("Center");
        var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surface.name = "Mouth decal preview surface";
        UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
        surface.transform.SetParent(center, false);
        surface.transform.localPosition = new Vector3(0, 0, 0.04f);
        surface.transform.localScale = new Vector3(0.2f, 0.12f, 1);
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.name = "SniperMouthTexture";
        material.SetTexture("_BaseMap", Copy(texture));
        // 입 모양은 3x3 아틀라스에서 데칼의 UV 범위로 선택한다.
        material.SetTextureScale("_BaseMap", Vector2Of(decal, "m_UVScale"));
        material.SetTextureOffset("_BaseMap", Vector2Of(decal, "m_UVBias"));
        material.SetFloat("_Surface", 1);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0);
        material.SetFloat("_Cull", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(Folder + "/SniperMouthTexture.mat"));
        surface.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void Strip(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var b in t.GetComponents<MonoBehaviour>()) if (b != null) UnityEngine.Object.DestroyImmediate(b);
        }
    }

    private static Material Material(string name, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
        material.SetFloat("_Cull", 0);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static T Copy<T>(T original) where T : UnityEngine.Object
    {
        if (original == null) throw new InvalidOperationException("Missing visual asset.");
        if (Copies.TryGetValue(original, out var cached)) return (T)cached;
        if (original is Texture)
        {
            // 읽기 불가 텍스처는 Instantiate 대신 원본 파일을 복사한다.
            string source = AssetDatabase.GetAssetPath(original);
            string target = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Sniper_" + Path.GetFileName(source));
            if (!AssetDatabase.CopyAsset(source, target)) throw new InvalidOperationException("Texture copy failed: " + source);
            var texture = AssetDatabase.LoadAssetAtPath<T>(target);
            Copies.Add(original, texture);
            return texture;
        }
        var clone = UnityEngine.Object.Instantiate(original);
        clone.name = original.name;
        AssetDatabase.CreateAsset(clone, AssetDatabase.GenerateUniqueAssetPath(Folder + "/Sniper_" + original.name.Replace('/', '_') + ".asset"));
        Copies.Add(original, clone);
        return clone;
    }

    [MenuItem("Tools/Duck Animation Test/Repair Mouth Texture")]
    public static void RepairMouth()
    {
        var root = GameObject.Find("Sniper Animation Preview");
        var surface = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Mouth decal preview surface");
        var source = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("d952dce07208e7d4c96995acc33fc4b4"));
        string property = source.GetTexturePropertyNames().First(n => source.GetTexture(n) is Texture2D);
        var material = surface.GetComponent<Renderer>().sharedMaterial;
        material.SetTexture("_BaseMap", Copy(source.GetTexture(property)));
        string decal = new Yaml(File.ReadAllText("Assets/Third_Party_Assets/DuckovImported/GameObject/Mouth_12.prefab")).Blocks.Values.First(b => b.Contains("  m_UVScale:"));
        material.SetTextureScale("_BaseMap", Vector2Of(decal, "m_UVScale"));
        material.SetTextureOffset("_BaseMap", Vector2Of(decal, "m_UVBias"));
        surface.localPosition = new Vector3(0, 0, 0.04f);
        Debug.Log("SniperMouth source=" + AssetDatabase.GetAssetPath(source) + "; texture=" + property + "; scale=" + source.GetTextureScale(property) + "; offset=" + source.GetTextureOffset(property));
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/SniperPreview.prefab");
        EditorSceneManager.SaveScene(root.scene);
        AssetDatabase.SaveAssets();
    }

    private static string Section(string text, string key) => Regex.Match(text, @"(?m)^\s*" + key + @":\r?\n(?<body>(?:[ \t]+[^\r\n]*\r?\n)*)").Groups["body"].Value;
    private static Vector2 Vector2Of(string text, string key)
    {
        var m = Regex.Match(text, key + @": \{x: ([^,]+), y: ([^}]+)\}");
        return new Vector2(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
    }
    private static long Reference(string text, string key) => long.Parse(Regex.Match(text, @"(?m)^\s*" + key + @": \{fileID: (-?\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
    private static float Number(string text, string key) => float.Parse(Regex.Match(text, @"(?m)^\s*" + key + @": ([-+\d.Ee]+)").Groups[1].Value, CultureInfo.InvariantCulture);
    private static Color ColorOf(string text, string key)
    {
        var m = Regex.Match(text, key + @": \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)");
        return new Color(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), 1);
    }

    // YAML의 fileID를 프리팹 계층 경로로 바꿔 Missing Script 없이도 소켓을 찾는다.
    private sealed class Yaml
    {
        public readonly Dictionary<long, string> Blocks = new Dictionary<long, string>();
        private readonly Dictionary<long, string> names = new Dictionary<long, string>();
        public Yaml(string text)
        {
            foreach (Match m in Regex.Matches(text, @"(?ms)^--- !u!(\d+) &(-?\d+)\r?\n(.*?)(?=^--- !u!|\z)"))
            {
                long id = long.Parse(m.Groups[2].Value);
                string body = m.Groups[3].Value;
                Blocks[id] = body;
                if (m.Groups[1].Value == "1") names[id] = Regex.Match(body, @"(?m)^  m_Name: (.*)").Groups[1].Value.Trim();
            }
        }
        public Transform Resolve(Transform root, long id)
        {
            if (id == 0) return null;
            var parts = new List<string>();
            var seen = new HashSet<long>();
            while (id != 0)
            {
                if (!seen.Add(id)) throw new InvalidOperationException("Cycle in transform hierarchy.");
                var block = Blocks[id];
                parts.Add(names[Reference(block, "m_GameObject")]);
                id = Reference(block, "m_Father");
            }
            parts.Reverse();
            string path = string.Join("/", parts.Skip(1));
            return path.Length == 0 ? root : root.Find(path);
        }
    }
}
