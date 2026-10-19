using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// 조사 JSON → 공유 SO → 스폰 프리팹/구역 참조만 연결하는 제작 도구다.
// 원본 파일과 씬의 배치·인원·AI 수치는 변경하지 않는다.
public static class EnemyEquipmentTableBuilder
{
    public const string Folder = "Assets/GameData/EnemySpawns/Equipment";
    private const string Prefabs = "Assets/Characters/GameCharacters/Spawning/";
    private const string Manifest = ".codex/data/enemy-equipment-tables.json";

    [MenuItem("Tools/ExtractionShooter/Characters/Build Enemy Equipment Tables")]
    private static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 실행한다.");
        var source = JObject.Parse(File.ReadAllText(Manifest));
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) throw new InvalidOperationException("Enemy 레이어가 없다.");
        var items = (JArray)JObject.Parse(File.ReadAllText(".codex/data/random-equipment-source.json"))["items"];
        var rows = File.ReadAllLines("Assets/GameData/Weapons/weapons.csv").Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Split(',')).ToArray();
        int typeColumn = Array.IndexOf(rows[0], "ammoType");
        var ammoIds = rows.Skip(1).ToDictionary(r => int.Parse(r[0]), r => r[typeColumn] == "Rifle" ? 2001 : r[typeColumn] == "Pistol" ? 2012 : 2022);
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/GameData/EnemySpawns", "Equipment");
        var tables = new Dictionary<string, EnemyEquipmentTable>();
        foreach (JObject profile in source["profiles"])
        {
            string id = (string)profile["id"], path = Folder + "/" + id + ".asset";
            var table = AssetDatabase.LoadAssetAtPath<EnemyEquipmentTable>(path);
            bool create = table == null;
            if (create) table = ScriptableObject.CreateInstance<EnemyEquipmentTable>();
            table.weapon = ReadSlot((JObject)profile["slots"]["Weapon"], ammoIds, true);
            table.armor = ReadSlot((JObject)profile["slots"]["Armor"], ammoIds, false);
            table.helmet = ReadSlot((JObject)profile["slots"]["Helmet"], ammoIds, false);
            table.sourceNote = (string)profile["wiki"] + "\n" + (string)profile["sourcePath"] + " (GUID " + (string)profile["sourceGuid"] + ")\n"
                + (string)source["policy"] + "\nProtection: rarity weight / eligible source items in rarity, normalized over nonempty eligible rarities. CSV rarity is not armor level.";
            if (create) AssetDatabase.CreateAsset(table, path);
            else EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssetIfDirty(table);
            tables.Add(id, table);
        }

        int configured = 0;
        foreach (string name in new[] { "Enemy_Ranged", "Enemy_USEC", "Squad_PatoChapo" })
        {
            string path = Prefabs + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var part in root.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = enemyLayer;
                var commander = root.GetComponentInChildren<SquadCommander>(true);
                foreach (var equipment in root.GetComponentsInChildren<EnemyEquipmentLoadout>(true))
                {
                    string profile = name == "Enemy_Ranged" ? "Scav" : name == "Enemy_USEC" ? "USECZero"
                        : equipment.gameObject == commander.Leader.gameObject ? "PatoChapo" : "USECWarehouse";
                    var data = new SerializedObject(equipment);
                    data.FindProperty("spawnEquipmentTable").objectReferenceValue = tables[profile];
                    data.ApplyModifiedPropertiesWithoutUndo();
                    ConfigureView(equipment, items);
                    configured++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        var assigned = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:EnemySpawnTable", new[] { "Assets/GameData/EnemySpawns/GroundZeroAreas" }))
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(AssetDatabase.GUIDToAssetPath(guid));
            if (table.name.StartsWith("Squad_")) continue; // 각 대원의 서로 다른 표를 덮어쓰지 않는다.
            string profile;
            if (table.name.StartsWith("USEC_")) profile = "USECZero";
            else
            {
                string suffix = table.name.Substring("Ranged_00_".Length);
                var spawn = source["spawns"].FirstOrDefault(s => (string)s["name"] == "EnemySpawner_" + suffix);
                var supported = spawn?["presets"].Where(p => p["profile"].Type != JTokenType.Null).Select(p => (string)p["profile"]).Distinct().ToArray();
                if (supported == null || supported.Length != 1) throw new InvalidOperationException("구역의 원거리 장비 프리셋을 하나로 식별할 수 없다: " + table.name);
                profile = supported[0];
            }
            var data = new SerializedObject(table);
            var entries = data.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++) entries.GetArrayElementAtIndex(i).FindPropertyRelative("equipmentTable").objectReferenceValue = tables[profile];
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(table);
            assigned.Add(table.name + ":" + profile);
        }
        return Newtonsoft.Json.JsonConvert.SerializeObject(new { tables = tables.Count, configuredEnemies = configured, areas = assigned });
    }

    private static EnemyEquipmentTable.Slot ReadSlot(JObject source, Dictionary<int, int> ammoIds, bool weapon)
    {
        if (source["missing"] is JArray missing && missing.Count > 0) throw new InvalidOperationException("미등록 장비 후보: " + missing);
        return new EnemyEquipmentTable.Slot
        {
            equipChance = (float)source["chance"],
            entries = source["entries"].Select(e => new EnemyEquipmentTable.Entry
            { itemId = (int)e["itemId"], ammoId = weapon ? ammoIds[(int)e["itemId"]] : 0, weight = (float)e["weight"] }).ToArray()
        };
    }

    private static void ConfigureView(EnemyEquipmentLoadout equipment, JArray items)
    {
        var view = equipment.GetComponent<EnemyProtectionView>();
        if (view == null) view = equipment.gameObject.AddComponent<EnemyProtectionView>();
        var transforms = equipment.GetComponentsInChildren<Transform>(true);
        var data = new SerializedObject(view);
        data.FindProperty("protectionLoadout").objectReferenceValue = equipment.GetComponent<EnemyProtectionLoadout>();
        data.FindProperty("armorSocket").objectReferenceValue = transforms.Single(t => t.name == "ArmorSocket");
        data.FindProperty("helmetSocket").objectReferenceValue = transforms.Single(t => t.name == "HelmatSocket");
        var visualEntries = data.FindProperty("visualEntries");
        var protection = items.Where(i => (string)i["kind"] != "Weapon").ToArray();
        visualEntries.arraySize = protection.Length;
        for (int i = 0; i < protection.Length; i++)
        {
            var item = protection[i];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)item["outputPath"]);
            if (prefab == null) throw new InvalidOperationException("보호구 외형이 없다: " + item["projectId"]);
            var entry = visualEntries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("itemId").intValue = (int)item["projectId"];
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
            entry.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
            entry.FindPropertyRelative("localScale").vector3Value = Vector3.one;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
