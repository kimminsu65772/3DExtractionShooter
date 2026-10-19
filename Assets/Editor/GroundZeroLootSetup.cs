using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 원본 분석 결과를 런타임용 SO와 씬 연결로 변환한다. 원본 DLL은 참조하지 않는다.
public static class GroundZeroLootSetup
{
    private const string Manifest = ".codex/data/groundzero-loot-source.json";
    private const string Folder = "Assets/GameData/LootTables/GroundZero";

    private static float GetEmptyRollChance(JObject profile)
    {
        var entries = (JArray)profile["entries"];
        // 이번 정책 변경은 미지원 후보 제외 후 냄비만 남은 표에 한정한다.
        if (entries.Count != 1 || (int)entries[0]["itemId"] != 5006) return 0f;
        double retainedMass = (double)profile["retainedProbabilityMass"];
        if (double.IsNaN(retainedMass) || retainedMass < 0d || retainedMass > 1d)
            throw new InvalidOperationException("원본 지원 후보의 확률 합이 유효하지 않다: " + profile["id"]);
        return (float)(1d - retainedMass);
    }

    // 씬 구성·상자 삭제를 재실행하지 않고 기존 냄비 전용 표의 정책만 갱신한다.
    public static string ApplyEmptyRollPolicy()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 중에는 확률표를 변경하지 않는다.");
        var data = JObject.Parse(File.ReadAllText(Manifest));
        var changes = new List<object>();
        var pending = new List<(LootTable table, JObject profile, string path, float emptyChance)>();
        foreach (string guid in AssetDatabase.FindAssets("t:LootTable", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
            var profile = ((JArray)data["profiles"]).OfType<JObject>()
                .SingleOrDefault(p => table.name.EndsWith("_" + (string)p["id"], StringComparison.Ordinal));
            if (profile == null) throw new InvalidOperationException("원본 대응이 없는 확률표: " + path);
            float emptyChance = GetEmptyRollChance(profile);
            if (emptyChance == 0f) continue;
            if (table.entries.Length != 1 || table.entries[0].itemId != 5006)
                throw new InvalidOperationException("원본 기록과 달라진 냄비 후보를 먼저 확인한다: " + path);
            if (EditorUtility.IsDirty(table)) throw new InvalidOperationException("미저장 테이블을 덮어쓰지 않는다: " + path);
            pending.Add((table, profile, path, emptyChance));
        }
        // 전체 대상의 대응을 확인한 뒤에만 백업과 변경을 시작한다.
        foreach (var item in pending)
        {
            string backup = ".codex/backups/2026-10-18-loot-empty-rolls/" + item.path;
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(item.path, backup, false);
            item.table.emptyRollChance = item.emptyChance;
            const string note = "2026-10-18: 냄비만 남은 표는 제외 후보의 확률을 빈 결과로 유지한다. 빈 결과는 재추첨하지 않는다.";
            if (item.table.sourceNote == null || !item.table.sourceNote.Contains(note))
                item.table.sourceNote += "\n" + note;
            EditorUtility.SetDirty(item.table);
            AssetDatabase.SaveAssetIfDirty(item.table);
            changes.Add(new { table = item.table.name, emptyChance = item.emptyChance,
                sourcePotProbability = (double)item.profile["retainedProbabilityMass"] });
        }
        return Newtonsoft.Json.JsonConvert.SerializeObject(new { updated = changes.Count, changes });
    }

    private static bool HasSupportedLoot(JObject profile)
    {
        int maxRolls = ParseRolls(profile).y;
        return maxRolls > 0 && ((JArray)profile["entries"]).Count > 0
            || (float)profile["fixedItemSpawnChance"] > 0 && ((JArray)profile["fixedEntries"]).Count > 0;
    }

    private static Vector2Int ParseRolls(JObject profile)
    {
        var numbers = System.Text.RegularExpressions.Regex.Matches((string)profile["randomCount"], @"\d+");
        return new Vector2Int(int.Parse(numbers[0].Value), int.Parse(numbers[1].Value));
    }

