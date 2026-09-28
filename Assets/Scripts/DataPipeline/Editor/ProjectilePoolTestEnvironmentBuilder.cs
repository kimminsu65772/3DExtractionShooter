using ExtractionShooter.DataPipeline;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ExtractionShooter.EditorTools
{
    // 코드 컴파일이 끝나면 테스트 프리팹, Addressables 항목, 전용 씬을 한 번 생성한다.
    [InitializeOnLoad]
    public static class ProjectilePoolTestEnvironmentBuilder
    {
        private const string SourcePrefabPath = "Assets/Prefabs/Weapon/Projectile.prefab";
        private const string TestFolder = "Assets/Tests/PlayMode/ProjectilePool";
        private const string TestPrefabPath = TestFolder + "/ProjectilePoolTest.prefab";
        private const string TestScenePath = "Assets/Scenes/ProjectilePoolTest.unity";
        private const string AddressablesGroupName = "Combat Pipeline Tests";
        private const string ProjectileAddress = "tests/projectile_pool";

        static ProjectilePoolTestEnvironmentBuilder()
        {
            EditorApplication.delayCall += BuildAutomaticallyIfMissing;
        }

        [MenuItem("Tools/Extraction Shooter/Rebuild Projectile Pool Test Environment")]
        public static void RebuildFromMenu()
        {
            Build(forceRebuild: true);
        }

        private static void BuildAutomaticallyIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            bool sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) != null;
            bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabPath) != null;

            if (sceneExists && prefabExists && HasAddressableEntry())
                return;

            Build(forceRebuild: false);
        }

        private static void Build(bool forceRebuild)
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);

            if (sourcePrefab == null)
            {
                Debug.LogError($"투사체 원본 프리팹을 찾을 수 없습니다: {SourcePrefabPath}");
                return;
            }

            EnsureFolder(TestFolder);

            if (forceRebuild || AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabPath) == null)
                CreateTestPrefab();

            RegisterAddressablePrefab();

            if (forceRebuild || AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null)
                CreateTestScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[CORE-03] 투사체 풀 테스트 환경 생성 완료. " +
                $"씬을 열고 Play 하세요: {TestScenePath}");
        }

        private static void CreateTestPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SourcePrefabPath);

            try
            {
                if (root.GetComponent<Projectile>() == null)
                    throw new MissingComponentException("원본 투사체 프리팹에 Projectile이 없습니다.");

                if (root.GetComponent<PooledProjectileLifecycle>() == null)
                    root.AddComponent<PooledProjectileLifecycle>();

                if (root.GetComponent<ProjectilePoolTestProbe>() == null)
                    root.AddComponent<ProjectilePoolTestProbe>();

                PrefabUtility.SaveAsPrefabAsset(root, TestPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RegisterAddressablePrefab()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.GetSettings(create: true);

            AddressableAssetGroup group = settings.FindGroup(AddressablesGroupName);

            if (group == null)
            {
                group = settings.CreateGroup(
                    AddressablesGroupName,
                    setAsDefaultGroup: false,
                    readOnly: false,
                    postEvent: false,
                    schemasToCopy: null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
            }

            string guid = AssetDatabase.AssetPathToGUID(TestPrefabPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(
                guid,
                group,
                readOnly: false,
                postEvent: false);

            entry.address = ProjectileAddress;
            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.EntryModified,
                entry,
                postEvent: true,
                settingsModified: true);
        }

        private static bool HasAddressableEntry()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
                return false;

            string guid = AssetDatabase.AssetPathToGUID(TestPrefabPath);

            if (string.IsNullOrEmpty(guid))
                return false;

            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            return entry != null && entry.address == ProjectileAddress;
        }

        private static void CreateTestScene()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            bool useSingleSceneMode = Application.isBatchMode;

            // 열린 에디터에서 저장되지 않은 사용자 씬을 Single 생성으로 덮어쓰지 않는다.
            // 이 경우에는 기존 씬을 먼저 저장한 뒤 메뉴를 다시 실행해야 한다.
            if (!useSingleSceneMode && string.IsNullOrEmpty(previousScene.path))
            {
                throw new System.InvalidOperationException(
                    "현재 열린 씬이 저장되지 않았습니다. 씬을 저장한 뒤 테스트 환경 생성을 다시 실행하세요.");
            }

            Scene testScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                useSingleSceneMode
                    ? NewSceneMode.Single
                    : NewSceneMode.Additive);

            SceneManager.SetActiveScene(testScene);

            try
            {
                GameObject environment = new GameObject("ProjectilePoolTestEnvironment");
                ProjectilePoolTestRunner runner =
                    environment.AddComponent<ProjectilePoolTestRunner>();

                GameObject inactiveRootObject = new GameObject("InactivePoolRoot");
                inactiveRootObject.transform.SetParent(environment.transform);

                GameObject spawnObject = new GameObject("SpawnPoint");
                spawnObject.transform.SetParent(environment.transform);
                spawnObject.transform.position = new Vector3(0f, 1f, 0f);

                GameObject targetObject = new GameObject("TargetPoint");
                targetObject.transform.SetParent(environment.transform);
                targetObject.transform.position = new Vector3(0f, 1f, 8f);

                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "ReturnWall";
                wall.transform.position = new Vector3(0f, 1f, 8f);
                wall.transform.localScale = new Vector3(6f, 2f, 0.5f);

                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Ground";
                ground.transform.position = new Vector3(0f, -0.5f, 4f);
                ground.transform.localScale = new Vector3(12f, 1f, 12f);

                CreateCamera();
                CreateLight();

                SerializedObject serializedRunner = new SerializedObject(runner);
                serializedRunner.FindProperty("inactiveRoot").objectReferenceValue =
                    inactiveRootObject.transform;
                serializedRunner.FindProperty("spawnPoint").objectReferenceValue =
                    spawnObject.transform;
                serializedRunner.FindProperty("targetPoint").objectReferenceValue =
                    targetObject.transform;
                serializedRunner.FindProperty("hitMask").intValue = ~0;
                serializedRunner.ApplyModifiedPropertiesWithoutUndo();

                // PrefabPool은 이 비활성 부모 아래에서 프리웜 인스턴스를 생성한다.
                inactiveRootObject.SetActive(false);

                EditorSceneManager.SaveScene(testScene, TestScenePath);
            }
            finally
            {
                if (!useSingleSceneMode)
                    EditorSceneManager.CloseScene(testScene, removeScene: true);

                if (!useSingleSceneMode &&
                    previousScene.IsValid() &&
                    previousScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousScene);
                }
            }
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(8f, 7f, -4f);
            camera.transform.LookAt(new Vector3(0f, 1f, 4f));
        }

        private static void CreateLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
