using UnityEngine;
using UnityEngine.UI;

// 기본 uGUI 계층을 만든다. UI를 미리 편집해 둔 경우에는 컨트롤러가 이 생성기를 다시 호출하지 않는다.
public static class InventoryUILayout
{
    private static readonly Color PanelColor = new Color(0.055f, 0.11f, 0.16f, 0.94f);

    public static void Build(InventoryUIController owner)
    {
        GameObject root = new GameObject("Inventory UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        // 별도의 화면 Canvas를 사용해 캐릭터 루트의 회전과 크기를 UI가 물려받지 않는다.
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform windows = Rect("Windows", root.transform);
        Stretch(windows);
        // 투명한 전체 화면 이미지가 빈 공간의 클릭도 받는다. 게임 시간이나 이동 입력은 건드리지 않는다.
        windows.gameObject.AddComponent<Image>().color = Color.clear;

        RectTransform bagPanel = Panel("Bag Panel", windows, owner);
        Place(bagPanel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 15f), new Vector2(410f, 790f));
        Label("Equipment Label", bagPanel, owner, "장비", new Vector2(16f, -14f), new Vector2(350f, 32f));
        RectTransform gear = Grid("Equipment Slots", bagPanel, 4);
        TopLeft(gear, new Vector2(16f, -55f), new Vector2(378f, 96f));
        Text bagTitle = Label("Bag Title", bagPanel, owner, "가방", new Vector2(16f, -166f), new Vector2(350f, 30f));
        RectTransform bag = ScrollGrid("Bag Slots", bagPanel, new Vector2(16f, -208f), new Vector2(378f, 485f));
        Label("Weight", bagPanel, owner, "전체 소지 중량: 계산 기능 미연결", new Vector2(16f, -711f), new Vector2(378f, 30f), 16);
        RectTransform weightTrack = Panel("Weight Track", bagPanel, owner);
        TopLeft(weightTrack, new Vector2(16f, -749f), new Vector2(378f, 12f));
        weightTrack.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.28f);

