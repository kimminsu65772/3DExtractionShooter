using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 원본 외형 데이터를 에디터에서 고정 계층으로 변환한다. 게임 실행 중에는 조립 스크립트가 필요하지 않다.
public static class DuckCandidatePrefabBuilder
{
    public const string RootFolder = "Assets/Characters/DuckCandidates";
    private const string Folder = RootFolder + "/VisualAssets";
    private const string Source = "Assets/Third_Party_Assets/DuckovImported/GameObject/0_CharacterModel_Custom_Template.prefab";
    private const string Export = "C:/Users/admin/Desktop/AssetRipper_export_20261007_063805/ExportedProject/Assets";
    private static string Candidate;
    private static readonly Dictionary<UnityEngine.Object, UnityEngine.Object> Copies = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
    private static readonly string[] Candidates = { "Default", "YellowDuck", "Solider", "Usec", "PMCLeader", "ScavBlue", "ScavRed", "Killa" };

    [MenuItem("Tools/Duck Candidates/Build Fixed Prefabs")]
    public static void BuildFromMenu() => Build();

    public static void Build(bool replaceGenerated = false)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료한다.");
        foreach (string candidate in Candidates)
            if (!replaceGenerated && File.Exists(RootFolder + "/Prefabs/Duck_" + candidate + ".prefab"))
                throw new InvalidOperationException("기존 후보 프리팹을 덮어쓰지 않는다: " + candidate);
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory(RootFolder + "/Prefabs");
        AssetDatabase.Refresh();
        Copies.Clear();
        string data = File.ReadAllText(Export + "/MonoBehaviour/CustomFaceData.asset");
        // 추출본마다 GUID가 달라질 수 있으므로 원본 GUID를 같은 이름의 프로젝트 파일에 연결한다.
        var originalPaths = new Dictionary<string, string>();
        foreach (string meta in Directory.EnumerateFiles(Export + "/GameObject", "*.prefab.meta"))
        {
            string guid = Regex.Match(File.ReadAllText(meta), @"(?m)^guid: (\w+)").Groups[1].Value;
            originalPaths[guid] = "Assets/Third_Party_Assets/DuckovImported/GameObject/" + Path.GetFileNameWithoutExtension(meta);
        }
        var model = new Yaml(File.ReadAllText(Source));
        string face = model.Blocks.Values.First(b => b.Contains("  foreheadDefaultHeight:"));
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/DuckPlayerCopy/DuckPlayerRoot.prefab");
        var avatar = player.GetComponentInChildren<Animator>(true).avatar;
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Characters/DuckEnemyCopy/DuckEnemy.overrideController");
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/DuckPlayerCopy/Clips/Idle.anim");
        if (avatar == null || controller == null || idle == null) throw new InvalidOperationException("검증한 애니메이션 자산이 없다.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            for (int i = 0; i < Candidates.Length; i++)
            {
                Candidate = Candidates[i];
                string preset = File.ReadAllText(Export + "/MonoBehaviour/CustomFacePreset_" + Candidate + ".asset");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source), scene);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "Duck_" + Candidate;
                Strip(root);
                var animator = root.GetComponentInChildren<Animator>(true);
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    renderer.sharedMesh = Copy(renderer.sharedMesh);
                    renderer.updateWhenOffscreen = true;
                    renderer.sharedMaterials = new[] { Material(Candidate + "_Body", ColorOf(Section(preset, "headSetting"), "mainColor")) };
                }
                string[] categories = { "hair", "eye", "eyebrow", "mouth", "tail", "foot", "foot", "wing", "wing" };
                string[] utilities = { "hairPart", "eyePart", "eyebrowPart", "mouthPart", "tailPart", "footLPart", "footRPart", "wingLPart", "wingRPart" };
                for (int p = 0; p < categories.Length; p++)
                {
                    string category = categories[p];
                    string plural = category == "foot" ? "foots" : category == "eyebrow" ? "eyebrows" : category + "s";
                    string list = Regex.Match(data, @"(?ms)^  " + plural + @":\r?\n(.*?)(?=^  \w+:|\z)").Groups[1].Value;
                    int id = (int)Number(preset, category + "ID");
                    // 0번 눈과 입은 빈 파츠이므로 비교용 완성 외형에는 기본 파츠를 보충한다.
                    if (id == 0 && (category == "eye" || category == "mouth")) id = 1;
                    string guid = Regex.Match(list, @"- id: " + id + @"\r?\n\s+part:.*guid: (\w+)").Groups[1].Value;
                    string path;
                    originalPaths.TryGetValue(guid, out path);
                    if (string.IsNullOrEmpty(path)) throw new InvalidOperationException(Candidate + ": 누락된 파츠 " + category + "/" + id);
                    AddPart(root, model, face, preset, category, utilities[p], path, p == 6 || p == 8);
                }
                // 포즈를 먼저 적용하고 얼굴 형태를 마지막에 반영해 원본 머리 설정을 보존한다.
                idle.SampleAnimation(animator.gameObject, 0);
                string setting = Section(preset, "headSetting");
                var head = model.Resolve(root.transform, Reference(face, "headJoint"));
                var forehead = model.Resolve(root.transform, Reference(face, "foreheadJoint"));
                head.localScale = Vector3.one * (1 + Number(setting, "headScaleOffset"));
                forehead.localPosition = Vector3.up * (Number(face, "foreheadDefaultHeight") + Number(setting, "foreheadHeight"));
                forehead.localScale = new Vector3(1, Number(setting, "foreheadRound"), 1);
                model.Resolve(root.transform, Reference(face, "hairSocket")).localPosition = forehead.localPosition;
                // 원본 런타임 기능은 복사하지 않고 시각 자산만 저장한다.
                foreach (var collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                PrefabUtility.SaveAsPrefabAsset(root, RootFolder + "/Prefabs/" + root.name + ".prefab");
                root.transform.position = new Vector3((i % 4 - 1.5f) * 2, 0, (i / 4) * 3);
                var label = new GameObject(Candidate);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(label, scene);
                label.transform.position = root.transform.position + new Vector3(0, 0.05f, 0.8f);
                label.transform.rotation = Quaternion.Euler(45, 180, 0);
                var text = label.AddComponent<TextMesh>();
                text.text = Candidate;
                text.fontSize = 48;
                text.characterSize = 0.025f;
                text.anchor = TextAnchor.MiddleCenter;
            }
            var cameraObject = new GameObject("Candidate Camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.transform.position = new Vector3(0, 6, 11);
            cameraObject.transform.LookAt(new Vector3(0, 0.7f, 1.5f));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            var lightObject = new GameObject("Candidate Light", typeof(Light));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = Quaternion.Euler(45, -35, 0);
            lightObject.GetComponent<Light>().type = LightType.Directional;
            lightObject.GetComponent<Light>().intensity = 2;
            EditorSceneManager.SaveScene(scene, RootFolder + "/DuckCandidateGallery.unity");
            AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        Debug.Log("Duck candidates: 8 fixed visual prefabs created.");
    }

    private static void AddPart(GameObject root, Yaml model, string face, string preset, string category, string utility, string name, bool mirror)
    {
        string path = name;
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
            r.sharedMaterials = Enumerable.Repeat(Material(Candidate + "_" + category, color), Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
        // 머리카락 없음 같은 선택지는 메시 없는 자리 표시자를 포함한다.
        foreach (var mesh in part.GetComponentsInChildren<MeshFilter>(true))
            if (mesh.sharedMesh != null) mesh.sharedMesh = Copy(mesh.sharedMesh);
        // 입은 원작에서 데칼로 그리므로 전용 데칼 머티리얼의 텍스처를 작은 표면으로 표시한다.
        if (category == "mouth") AddMouthSurface(part, yaml);
        Debug.Log("DuckCandidatePart " + utility + " <- " + name + "; socket=" + socket.name + "; mirror=" + mirror);
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
        material.name = Candidate + "_MouthTexture";
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
        AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + Candidate + "_MouthTexture.mat"));
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