    private static Dictionary<string, GameObject> GetObjects(Scene scene)
    {
        // 복원 씬은 원본 local file ID를 유지한다. 중복 이름이나 좌표에 기대지 않고 정확히 대응한다.
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name.StartsWith("Loot"))
            .ToDictionary(t => GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).targetObjectId.ToString(), t => t.gameObject);
    }

    private static GameObject RemovalRoot(GameObject go)
    {
        Transform root = go.transform;
        for (Transform parent = root.parent; parent != null; parent = parent.parent)
            if (parent.name.StartsWith("LootBox")) root = parent;
        return root.gameObject;
    }

    public static string Preview()
    {
        var scene = SceneManager.GetSceneByName("Level_GroundZero_1");
        var data = JObject.Parse(File.ReadAllText(Manifest));
        var profiles = ((JArray)data["profiles"]).OfType<JObject>().ToDictionary(p => (string)p["id"]);
        var objects = GetObjects(scene);
        var keep = new List<string>();
        var remove = new List<object>();
        var missing = new List<string>();
        var preserve = new List<string>();
        foreach (JObject box in data["boxes"])
        {
            string id = (string)box["sourceFileId"];
            if (!objects.TryGetValue(id, out GameObject go)) { missing.Add((string)box["path"]); continue; }
            var existing = go.GetComponent<LootContainer>();
            if (go.name.StartsWith("LootBox_Starter") || existing != null && existing.RandomTable == null)
            { preserve.Add(go.name); continue; }
            if (HasSupportedLoot(profiles[(string)box["profileId"]])) keep.Add(go.name);
            else
            {
                GameObject root = RemovalRoot(go);
                remove.Add(new { name = root.name, path = AnimationUtility.CalculateTransformPath(root.transform, null), sourceId = id });
            }
        }
        return Newtonsoft.Json.JsonConvert.SerializeObject(new { keep = keep.Count, remove, missing, preserve });
    }

    public static string BuildAndApply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료한다.");
        var scene = SceneManager.GetSceneByName("Level_GroundZero_1");
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("GroundZero_1 씬을 연다.");
        int layer = LayerMask.NameToLayer("Interaction");
        if (layer < 0) throw new InvalidOperationException("Interaction 레이어가 없다.");
        var bootstrap = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SceneBootStrap>(true)).Single();
        var data = JObject.Parse(File.ReadAllText(Manifest));
        var profiles = ((JArray)data["profiles"]).OfType<JObject>().ToDictionary(p => (string)p["id"]);
        var objects = GetObjects(scene);
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var tables = new Dictionary<string, LootTable>();
        foreach (JObject profile in data["profiles"])
        {
            if (!HasSupportedLoot(profile)) continue;
            string id = (string)profile["id"];
            bool used = ((JArray)data["boxes"]).OfType<JObject>().Any(b => (string)b["profileId"] == id
                && objects.TryGetValue((string)b["sourceFileId"], out GameObject target) && !target.name.StartsWith("LootBox_Starter"));
            if (!used) continue;
            string label = System.Text.RegularExpressions.Regex.Replace((string)profile["label"], @"[^a-zA-Z0-9_]", "_");
            string path = Folder + "/" + label + "_" + id + ".asset";
            var table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
            bool created = table == null;
            if (created) table = ScriptableObject.CreateInstance<LootTable>();
            table.activeChance = (float)profile["activeChance"];
            table.inventorySize = (int)profile["inventorySize"];
            var rolls = ParseRolls(profile);
            table.minRolls = rolls.x;
            table.maxRolls = rolls.y;
            table.emptyRollChance = GetEmptyRollChance(profile);
            table.entries = ReadEntries((JArray)profile["entries"]);
            table.fixedItemSpawnChance = (float)profile["fixedItemSpawnChance"];
            table.fixedItems = ReadEntries((JArray)profile["fixedEntries"]);
            table.sourceNote = "GroundZero 원본 상자/태그/레어도 가중치를 계산한 후 지원 아이템만 재정규화. 원본 후보 질량: "
                + profile["retainedProbabilityMass"] + "\n태그: " + profile["sourceTags"] + "\n레어도: " + profile["sourceQualities"]
                + "\nhttps://escapefromduckov.net/ko/items/bandage";
            if (table.emptyRollChance > 0f)
                table.sourceNote += "\n냄비 단일 후보 표: 제외한 후보는 빈 결과로 유지하며 재추첨하지 않는다.";
            if (created) AssetDatabase.CreateAsset(table, path);
            else EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssetIfDirty(table);
            tables[id] = table;
        }

        var containers = new List<LootContainer>();
        var removals = new List<GameObject>();
        int preserved = 0;
        int unmatched = 0;
        foreach (JObject box in data["boxes"])
        {
            if (!objects.TryGetValue((string)box["sourceFileId"], out GameObject go)) { unmatched++; continue; }
            var container = go.GetComponent<LootContainer>();
            if (go.name.StartsWith("LootBox_Starter") || container != null && container.RandomTable == null)
            {
                if (container != null) containers.Add(container);
                preserved++;
                continue;
            }
            if (!tables.TryGetValue((string)box["profileId"], out LootTable table))
            {
                GameObject root = RemovalRoot(go);
                if (!removals.Contains(root)) removals.Add(root);
                continue;
            }
            if (container == null) container = Undo.AddComponent<LootContainer>(go);
            var serialized = new SerializedObject(container);
            serialized.FindProperty("randomTable").objectReferenceValue = table;
            serialized.FindProperty("containerSize").intValue = table.inventorySize > 8 ? 16 : table.inventorySize > 4 ? 8 : 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var interactable = go.GetComponent<LootInteractable>();
            if (interactable == null) interactable = Undo.AddComponent<LootInteractable>(go);
            var interaction = new SerializedObject(interactable);
            interaction.FindProperty("lootContainer").objectReferenceValue = container;
            interaction.ApplyModifiedPropertiesWithoutUndo();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            containers.Add(container);
        }
        // 삭제 대상이 유지할 상자의 부모라면 잘못된 원본 대응이므로 삭제 전에 중단한다.
        foreach (var remove in removals)
            if (containers.Any(c => c.transform.IsChildOf(remove.transform))) throw new InvalidOperationException("삭제 대상 아래 지원 상자가 있다: " + remove.name);
        var initializer = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StageLootInitializer>(true)).SingleOrDefault();
        if (initializer == null)
        {
            var go = new GameObject("StageLootInitializer");
            SceneManager.MoveGameObjectToScene(go, scene);
            initializer = go.AddComponent<StageLootInitializer>();
        }
        var setup = new SerializedObject(initializer);
        var bindings = setup.FindProperty("containers");
        bindings.arraySize = containers.Count;
        for (int i = 0; i < containers.Count; i++) bindings.GetArrayElementAtIndex(i).objectReferenceValue = containers[i];
        setup.ApplyModifiedPropertiesWithoutUndo();
        var bootstrapSetup = new SerializedObject(bootstrap);
        bootstrapSetup.FindProperty("stageLootInitializer").objectReferenceValue = initializer;
        bootstrapSetup.ApplyModifiedPropertiesWithoutUndo();
        foreach (GameObject go in removals) Undo.DestroyObjectImmediate(go);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        return Newtonsoft.Json.JsonConvert.SerializeObject(new { tables = tables.Count, containers = containers.Count, activeContainers = containers.Count(c => c.gameObject.activeInHierarchy), removed = removals.Count, preserved, unmatched, saved });
    }

    private static LootTable.Entry[] ReadEntries(JArray entries)
    {
        return entries.OfType<JObject>().Select(e => new LootTable.Entry
        {
            itemType = (ItemType)Enum.Parse(typeof(ItemType), (string)e["itemType"]),
            itemId = (int)e["itemId"], weight = (float)e["weight"],
            minCount = (int)e["minCount"], maxCount = (int)e["maxCount"]
        }).ToArray();
    }
}
