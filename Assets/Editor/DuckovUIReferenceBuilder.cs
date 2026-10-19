using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 원본 UI의 표시 계층만 정리한다. 실제 인벤토리나 전투 데이터는 연결하지 않는다.
public static class DuckovUIReferenceBuilder
{
    private const string Folder = "Assets/Art/UI/DuckovReference/Layouts";
    private const string ScenePath = "Assets/Test/UIReference/Duckov_UIReference.unity";

    [MenuItem("Tools/ExtractionShooter/UI/Prepare Imported References")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 실행한다.");
        JObject manifest = JObject.Parse(File.ReadAllText(Folder + "/source-manifest.json"));
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Loading/Fonts/Jua-Loading SDF.asset");
        var validation = new JArray();
        Directory.CreateDirectory(Folder + "/Previews");
        Directory.CreateDirectory("Assets/Test/UIReference");
        AssetDatabase.Refresh();
        foreach (JObject entry in manifest["references"])
        {
            string prefabPath = (string)entry["prefab"];
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (JObject shape in entry["convertedProceduralImages"])
                {
                    string source = (string)entry["sourcePath"], full = (string)shape["path"];
                    Transform target = full == source ? root.transform : root.transform.Find(full.Substring(source.Length + 1));
                    var image = target != null ? target.GetComponent<UnityEngine.UI.Image>() : null;
                    if (image == null) throw new InvalidOperationException("그래픽 경로를 찾지 못했다: " + full);
                    string radiusText = shape["modifiers"].FirstOrDefault()?.ToString() ?? "0";
                    float radius;
                    if (!float.TryParse(radiusText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out radius))
                    {
                        var numbers = System.Text.RegularExpressions.Regex.Matches(radiusText, @"-?\d+(?:\.\d+)?").Cast<System.Text.RegularExpressions.Match>();
                        radius = numbers.Select(m => float.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max();
                    }
                    float border = (float)shape["borderWidth"];
                    if (image.GetComponent<UnityEngine.UI.Mask>() != null)
                    {
                        image.enabled = true;
                        image.sprite = null;
                        image.color = Color.white;
                        Transform oldPreview = image.transform.Find("ReferenceCircle");
                        if (oldPreview != null) oldPreview.gameObject.SetActive(false);
                    }
                    else if ((bool?)shape["round"] == true) CreateRoundPreview(image, border);
                    else if (border > 0) CreateBorder(image, border);
                    else
                    {
                        image.enabled = true;
                        string originalSprite = (string)shape["originalSprite"];
                        var guid = System.Text.RegularExpressions.Regex.Match(originalSprite ?? "", @"guid: ([a-f0-9]{32})");
                        image.sprite = guid.Success ? AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid.Groups[1].Value)) : null;
                    }
                }
                foreach (JObject state in entry["canvasGroups"] ?? new JArray())
                {
                    string relative = ((string)state["path"]).TrimStart('/');
                    Transform target = relative.Length == 0 ? root.transform : root.transform.Find(relative);
                    if (target != null && target.GetComponent<CanvasGroup>() != null) target.GetComponent<CanvasGroup>().alpha = (float)state["alpha"];
                }
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.font = font;
                    text.fontSharedMaterial = font.material;
                    if (System.Text.RegularExpressions.Regex.IsMatch(text.text, @"[\u3400-\u9fff]")) text.text = "Reference";
                    text.raycastTarget = false;
                }
                SetSampleState(root, (string)entry["name"]);
                int missing = root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                if (missing != 0) throw new InvalidOperationException(prefabPath + ": Missing Script " + missing);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                validation.Add(new JObject { ["name"] = (string)entry["name"], ["missingScripts"] = missing,
                    ["graphics"] = root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Length,
                    ["grids"] = new JArray(root.GetComponentsInChildren<UnityEngine.UI.GridLayoutGroup>(true).Select(g => new JObject {
                        ["name"] = g.gameObject.name, ["parent"] = g.transform.parent.gameObject.name, ["children"] = g.transform.childCount,
                        ["inDetails"] = g.GetComponentsInParent<Transform>(true).Any(t => t.gameObject.name == "ItemDetails") })),
                    ["buttonsWithGameCallbacks"] = root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Count(b => b.onClick.GetPersistentEventCount() != 0) });
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        BuildPreviewScene(manifest);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        File.WriteAllText(Folder + "/validation.json", new JObject { ["references"] = validation, ["scene"] = ScenePath,
            ["scope"] = "Static visual references only; no gameplay integration." }.ToString());
        Debug.Log("UI 레퍼런스 준비 완료: " + ScenePath);
    }

    // 원형 그래픽의 샘플 진행 상태를 텍스처로 굽는다. 런타임 진행 애니메이션이 아니다.
    private static void CreateRoundPreview(UnityEngine.UI.Image source, float border)
    {
        // 재장전 HUD는 정적 미리보기 대신 런타임 fillAmount를 표시한다.
        if (source.transform.root.name == "ReloadProgress_Reference")
        {
            ConfigureReloadRing(source, border);
            return;
        }
        foreach (Transform child in source.transform.Cast<Transform>().Where(t => t.name.StartsWith("ReferenceBorder_")).ToArray())
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        source.enabled = false;
        Transform existing = source.transform.Find("ReferenceCircle");
        var target = existing != null ? existing.gameObject : new GameObject("ReferenceCircle", typeof(RectTransform), typeof(UnityEngine.UI.RawImage), typeof(UnityEngine.UI.LayoutElement));
        target.layer = 5;
        target.transform.SetParent(source.transform, false);
        target.transform.SetAsFirstSibling();
        var rect = target.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        target.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        var raw = target.GetComponent<UnityEngine.UI.RawImage>();
        raw.color = source.color;
        raw.raycastTarget = false;
        Directory.CreateDirectory(Folder + "/PreviewShapes");
        float width = Mathf.Max(16, Mathf.Abs(source.rectTransform.rect.width));
        float height = Mathf.Max(16, Mathf.Abs(source.rectTransform.rect.height));
        bool partial = source.type == UnityEngine.UI.Image.Type.Filled;
        bool radial = partial && source.fillMethod == UnityEngine.UI.Image.FillMethod.Radial360;
        string file = Folder + "/PreviewShapes/Rounded_" + Mathf.RoundToInt(border * 10) + "_" + Mathf.RoundToInt(width) + "x" + Mathf.RoundToInt(height) + "_" + partial + "_" + radial + ".asset";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
        if (texture == null)
        {
            float scale = Mathf.Min(1, 256 / Mathf.Max(width, height));
            int textureWidth = Mathf.Max(16, Mathf.RoundToInt(width * scale));
            int textureHeight = Mathf.Max(16, Mathf.RoundToInt(height * scale));
            texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            texture.name = "Static rounded UI preview";
            float radius = Mathf.Min(textureWidth, textureHeight) * 0.5f;
            for (int y = 0; y < textureHeight; y++) for (int x = 0; x < textureWidth; x++)
            {
                Vector2 delta = new Vector2(x + 0.5f - textureWidth * 0.5f, y + 0.5f - textureHeight * 0.5f);
                Vector2 q = new Vector2(Mathf.Abs(delta.x) - (textureWidth * 0.5f - radius), Mathf.Abs(delta.y) - (textureHeight * 0.5f - radius));
                float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
                float alpha = Mathf.Clamp01(0.5f - distance);
                if (border > 0) alpha *= Mathf.Clamp01(distance + border * scale + 0.5f);
                float angle = Mathf.Repeat(Mathf.Atan2(delta.x, delta.y) / (2 * Mathf.PI), 1);
                if (partial && (radial ? angle > 0.65f : (x + 0.5f) / textureWidth > 0.65f)) alpha = 0;
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            AssetDatabase.CreateAsset(texture, file);
        }
        raw.texture = texture;
    }

    // 편집 시 전체 링 Sprite를 저장한다. 런타임에는 기존 Image의 값만 갱신한다.
    public static void ConfigureReloadRing(UnityEngine.UI.Image source, float border)
    {
        bool background = source.name == "FillBackground";
        string directory = Folder + "/Sprites";
        Directory.CreateDirectory(directory);
        string path = directory + (background ? "/ReloadRingBackground.png" : "/ReloadRing.png");
        // 현재 프리팹의 60px/15px, 50px/5px 비율을 유지한다.
        const int scale = 4;
        int size = background ? 60 * scale : 50 * scale;
        float radius = size * 0.5f;
        float innerRadius = radius - border * scale;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        try
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x + 0.5f - radius, y + 0.5f - radius).magnitude;
                float alpha = Mathf.Clamp01(radius - distance + 0.5f) *
                    Mathf.Clamp01(distance - innerRadius + 0.5f);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.spriteBorder = Vector4.zero;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        source.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        source.enabled = true;
        source.type = background ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Filled;
        source.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        source.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
        source.fillClockwise = true;
        source.raycastTarget = false;
        Transform preview = source.transform.Find("ReferenceCircle");
        if (preview != null) preview.gameObject.SetActive(false);
    }

    // 전용 셰이더의 테두리를 일반 uGUI 이미지 네 변으로 표시한다. 원본 Sprite 메타데이터는 수정하지 않는다.
    private static void CreateBorder(UnityEngine.UI.Image source, float width)
    {
        source.enabled = false;
        if (source.transform.Find("ReferenceBorder_0") != null) return;
        Vector2[] minimum = { new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 0), new Vector2(1, 0) };
        Vector2[] maximum = { new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        Vector2[] pivots = { new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 0.5f), new Vector2(1, 0.5f) };
        for (int i = 0; i < 4; i++)
        {
            var edge = new GameObject("ReferenceBorder_" + i, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            edge.layer = 5;
            edge.transform.SetParent(source.transform, false);
            var rect = edge.GetComponent<RectTransform>();
            rect.anchorMin = minimum[i];
            rect.anchorMax = maximum[i];
            rect.pivot = pivots[i];
            rect.sizeDelta = i < 2 ? new Vector2(0, width) : new Vector2(width, 0);
            edge.GetComponent<UnityEngine.UI.Image>().color = source.color;
            edge.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            edge.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        }
    }

    private static void SetSampleState(GameObject root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (new[] { "WhiteBlink", "DeathIndicator", "DamageBar", "Hovering", "Highlight", "DenialIndicator", "SelectionIndicator" }.Contains(t.name)) t.gameObject.SetActive(false);
        if (name == "PlayerHealth") foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true)) t.text = "78 / 100";
        if (name == "UnitHealthBar") foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true)) t.text = "Enemy";
        if (name == "ReloadProgress") foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true)) t.text = t.name == "ActionText" ? "Reloading" : "1.2s";
        if (name == "InteractionProgress") foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true)) if (t.text == "Reference") t.text = "Searching";
        if (name == "PlayerStamina") foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true)) if (image.name == "Fill") image.color = new Color(0.75f, 1, 0.4f, 1);
        if (name == "InteractionPrompt") foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true)) if (t.text == "Reference") t.text = "Interact";
        foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true)) if (image.type == UnityEngine.UI.Image.Type.Filled) image.fillAmount = 0.65f;
        foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.RawImage>(true)) if (image.texture == null) image.gameObject.SetActive(false);
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true)) if (t.text.Contains("123456789")) t.text = "1,250";
        if (name == "ReloadProgress") foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true)) t.gameObject.SetActive(true);
        if (name == "QuickSlots" || name == "WeaponSlots" || name == "InventorySlot") ClearPlaceholderItem(root.transform);
        if (name == "Inventory" || name == "Warehouse")
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (new[] { "ItemDetails", "ShortcutEditor", "LoadingIndicator", "InventoryDisplay_Pet", "WeightBar" }.Contains(t.name)) t.gameObject.SetActive(false);
            // 원본의 실행 중 레이아웃 갱신을 제외하면 Scroll View의 선호 높이가 0이 된다.
            foreach (var scroll in root.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))
            {
                if (scroll.GetComponentsInParent<Transform>(true).Any(t => t.name == "ItemDetails" || t.name == "InventoryDisplay_Pet")) continue;
                var layout = scroll.GetComponent<UnityEngine.UI.LayoutElement>() ?? scroll.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                layout.minHeight = 420;
                layout.preferredHeight = 540;
                layout.flexibleHeight = 1;
            }
            foreach (var grid in root.GetComponentsInChildren<UnityEngine.UI.GridLayoutGroup>(true))
            {
                if (grid.GetComponentsInParent<Transform>(true).Any(t => t.name == "ItemDetails" || t.name == "InventoryDisplay_Pet")) continue;
                if (grid.name == "GridLayout" && grid.transform.parent.name == "ItemSlotsDisplay")
                {
                    foreach (Transform parent in grid.GetComponentsInParent<Transform>(true)) parent.gameObject.SetActive(true);
                    foreach (Transform child in grid.transform) if (child.name.StartsWith("SlotDisplay"))
                    {
                        child.gameObject.SetActive(true);
                        ClearPlaceholderItem(child);
                    }
                }
                if (grid.name != "Layout" || grid.transform.parent.name != "Container") continue;
                foreach (Transform parent in grid.GetComponentsInParent<Transform>(true)) parent.gameObject.SetActive(true);
                int count = name == "Warehouse" && grid.transform.GetComponentsInParent<Transform>(true).Any(t => t.name == "LootTarget") ? 64 : 20;
                Transform template = grid.transform.Cast<Transform>().FirstOrDefault(t => !t.name.StartsWith("SampleSlot_"));
                if (template == null)
                {
                    var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/InventorySlot_Reference.prefab");
                    template = UnityEngine.Object.Instantiate(slotPrefab, grid.transform, false).transform;
                    template.name = "EntryTemplate";
                }
                template.gameObject.SetActive(false);
                for (int i = 0; i < count; i++)
                {
                    string slotName = "SampleSlot_" + i;
                    Transform slot = grid.transform.Find(slotName);
                    if (slot == null)
                    {
                        slot = UnityEngine.Object.Instantiate(template.gameObject, grid.transform).transform;
                        slot.name = slotName;
                    }
                    slot.gameObject.SetActive(true);
                    ClearPlaceholderItem(slot);
                }
                grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 4;
                grid.cellSize = new Vector2(120, 120);
                grid.spacing = new Vector2(8, 8);
                int rows = Mathf.CeilToInt(count / 4f);
                float height = grid.padding.top + grid.padding.bottom + rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
                float width = grid.padding.left + grid.padding.right + 4 * grid.cellSize.x + 3 * grid.spacing.x;
                var gridRect = (RectTransform)grid.transform;
                gridRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                var element = grid.GetComponent<UnityEngine.UI.LayoutElement>() ?? grid.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                element.minHeight = element.preferredHeight = height;
                element.preferredWidth = width;
                var fitter = grid.GetComponent<UnityEngine.UI.ContentSizeFitter>() ?? grid.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            }
            Canvas.ForceUpdateCanvases();
            foreach (var scroll in root.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true)) scroll.verticalNormalizedPosition = 1;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.text == "Reference" && text.GetComponentsInParent<Transform>(true).Any(t => t.name == "Title"))
                    text.text = name == "Warehouse" && text.GetComponentsInParent<Transform>(true).Any(t => t.name == "LootTarget") ? "Storage" : "Inventory";
        }
    }

    private static void ClearPlaceholderItem(Transform root)
    {
        string[] hidden = { "Icon", "CountContainer", "Durability", "DurabilityContainer", "DisplayName", "Name", "InspectioningIndicator", "NotInteractableIndicator", "SlotIndicators", "WishlistInfo", "UseIndicator", "LockIndicator", "DisabledIndicator" };
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (hidden.Contains(t.name) || t.name.IndexOf("lock", StringComparison.OrdinalIgnoreCase) >= 0) t.gameObject.SetActive(false);
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            if (!t.transform.GetComponentsInParent<Transform>(true).Any(p => p.name == "InputIndicator")) t.gameObject.SetActive(false);
        foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            if (image.name == "BG") image.color = new Color(0.13f, 0.2f, 0.26f, 0.8f);
            if (image.name == "Frame")
            {
                image.color = new Color(0.7f, 0.75f, 0.8f, 0.7f);
                foreach (Transform child in image.transform) if (child.name.StartsWith("ReferenceBorder_")) child.GetComponent<UnityEngine.UI.Image>().color = image.color;
            }
            Transform circle = image.transform.Find("ReferenceCircle");
            if (circle != null) circle.GetComponent<UnityEngine.UI.RawImage>().color = image.color;
        }
    }

    private static void BuildPreviewScene(JObject manifest)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var cameraObject = new GameObject("Reference Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 540;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.12f, 0.16f, 1);
            camera.cullingMask = 1 << 5;
            var lightObject = new GameObject("Reference Light", typeof(Light));
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.GetComponent<Light>().type = LightType.Directional;
            var canvasObject = new GameObject("Reference Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            canvasObject.layer = 5;
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var roots = new List<GameObject>();
            foreach (JObject entry in manifest["references"])
            {
                string name = (string)entry["name"];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)entry["prefab"]);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = name + "_Reference";
                instance.transform.SetParent(canvasObject.transform, false);
                var rect = instance.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                if (name == "Inventory" || name == "Warehouse") rect.sizeDelta = new Vector2(1760, 920);
                instance.SetActive(true);
                Canvas.ForceUpdateCanvases();
                Capture(camera, name);
                instance.SetActive(false);
                roots.Add(instance);
            }
            // 겹치지 않는 기본 HUD 비교 배치다. 원본 좌표는 개별 프리팹에 보존한다.
            string[] hudNames = { "PlayerHealth", "PlayerStamina", "QuickSlots", "ReloadProgress", "InteractionProgress", "InteractionPrompt", "UnitHealthBar", "WeaponSlots" };
            Vector2[] positions = { new Vector2(-650, -420), new Vector2(-620, -330), new Vector2(0, -420), new Vector2(0, 200), new Vector2(0, 20), new Vector2(350, 120), new Vector2(-400, 220), new Vector2(-590, -160) };
            for (int i = 0; i < hudNames.Length; i++)
            {
                int index = manifest["references"].ToList().FindIndex(e => (string)e["name"] == hudNames[i]);
                roots[index].SetActive(true);
                roots[index].GetComponent<RectTransform>().anchoredPosition = positions[i];
            }
            Canvas.ForceUpdateCanvases();
            Capture(camera, "HUD_Overview");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void Capture(Camera camera, string name)
    {
        var renderTexture = new RenderTexture(1920, 1080, 24);
        var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = renderTexture;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            if (name == "Inventory" || name == "Warehouse")
            {
                var records = new JArray();
                var sceneCanvas = camera.gameObject.scene.GetRootGameObjects().First(g => g.name == "Reference Canvas");
                foreach (var grid in sceneCanvas.GetComponentsInChildren<UnityEngine.UI.GridLayoutGroup>(true).Where(g => g.gameObject.activeInHierarchy))
                {
                    var parents = new JArray();
                    foreach (RectTransform rect in grid.GetComponentsInParent<RectTransform>(true).Take(7))
                        parents.Add(new JObject { ["name"] = rect.gameObject.name, ["width"] = rect.rect.width, ["height"] = rect.rect.height,
                            ["position"] = rect.position.ToString(), ["mask"] = rect.GetComponent<UnityEngine.UI.Mask>() != null, ["rectMask"] = rect.GetComponent<UnityEngine.UI.RectMask2D>() != null });
                    records.Add(new JObject { ["name"] = grid.gameObject.name, ["parents"] = parents });
                }
                File.WriteAllText(Folder + "/Previews/" + name + "-layout.json", records.ToString());
            }
            RenderTexture.active = renderTexture;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Folder + "/Previews/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
