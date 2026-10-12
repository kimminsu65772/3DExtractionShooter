using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// CSV의 기본 보호 장비를 원작 로직 없이 사용할 외형 프리팹으로 조립한다.
public static class ProtectionVisualPrefabBuilder
{
    public const string Folder = "Assets/Art/Equipment/DuckProtection";
    private const string Imported = "Assets/Third_Party_Assets/DuckovImported/GameObject/";
    private static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();

    [MenuItem("Tools/Duck Equipment/Build Level 1-3 Protection")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료한다.");
        if (Directory.Exists(Folder + "/Prefabs")) throw new InvalidOperationException("기존 조립 결과를 덮어쓰지 않는다.");
        // CSV에서 등급과 ID를 읽으며 수치나 주소 필드는 변경하지 않는다.
        var armor = ReadRows("Assets/GameData/Armor/armor.csv");
        var helmet = ReadRows("Assets/GameData/Helmet/helmet.csv");
        foreach (var rows in new[] { armor, helmet })
            for (int level = 1; level <= 3; level++)
                if (rows.Count(r => r["armorLevel"] == level.ToString()) != 1)
                    throw new InvalidOperationException("등급별 기본 장비가 하나여야 한다.");
        Directory.CreateDirectory(Folder + "/Prefabs");
        Directory.CreateDirectory(Folder + "/Materials");
        AssetDatabase.Refresh();
        Materials.Clear();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            for (int level = 1; level <= 3; level++)
            {
                var bodyRow = armor.Single(r => r["armorLevel"] == level.ToString());
                var headRow = helmet.Single(r => r["armorLevel"] == level.ToString());
                var body = Assemble("Armor", level, bodyRow);
                var head = Assemble("Helmet", level, headRow);
                var duck = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Characters/DuckCandidates/Prefabs/Duck_Default.prefab"), scene);
                duck.name = "Protection_Level_" + level;
                Attach(duck, body, "ArmorSocket");
                Attach(duck, head, "HelmatSocket");
                duck.transform.position = new Vector3((level - 2) * 1.8f, 0, 0);
                var label = new GameObject("Level " + level, typeof(TextMesh));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(label, scene);
                label.transform.position = duck.transform.position + new Vector3(0, 0.1f, 0.8f);
                label.transform.rotation = Quaternion.Euler(20, 180, 0);
                var text = label.GetComponent<TextMesh>();
                text.text = "Lv." + level + "  " + bodyRow["itemId"] + " / " + headRow["itemId"];
                text.fontSize = 40;
                text.characterSize = 0.025f;
                text.anchor = TextAnchor.MiddleCenter;
            }
            var cameraObject = new GameObject("Protection Camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.transform.position = new Vector3(0, 2.6f, 7);
            cameraObject.transform.LookAt(new Vector3(0, 0.7f, 0));
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            var lightObject = new GameObject("Protection Light", typeof(Light));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2;
            light.shadows = LightShadows.None;
            lightObject.transform.rotation = Quaternion.Euler(35, -25, 0);
            EditorSceneManager.SaveScene(scene, Folder + "/ProtectionPreview.unity");
            AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    private static List<Dictionary<string, string>> ReadRows(string path)
    {
        // 현재 보호 장비 CSV는 인용 쉼표가 없는 고정 스키마다. 복잡한 CSV는 기존 파이프라인을 사용한다.
        string[] lines = File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        string[] headers = lines[0].TrimStart('\uFEFF').Split(',');
        return lines.Skip(1).Select(line =>
        {
            string[] values = line.Split(',');
            if (values.Length != headers.Length) throw new InvalidOperationException("CSV 열 개수가 다르다: " + path);
            return headers.Select((h, i) => new { h, v = values[i] }).ToDictionary(p => p.h, p => p.v);
        }).ToList();
    }

    private static GameObject Assemble(string type, int level, Dictionary<string, string> row)
    {
        string source = Imported + "IG_" + (type == "Armor" ? "Armor" : "Helmat") + "_LV" + level + ".prefab";
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if (original == null) throw new InvalidOperationException("외형 에셋이 없다: " + source);
        var root = UnityEngine.Object.Instantiate(original);
        try
        {
            root.name = type + "_Lv" + level + "_" + row["itemId"];
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach (var script in t.GetComponents<MonoBehaviour>()) UnityEngine.Object.DestroyImmediate(script);
                t.gameObject.layer = 0;
            }
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var rigidbody in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(rigidbody);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(ConvertMaterial).ToArray();
            foreach (var mesh in root.GetComponentsInChildren<MeshFilter>(true))
                if (mesh.sharedMesh == null) throw new InvalidOperationException("메시가 없다: " + source);
            return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Prefabs/" + root.name + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static Material ConvertMaterial(Material source)
    {
        if (source == null) throw new InvalidOperationException("머티리얼이 없다.");
        if (Materials.TryGetValue(source, out var cached)) return cached;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = source.name + "_URP";
        if (source.HasProperty("_MainTex"))
        {
            material.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            material.SetTextureScale("_BaseMap", source.GetTextureScale("_MainTex"));
            material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_MainTex"));
        }
        material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
        // 원본 패킹이 불명확한 금속 맵은 임의로 연결하지 않고 알베도와 노멀만 옮긴다.
        if (source.HasProperty("_NormalMap") && source.GetTexture("_NormalMap") != null)
        {
            material.SetTexture("_BumpMap", source.GetTexture("_NormalMap"));
            material.EnableKeyword("_NORMALMAP");
        }
        material.SetFloat("_Cull", 0);
        AssetDatabase.CreateAsset(material, Folder + "/Materials/" + material.name + ".mat");
        Materials.Add(source, material);
        return material;
    }

    private static void Attach(GameObject duck, GameObject equipment, string socketName)
    {
        var socket = duck.GetComponentsInChildren<Transform>(true).Single(t => t.name == socketName);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(equipment, duck.scene);
        // 외형 내부 오프셋을 보존하고 루트만 해당 장비 소켓에 일치시킨다.
        instance.transform.SetParent(socket, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
    }
}

