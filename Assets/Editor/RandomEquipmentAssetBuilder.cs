using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

// 원본 게임 로직 없이 장비 외형과 아이콘만 가져오는 에디터 제작 도구다.
public static class RandomEquipmentAssetBuilder
{
    private const string Manifest = ".codex/data/random-equipment-source.json";
    private const string ImportRoot = "Assets/Art/Equipment/DuckovRandom/Source/";
    private static readonly Dictionary<string, string> OriginalPaths = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> CopiedPaths = new Dictionary<string, string>();
    private static readonly HashSet<string> Visited = new HashSet<string>();
    private static readonly Regex GuidReference = new Regex(@"guid: ([a-f0-9]{32})");
    private static string sourceRoot;

    public static string ImportSources()
    {
        var manifest = JObject.Parse(File.ReadAllText(Manifest));
        sourceRoot = (string)manifest["sourceRoot"];
        OriginalPaths.Clear(); CopiedPaths.Clear(); Visited.Clear();
        // 탐색 범위를 외형 의존성으로 제한한다. 원본 DLL/스크립트/씬은 가져오지 않는다.
        foreach (string folder in new[] { "GameObject", "Mesh", "Material", "Texture2D", "Sprite", "Cubemap" })
        {
            string root = Path.Combine(sourceRoot, folder);
            if (!Directory.Exists(root)) continue;
            foreach (string meta in Directory.EnumerateFiles(root, "*.meta", SearchOption.AllDirectories))
            {
                string guid = File.ReadLines(meta).FirstOrDefault(l => l.StartsWith("guid: "));
                if (guid != null) OriginalPaths[guid.Substring(6)] = meta.Substring(0, meta.Length - 5);
            }
        }
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (JObject item in manifest["items"])
            {
                string graphic = (string)item["graphicGuid"];
                // 별도 graphic 참조가 없는 LV1은 기존 조립 외형을 재사용한다.
                if (!string.IsNullOrEmpty(graphic)) ImportDependency(graphic);
                ImportDependency((string)item["iconGuid"]);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
        return "Copied visual dependencies: " + CopiedPaths.Count;
    }

    private static void ImportDependency(string guid)
    {
        if (string.IsNullOrEmpty(guid) || guid.StartsWith("0000000000000000") || !Visited.Add(guid)) return;
        if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) return;
        string source;
        // 범위 밖의 스크립트/셰이더 참조는 복원하지 않고 최종 외형에서 제거·교체한다.
        if (!OriginalPaths.TryGetValue(guid, out source) || !File.Exists(source)) return;
        string relative = source.Substring(sourceRoot.Length + 1).Replace('\\', '/');
        string destination = ImportRoot + relative;
        if (File.Exists(destination)) throw new InvalidOperationException("기존 파일을 덮어쓰지 않는다: " + destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(source, destination);
        File.Copy(source + ".meta", destination + ".meta");
        CopiedPaths.Add(guid, destination);
        // 바이너리 메시와 이미지는 문자열로 읽지 않는다.
        using (var reader = File.OpenText(source))
        {
            string first = reader.ReadLine();
            if (first != null && first.StartsWith("%YAML"))
                foreach (Match match in GuidReference.Matches(reader.ReadToEnd())) ImportDependency(match.Groups[1].Value);
        }
        foreach (Match match in GuidReference.Matches(File.ReadAllText(source + ".meta"))) ImportDependency(match.Groups[1].Value);
    }

    public static string BuildAssets()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료한다.");
        Directory.CreateDirectory(WeaponShowcaseBuilder.Folder + "/Prefabs");
        Directory.CreateDirectory(WeaponShowcaseBuilder.Folder + "/Materials");
        Directory.CreateDirectory(ProtectionVisualPrefabBuilder.Folder + "/Prefabs");
        Directory.CreateDirectory(ProtectionVisualPrefabBuilder.Folder + "/RandomMaterials");
        AssetDatabase.Refresh();
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("Addressables 설정이 없다.");
        var result = new List<object>();
        var items = (JArray)JObject.Parse(File.ReadAllText(Manifest))["items"];
        foreach (JObject item in items)
        {
            string path = (string)item["outputPath"];
            string kind = (string)item["kind"];
            if (!File.Exists(path))
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath((string)item["graphicGuid"]));
                if (source == null) throw new InvalidOperationException("원본 외형이 없다: " + item["name"]);
                if (kind == "Weapon") WeaponShowcaseBuilder.Assemble(source, path, (string)item["assetName"]);
                else BuildProtection(source, path, (string)item["assetName"]);
            }
            Register(settings, path, (string)item["viewAddress"]);
            string iconPath = AssetDatabase.GUIDToAssetPath((string)item["iconGuid"]);
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (icon == null || icon.texture == null) throw new InvalidOperationException("아이콘 의존성이 없다: " + item["name"]);
            // 기존 총 아이콘의 주소와 에셋 연결은 그대로 둔다.
            int id = (int)item["projectId"];
            if (kind != "Weapon" || id > 1004) Register(settings, iconPath, (string)item["iconAddress"]);
            result.Add(new { id, path, icon = iconPath });
        }
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true);
        foreach (var group in settings.groups) if (group != null) AssetDatabase.SaveAssetIfDirty(group);
        AssetDatabase.SaveAssetIfDirty(settings);
        return Newtonsoft.Json.JsonConvert.SerializeObject(result);
    }

    private static void Register(AddressableAssetSettings settings, string path, string address)
    {
        string guid = AssetDatabase.AssetPathToGUID(path);
        var conflict = settings.groups.Where(g => g != null).SelectMany(g => g.entries)
            .FirstOrDefault(e => e.address == address && e.guid != guid);
        if (conflict != null) throw new InvalidOperationException("주소 충돌: " + address);
        var existing = settings.FindAssetEntry(guid);
        if (existing != null && existing.address != address) throw new InvalidOperationException("기존 주소를 변경하지 않는다: " + existing.address);
        var entry = existing ?? settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
        entry.address = address;
    }

    // 이번에 복사한 원본 외형에만 남아 있는 끊어진 스크립트를 정리한다.
    public static string CleanImportedSources()
    {
        int removed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ImportRoot.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = 0;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                if (count == 0) continue;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                removed += count;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return "Removed missing scripts from imported visual sources: " + removed;
    }

    public static string ValidateAssets()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var entries = settings.groups.Where(g => g != null).SelectMany(g => g.entries).ToArray();
        var issues = new List<string>();
        var items = (JArray)JObject.Parse(File.ReadAllText(Manifest))["items"];
        foreach (JObject item in items)
        {
            string path = (string)item["outputPath"];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { issues.Add("Prefab: " + path); continue; }
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0) issues.Add("Missing script: " + path);
            foreach (var mesh in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mesh.sharedMesh == null) issues.Add("Mesh: " + path);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                if (renderer.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader")) issues.Add("Material: " + path);
            if ((string)item["kind"] == "Weapon")
            {
                var view = prefab.GetComponent<WeaponView>();
                if (view == null || !view.HasMuzzle || Vector3.Dot(view.MuzzleTransform.forward, Vector3.forward) < 0.9999f) issues.Add("Muzzle: " + path);
            }
            string viewAddress = (string)item["viewAddress"];
            if (!entries.Any(e => e.address == viewAddress && e.guid == AssetDatabase.AssetPathToGUID(path))) issues.Add("View address: " + viewAddress);
            string iconAddress = (string)item["iconAddress"];
            var iconEntry = entries.SingleOrDefault(e => e.address == iconAddress);
            if (iconEntry == null || AssetDatabase.LoadAssetAtPath<Sprite>(iconEntry.AssetPath) == null) issues.Add("Icon address: " + iconAddress);
        }
        return Newtonsoft.Json.JsonConvert.SerializeObject(new { checkedItems = items.Count, issues });
    }

    private static void BuildProtection(GameObject source, string path, string name)
    {
        var root = UnityEngine.Object.Instantiate(source);
        try
        {
            WeaponShowcaseBuilder.RemoveEmptyMeshHolders(source, root);
            root.name = name;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach (var script in t.GetComponents<MonoBehaviour>()) UnityEngine.Object.DestroyImmediate(script);
                t.gameObject.layer = 0;
            }
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.DestroyImmediate(particle.gameObject);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(ConvertMaterial).ToArray();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh == null) throw new InvalidOperationException("메시가 없다: " + name);
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh == null) throw new InvalidOperationException("스킨 메시가 없다: " + name);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static Material ConvertMaterial(Material source)
    {
        if (source == null) throw new InvalidOperationException("원본 머티리얼이 없다.");
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        string path = ProtectionVisualPrefabBuilder.Folder + "/RandomMaterials/" + source.name + "_" + guid.Substring(0, 8) + ".mat";
        var converted = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (converted != null) return converted;
        converted = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var saved = new SerializedObject(source).FindProperty("m_SavedProperties.m_TexEnvs");
        for (int i = 0; saved != null && i < saved.arraySize; i++)
        {
            var pair = saved.GetArrayElementAtIndex(i);
            string property = pair.FindPropertyRelative("first").stringValue;
            var texture = pair.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            if (property == "_MainTex" || property == "_BaseMap")
            {
                converted.SetTexture("_BaseMap", texture);
                converted.SetTextureScale("_BaseMap", pair.FindPropertyRelative("second.m_Scale").vector2Value);
                converted.SetTextureOffset("_BaseMap", pair.FindPropertyRelative("second.m_Offset").vector2Value);
            }
            if ((property == "_NormalMap" || property == "_BumpMap") && texture != null)
            { converted.SetTexture("_BumpMap", texture); converted.EnableKeyword("_NORMALMAP"); }
        }
        converted.SetColor("_BaseColor", Color.white);
        converted.SetFloat("_Cull", 0);
        AssetDatabase.CreateAsset(converted, path);
        return converted;
    }
}