        RectTransform loot = Panel("Loot Panel", windows, owner);
        Place(loot, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 150f), new Vector2(410f, 520f));
        Text lootTitle = Label("Loot Title", loot, owner, "전리품", new Vector2(16f, -16f), new Vector2(330f, 34f));
        RectTransform lootGrid = ScrollGrid("Loot Slots", loot, new Vector2(16f, -65f), new Vector2(378f, 365f));
        Label("Loot Help", loot, owner, "더블클릭: 획득  /  드래그: 이동·교환", new Vector2(16f, -460f), new Vector2(380f, 30f), 16);

        RectTransform detail = Panel("Item Details", windows, owner);
        Place(detail, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(490f, 470f));
        Image detailIcon = Icon("Icon", detail, new Vector2(16f, -16f), new Vector2(80f, 80f));
        Text detailText = Label("Description", detail, owner, string.Empty, new Vector2(24f, -110f), new Vector2(442f, 334f));
        detailText.alignment = TextAnchor.UpperLeft;

        RectTransform hotbar = Panel("Quickslots", root.transform, owner);
        Place(hotbar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(804f, 144f));
        RectTransform quick = Grid("Weapon Quickslots", hotbar, 2);
        TopLeft(quick, new Vector2(12f, -32f), new Vector2(190f, 98f));
        Label("Quickslot Help", hotbar, owner, "1·2 무기 선택  |  Tab 가방  |  F 상호작용", new Vector2(12f, -2f), new Vector2(780f, 26f), 16);
        EnsureItemQuickslotGrid(hotbar);

        Text prompt = Label("Interaction Prompt", root.transform, owner, string.Empty, Vector2.zero, new Vector2(500f, 35f));
        Place((RectTransform)prompt.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 175f), new Vector2(500f, 35f));
        prompt.alignment = TextAnchor.MiddleCenter;
        RectTransform ghost = Panel("Drag Ghost", root.transform, owner);
        Place(ghost, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(120f, 100f));
        // 드래그 그림은 마우스 판정을 받지 않아 아래 목적지 칸까지 Drop이 전달되게 한다.
        CanvasGroup group = ghost.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        Image ghostIcon = Icon("Icon", ghost, new Vector2(20f, -4f), new Vector2(80f, 60f));
        Text ghostText = Label("Name", ghost, owner, string.Empty, new Vector2(4f, -65f), new Vector2(112f, 32f), 16);
        owner.ConfigureView(canvas, windows.gameObject, loot.gameObject, detail.gameObject, bag, gear, lootGrid, quick, bagTitle, lootTitle, detailText, detailIcon, prompt, ghost, ghostText, ghostIcon);
        // 공통 칸의 원본을 숨겨 둔다. 이 한 칸의 이미지·텍스트 배치를 편집하면 모든 목록에 적용한다.
        InventorySlotView template = CreateSlot((RectTransform)root.transform, owner);
        template.name = "Slot Template";
        template.gameObject.SetActive(false);
        owner.SetSlotTemplate(template);
        windows.gameObject.SetActive(false);
        loot.gameObject.SetActive(false);
        detail.gameObject.SetActive(false);
        ghost.gameObject.SetActive(false);
    }

    public static RectTransform EnsureItemQuickslotGrid(RectTransform hotbar)
    {
        // 이전 기본 UI의 임시 칸만 숨긴다. 사용자 편집 내용은 삭제하지 않는다.
        for (int key = 3; key <= 8; key++)
        {
            Transform placeholder = hotbar.Find($"Item Quickslot {key} (Unconnected)");
            if (placeholder != null) placeholder.gameObject.SetActive(false);
        }
        // 여러 번 준비해도 소모품 목록을 중복 생성하지 않는다.
        RectTransform existing = hotbar.Find("Item Quickslots") as RectTransform;
        if (existing != null) return existing;
        RectTransform grid = Grid("Item Quickslots", hotbar, 6);
        TopLeft(grid, new Vector2(212f, -32f), new Vector2(568f, 98f));
        return grid;
    }

    public static InventorySlotView CreateSlot(RectTransform parent, InventoryUIController owner)
    {
        RectTransform slot = Panel("Slot", parent, owner);
        slot.sizeDelta = new Vector2(88f, 96f);
        Image background = slot.GetComponent<Image>();
        background.sprite = owner.SlotSprite;
        background.color = new Color(0.12f, 0.2f, 0.26f, 0.94f);
        Outline outline = slot.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.55f, 0.66f, 0.71f, 0.7f);
        outline.effectDistance = Vector2.one;
        Image icon = Icon("Icon", slot, new Vector2(6f, -5f), new Vector2(76f, 53f));
        Text name = Label("Name", slot, owner, string.Empty, new Vector2(4f, -52f), new Vector2(80f, 23f), 13);
        Text count = Label("Count", slot, owner, string.Empty, new Vector2(4f, -73f), new Vector2(80f, 19f), 14);
        count.alignment = TextAnchor.MiddleRight;
        Text key = Label("Key", slot, owner, string.Empty, new Vector2(3f, -2f), new Vector2(22f, 24f), 15);
        InventorySlotView view = slot.gameObject.AddComponent<InventorySlotView>();
        view.Configure(background, icon, name, count, key);
        return view;
    }

    private static RectTransform ScrollGrid(string name, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform viewport = Rect(name + " Viewport", parent);
        TopLeft(viewport, position, size);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.viewport = viewport;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        RectTransform content = Grid(name, viewport, 4);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;
        return content;
    }

    private static RectTransform Grid(string name, Transform parent, int columns)
    {
        RectTransform rect = Rect(name, parent);
        GridLayoutGroup layout = rect.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(88f, 96f);
        layout.spacing = new Vector2(8f, 8f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = columns;
        return rect;
    }

    private static RectTransform Panel(string name, Transform parent, InventoryUIController owner)
    {
        RectTransform rect = Rect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = PanelColor;
        image.sprite = owner.PanelSprite;
        image.type = Image.Type.Sliced;
        return rect;
    }

    private static Image Icon(string name, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = Rect(name, parent);
        TopLeft(rect, position, size);
        Image icon = rect.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;
        return icon;
    }

    private static Text Label(string name, Transform parent, InventoryUIController owner, string value, Vector2 position, Vector2 size, int fontSize = 20)
    {
        RectTransform rect = Rect(name, parent);
        TopLeft(rect, position, size);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = owner.UIFont;
        text.fontSize = fontSize;
        text.color = new Color(0.9f, 0.94f, 0.97f);
        text.text = value;
        text.raycastTarget = false;
        text.alignment = TextAnchor.MiddleLeft;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void TopLeft(RectTransform rect, Vector2 position, Vector2 size) => Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
