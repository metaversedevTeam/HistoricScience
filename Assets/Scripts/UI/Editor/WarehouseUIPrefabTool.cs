using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Figma의 '창고 ui' 프레임을 프리팹(패널·슬롯·카테고리 탭)으로 만들어 내는 에디터 도구.
// 카테고리 탭은 도감의 알약형 탭 컴포넌트(CodexAgeTabUI)를 창고 색상으로 다시 칠해 재사용한다.
public static class WarehouseUIPrefabTool
{
    public const string PanelPrefabPath = "Assets/Prefabs/UI/WarehouseUI.prefab";
    public const string SlotPrefabPath = "Assets/Prefabs/UI/Warehouse/WarehouseSlot.prefab";
    public const string TabPrefabPath = "Assets/Prefabs/UI/Warehouse/WarehouseCategoryTab.prefab";

    private const string FontAssetPath = "Assets/UI/Fonts/Test/BMHANNA_11YRS_OTF SDF.asset";

    // 화면 레이아웃 기준값 (1920x1080 기준 해상도, Figma 좌표 그대로)
    private const float ScreenPadding = 32f;
    private const float ContentTop = -97f;
    private const float ContentHeight = 867f;
    private const float CategoryPanelWidth = 220f;
    private const float GridPanelX = 276f;
    private const float GridPanelWidth = 1168f;
    private const float DetailPanelX = 1468f;
    private const float DetailPanelWidth = 420f;
    private const float BottomBarTop = -984f;
    private const float BottomBarWidth = 1856f;
    private const float BottomBarHeight = 72f;
    private const float PanelPadding = 16f;
    // 격자 폭(1136)을 6열과 열 간격 12로 나눈 슬롯 한 칸의 크기
    private const float SlotWidth = 179.3333f;
    private const float SlotHeight = 184.75f;
    private const float SlotSpacing = 12f;
    private const int SlotColumnCount = 6;
    // 아이콘이 아이콘 영역을 꽉 채우도록 남기는 사방 여백
    private const float SlotIconPadding = 8f;
    private const float DetailIconPadding = 16f;

    // 색상 팔레트
    private static readonly Color PanelFill = new Color32(0x02, 0x01, 0x08, 0xCC);
    private static readonly Color PanelOutline = new Color32(0x33, 0x33, 0x33, 0xFF);
    private static readonly Color BoxFill = new Color32(0x02, 0x01, 0x08, 0xFF);
    private static readonly Color InnerFill = new Color(1f, 1f, 1f, 0.04f);
    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color Accent = new Color32(0x25, 0x63, 0xEB, 0xFF);
    private static readonly Color AccentSoft = new Color32(0x25, 0x63, 0xEB, 0x80);
    private static readonly Color TextPrimary = Color.white;
    private static readonly Color TextStrong = new Color(1f, 1f, 1f, 0.8f);
    private static readonly Color TextMuted = new Color(1f, 1f, 1f, 0.7f);
    private static readonly Color TextFaint = new Color32(0xB3, 0xB3, 0xB3, 0xFF);
    private static readonly Color PlaceholderIcon = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color CloseButtonFill = new Color(1f, 1f, 1f, 0.08f);

    [MenuItem("Tools/HistoricScience/Generate Warehouse UI Prefabs")]
    public static void Generate()
    {
        UIProceduralSpriteFactory.GenerateAll();

        GameObject tabPrefab = BuildTabPrefab();
        GameObject slotPrefab = BuildSlotPrefab();
        GameObject panelPrefab = BuildPanelPrefab(tabPrefab, slotPrefab);

        AssetDatabase.SaveAssets();
        Debug.Log($"[WarehouseUIPrefabTool] '{AssetDatabase.GetAssetPath(panelPrefab)}' 창고 UI 프리팹을 생성했습니다.");
    }

    // ─────────────────────────────── 탭 프리팹 ───────────────────────────────

    // 카테고리 필터 탭 프리팹을 만든다. 세로 목록에서 패널 폭 전체로 늘어난다.
    private static GameObject BuildTabPrefab()
    {
        GameObject root = NewUIObject("WarehouseCategoryTab", null);
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(188f, 44f);

        Image fill = AddImage(root, UIProceduralSpriteFactory.LoadFill(14), PanelFill);
        Image outline = AddStretchedImage(root, "Outline", UIProceduralSpriteFactory.LoadLine(14), PanelOutline);

        Button button = root.AddComponent<Button>();
        button.targetGraphic = fill;
        button.transition = Selectable.Transition.None;

        var element = root.AddComponent<LayoutElement>();
        element.minHeight = 44f;
        element.preferredHeight = 44f;

        GameObject labelGO = NewUIObject("Label", root.transform);
        StretchFull(labelGO.GetComponent<RectTransform>());
        TextMeshProUGUI label = AddText(labelGO, "카테고리", 24f, new Color32(0xAF, 0xAF, 0xAF, 0xFF), TextAlignmentOptions.Center);

        var tab = root.AddComponent<CodexAgeTabUI>();
        var so = new SerializedObject(tab);
        so.FindProperty("_button").objectReferenceValue = button;
        so.FindProperty("_fill").objectReferenceValue = fill;
        so.FindProperty("_outline").objectReferenceValue = outline;
        so.FindProperty("_label").objectReferenceValue = label;
        SetColor(so, "_selectedFill", new Color32(0xC2, 0x41, 0x0C, 0xFF));
        SetColor(so, "_selectedOutline", new Color32(0xC2, 0x41, 0x0C, 0xFF));
        SetColor(so, "_selectedLabel", TextPrimary);
        SetColor(so, "_normalFill", PanelFill);
        SetColor(so, "_normalOutline", PanelOutline);
        SetColor(so, "_normalLabel", new Color32(0xAF, 0xAF, 0xAF, 0xFF));
        so.ApplyModifiedPropertiesWithoutUndo();

        return SaveAsPrefab(root, TabPrefabPath);
    }

    // ─────────────────────────────── 슬롯 프리팹 ───────────────────────────────

    // 창고 격자에 놓이는 슬롯 프리팹을 만든다. 빈 칸일 때는 아이콘 영역과 정보 줄이 꺼진다.
    private static GameObject BuildSlotPrefab()
    {
        GameObject root = NewUIObject("WarehouseSlot", null);
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(SlotWidth, SlotHeight);

        Image slotFill = AddImage(root, UIProceduralSpriteFactory.LoadFill(14), BoxFill);
        Image outline = AddStretchedImage(root, "Outline", UIProceduralSpriteFactory.LoadLine(14), PanelOutline);
        Image selectedOutline = AddStretchedImage(root, "SelectedOutline", UIProceduralSpriteFactory.LoadLine(14), Accent);
        selectedOutline.gameObject.SetActive(false);

        Button button = root.AddComponent<Button>();
        button.targetGraphic = slotFill;
        button.transition = Selectable.Transition.None;

        // 아이콘 영역
        GameObject iconAreaGO = NewUIObject("IconArea", root.transform);
        PlaceTopLeft(iconAreaGO, 10f, -10f, SlotWidth - 20f, 133.75f);
        AddImage(iconAreaGO, UIProceduralSpriteFactory.LoadFill(10), InnerFill);

        GameObject iconGO = NewUIObject("Icon", iconAreaGO.transform);
        StretchWithPadding(iconGO.GetComponent<RectTransform>(), SlotIconPadding);
        Image icon = AddImage(iconGO, null, TextPrimary);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        GameObject placeholderGO = NewUIObject("PlaceholderIcon", iconAreaGO.transform);
        PlaceCentered(placeholderGO, 0f, 0f, 44f, 44f);
        Image placeholder = AddImage(placeholderGO, UIProceduralSpriteFactory.LoadIcon("Icon_CircleX"), PlaceholderIcon);
        placeholder.raycastTarget = false;

        // 이름 + 수량
        GameObject metaGO = NewUIObject("Meta", root.transform);
        PlaceTopLeft(metaGO, 10f, -151.75f, SlotWidth - 20f, 23f);

        GameObject nameGO = NewUIObject("Name", metaGO.transform);
        PlaceTopLeft(nameGO, 0f, 0f, 100f, 23f);
        TextMeshProUGUI nameText = AddText(nameGO, "아이템", 18f, TextPrimary, TextAlignmentOptions.MidlineLeft);
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        GameObject countGO = NewUIObject("Count", metaGO.transform);
        PlaceTopRight(countGO, 0f, 0f, 59f, 23f);
        TextMeshProUGUI countText = AddText(countGO, "x0", 18f, new Color32(0x16, 0xA3, 0x4A, 0xFF), TextAlignmentOptions.MidlineRight);

        var slot = root.AddComponent<WarehouseSlotUI>();
        var so = new SerializedObject(slot);
        so.FindProperty("_button").objectReferenceValue = button;
        so.FindProperty("_outline").objectReferenceValue = outline;
        so.FindProperty("_selectedOutline").objectReferenceValue = selectedOutline;
        so.FindProperty("_iconArea").objectReferenceValue = iconAreaGO.GetComponent<RectTransform>();
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_placeholderIcon").objectReferenceValue = placeholder;
        so.FindProperty("_meta").objectReferenceValue = metaGO.GetComponent<RectTransform>();
        so.FindProperty("_nameText").objectReferenceValue = nameText;
        so.FindProperty("_countText").objectReferenceValue = countText;
        so.ApplyModifiedPropertiesWithoutUndo();

        return SaveAsPrefab(root, SlotPrefabPath);
    }

    // ─────────────────────────────── 패널 프리팹 ───────────────────────────────

    // 창고 UI 전체(딤 + 헤더 + 3분할 본문 + 하단 바) 프리팹을 만든다.
    private static GameObject BuildPanelPrefab(GameObject tabPrefab, GameObject slotPrefab)
    {
        GameObject root = NewUIObject("WarehouseUI", null);
        StretchFull(root.GetComponent<RectTransform>());

        // 뒤 화면을 가리는 딤 — 클릭이 뒤로 새지 않도록 raycastTarget을 켜 둔다
        GameObject dimGO = NewUIObject("Dim", root.transform);
        StretchFull(dimGO.GetComponent<RectTransform>());
        AddImage(dimGO, null, Dim);

        Button closeButton = BuildHeader(root);
        RectTransform tabParent = BuildCategoryPanel(root);
        BuildGridPanel(root, out TMP_InputField searchInput, out ScrollRect scrollRect, out RectTransform slotParent);
        BuildDetailPanel(root, out Image detailIcon, out Image detailPlaceholder, out RectTransform detailInfo,
            out TextMeshProUGUI detailName, out TextMeshProUGUI detailDescription,
            out RectTransform detailQuantity, out TextMeshProUGUI detailQuantityText);
        BuildBottomBar(root, out TextMeshProUGUI capacityText, out RectTransform capacityFill);

        var warehouseUI = root.AddComponent<WarehouseUI>();
        var so = new SerializedObject(warehouseUI);
        so.FindProperty("_tabPrefab").objectReferenceValue = tabPrefab.GetComponent<CodexAgeTabUI>();
        so.FindProperty("_tabParent").objectReferenceValue = tabParent;
        so.FindProperty("_slotPrefab").objectReferenceValue = slotPrefab.GetComponent<WarehouseSlotUI>();
        so.FindProperty("_slotParent").objectReferenceValue = slotParent;
        so.FindProperty("_scrollRect").objectReferenceValue = scrollRect;
        so.FindProperty("_searchInput").objectReferenceValue = searchInput;
        so.FindProperty("_closeButton").objectReferenceValue = closeButton;
        so.FindProperty("_detailIcon").objectReferenceValue = detailIcon;
        so.FindProperty("_detailPlaceholderIcon").objectReferenceValue = detailPlaceholder;
        so.FindProperty("_detailInfo").objectReferenceValue = detailInfo;
        so.FindProperty("_detailNameText").objectReferenceValue = detailName;
        so.FindProperty("_detailDescriptionText").objectReferenceValue = detailDescription;
        so.FindProperty("_detailQuantity").objectReferenceValue = detailQuantity;
        so.FindProperty("_detailQuantityText").objectReferenceValue = detailQuantityText;
        so.FindProperty("_capacityText").objectReferenceValue = capacityText;
        so.FindProperty("_capacityFill").objectReferenceValue = capacityFill;
        so.ApplyModifiedPropertiesWithoutUndo();

        return SaveAsPrefab(root, PanelPrefabPath);
    }

    // 제목과 우상단 닫기(X) 버튼으로 이루어진 화면 헤더를 만든다.
    private static Button BuildHeader(GameObject root)
    {
        GameObject headerGO = NewUIObject("Header", root.transform);
        PlaceTopLeft(headerGO, ScreenPadding, -ScreenPadding, BottomBarWidth, 45f);

        GameObject titleGO = NewUIObject("Title", headerGO.transform);
        PlaceTopLeft(titleGO, 0f, 0f, 400f, 45f);
        AddText(titleGO, "창고", 36f, TextPrimary, TextAlignmentOptions.MidlineLeft);

        GameObject buttonGO = NewUIObject("CloseButton", headerGO.transform);
        PlaceTopRight(buttonGO, 0f, 0f, 44f, 44f);
        Image fill = AddImage(buttonGO, UIProceduralSpriteFactory.LoadFill(12), CloseButtonFill);
        AddStretchedImage(buttonGO, "Outline", UIProceduralSpriteFactory.LoadLine(12), AccentSoft);

        GameObject iconGO = NewUIObject("Icon", buttonGO.transform);
        PlaceCentered(iconGO, 0f, 0f, 18f, 18f);
        Image icon = AddImage(iconGO, UIProceduralSpriteFactory.LoadIcon("Icon_Close"), TextFaint);
        icon.raycastTarget = false;

        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = fill;
        button.transition = Selectable.Transition.None;

        return button;
    }

    // 좌측 카테고리 필터 패널을 만들고, 탭이 세로로 쌓일 부모를 반환한다.
    private static RectTransform BuildCategoryPanel(GameObject root)
    {
        GameObject panelGO = BuildPanelBox(root, "CategoryPanel", ScreenPadding, CategoryPanelWidth);

        GameObject labelGO = NewUIObject("Label", panelGO.transform);
        PlaceTopLeft(labelGO, PanelPadding, -PanelPadding, 188f, 18f);
        AddText(labelGO, "카테고리", 14f, TextPrimary, TextAlignmentOptions.MidlineLeft);

        GameObject tabsGO = NewUIObject("Tabs", panelGO.transform);
        PlaceTopLeft(tabsGO, PanelPadding, -46f, 188f, ContentHeight - 62f);

        var layout = tabsGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return tabsGO.GetComponent<RectTransform>();
    }

    // 가운데 아이템 목록 패널(제목·검색창·6열 격자)을 만든다.
    private static void BuildGridPanel(GameObject root, out TMP_InputField searchInput, out ScrollRect scrollRect, out RectTransform slotParent)
    {
        GameObject panelGO = BuildPanelBox(root, "GridPanel", GridPanelX, GridPanelWidth);
        float contentWidth = GridPanelWidth - PanelPadding * 2f;

        GameObject titleGO = NewUIObject("Title", panelGO.transform);
        PlaceTopLeft(titleGO, PanelPadding, -PanelPadding, 400f, 44f);
        AddText(titleGO, "아이템 목록", 18f, TextStrong, TextAlignmentOptions.MidlineLeft);

        searchInput = BuildSearchField(panelGO);

        GameObject scrollGO = NewUIObject("SlotScroll", panelGO.transform);
        PlaceTopLeft(scrollGO, PanelPadding, -76f, contentWidth, ContentHeight - 92f);
        scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;

        GameObject viewportGO = NewUIObject("Viewport", scrollGO.transform);
        RectTransform viewport = viewportGO.GetComponent<RectTransform>();
        StretchFull(viewport);
        viewportGO.AddComponent<RectMask2D>();

        GameObject contentGO = NewUIObject("Content", viewportGO.transform);
        RectTransform content = contentGO.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(SlotWidth, SlotHeight);
        grid.spacing = new Vector2(SlotSpacing, SlotSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = SlotColumnCount;
        grid.childAlignment = TextAnchor.UpperLeft;

        var fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;

        slotParent = content;
    }

    // 돋보기 아이콘과 자리표시자를 갖춘 아이템 검색 입력 필드를 만든다.
    private static TMP_InputField BuildSearchField(GameObject panel)
    {
        GameObject fieldGO = NewUIObject("SearchField", panel.transform);
        PlaceTopRight(fieldGO, -PanelPadding, -PanelPadding, 360f, 44f);
        Image fill = AddImage(fieldGO, UIProceduralSpriteFactory.LoadFill(14), BoxFill);
        AddStretchedImage(fieldGO, "Outline", UIProceduralSpriteFactory.LoadLine(14), PanelOutline);

        GameObject searchIconGO = NewUIObject("Icon", fieldGO.transform);
        PlaceLeftCentered(searchIconGO, 14f, 18f, 18f);
        AddImage(searchIconGO, UIProceduralSpriteFactory.LoadIcon("Icon_Search"), TextMuted);

        GameObject viewportGO = NewUIObject("TextArea", fieldGO.transform);
        RectTransform viewport = viewportGO.GetComponent<RectTransform>();
        StretchFull(viewport);
        viewport.offsetMin = new Vector2(42f, 4f);
        viewport.offsetMax = new Vector2(-14f, -4f);
        viewportGO.AddComponent<RectMask2D>();

        GameObject placeholderGO = NewUIObject("Placeholder", viewportGO.transform);
        StretchWithPadding(placeholderGO.GetComponent<RectTransform>(), 0f);
        TextMeshProUGUI placeholder = AddText(placeholderGO, "아이템 검색...", 14f, TextMuted, TextAlignmentOptions.MidlineLeft);

        GameObject textGO = NewUIObject("Text", viewportGO.transform);
        StretchWithPadding(textGO.GetComponent<RectTransform>(), 0f);
        TextMeshProUGUI text = AddText(textGO, string.Empty, 14f, TextPrimary, TextAlignmentOptions.MidlineLeft);
        text.richText = false;

        var input = fieldGO.AddComponent<TMP_InputField>();
        input.targetGraphic = fill;
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.caretColor = TextPrimary;
        input.customCaretColor = true;
        input.selectionColor = new Color(0.18f, 0.42f, 1f, 0.4f);
        input.lineType = TMP_InputField.LineType.SingleLine;
        // 한글 IME 조합 중인 글자에 밑줄이 그어지지 않도록 리치 텍스트를 끈다
        input.richText = false;

        return input;
    }

    // 우측 상세 패널(미리보기 박스·이름·설명·수량)을 만든다.
    private static void BuildDetailPanel(GameObject root, out Image detailIcon, out Image detailPlaceholder, out RectTransform detailInfo,
        out TextMeshProUGUI detailName, out TextMeshProUGUI detailDescription,
        out RectTransform detailQuantity, out TextMeshProUGUI detailQuantityText)
    {
        GameObject panelGO = BuildPanelBox(root, "DetailPanel", DetailPanelX, DetailPanelWidth);
        float contentWidth = DetailPanelWidth - PanelPadding * 2f;

        GameObject titleGO = NewUIObject("Title", panelGO.transform);
        PlaceTopLeft(titleGO, PanelPadding, -PanelPadding, contentWidth, 23f);
        AddText(titleGO, "선택된 아이템", 18f, TextStrong, TextAlignmentOptions.MidlineLeft);

        GameObject previewGO = NewUIObject("Preview", panelGO.transform);
        PlaceTopLeft(previewGO, PanelPadding, -55f, contentWidth, 220f);
        AddImage(previewGO, UIProceduralSpriteFactory.LoadFill(16), BoxFill);
        AddStretchedImage(previewGO, "Outline", UIProceduralSpriteFactory.LoadLine(16), PanelOutline);

        GameObject previewInnerGO = NewUIObject("IconArea", previewGO.transform);
        StretchWithPadding(previewInnerGO.GetComponent<RectTransform>(), 2f);
        AddImage(previewInnerGO, UIProceduralSpriteFactory.LoadFill(12), InnerFill);

        GameObject iconGO = NewUIObject("Icon", previewInnerGO.transform);
        StretchWithPadding(iconGO.GetComponent<RectTransform>(), DetailIconPadding);
        detailIcon = AddImage(iconGO, null, TextPrimary);
        detailIcon.preserveAspect = true;
        detailIcon.raycastTarget = false;

        GameObject placeholderGO = NewUIObject("PlaceholderIcon", previewInnerGO.transform);
        PlaceCentered(placeholderGO, 0f, 0f, 72f, 72f);
        detailPlaceholder = AddImage(placeholderGO, UIProceduralSpriteFactory.LoadIcon("Icon_CircleX"), PlaceholderIcon);
        detailPlaceholder.raycastTarget = false;

        GameObject infoGO = NewUIObject("Info", panelGO.transform);
        PlaceTopLeft(infoGO, PanelPadding, -291f, contentWidth, 110f);
        detailInfo = infoGO.GetComponent<RectTransform>();

        GameObject nameGO = NewUIObject("Name", infoGO.transform);
        PlaceTopLeft(nameGO, 0f, 0f, contentWidth, 50f);
        detailName = AddText(nameGO, "아이템 이름", 40f, TextPrimary, TextAlignmentOptions.TopLeft);

        GameObject descriptionGO = NewUIObject("Description", infoGO.transform);
        PlaceTopLeft(descriptionGO, 0f, -60f, contentWidth, 50f);
        detailDescription = AddText(descriptionGO, "아이템 설명", 20f, TextMuted, TextAlignmentOptions.TopLeft);

        GameObject quantityGO = NewUIObject("Quantity", panelGO.transform);
        PlaceTopLeft(quantityGO, PanelPadding, -451f, contentWidth, 45f);
        detailQuantity = quantityGO.GetComponent<RectTransform>();

        GameObject quantityLabelGO = NewUIObject("Label", quantityGO.transform);
        PlaceTopLeft(quantityLabelGO, 0f, 0f, 160f, 45f);
        AddText(quantityLabelGO, "수량", 36f, TextStrong, TextAlignmentOptions.MidlineLeft);

        GameObject quantityValueGO = NewUIObject("Value", quantityGO.transform);
        PlaceTopRight(quantityValueGO, 0f, 0f, 160f, 45f);
        detailQuantityText = AddText(quantityValueGO, "x0", 36f, new Color32(0x16, 0xA3, 0x4A, 0xFF), TextAlignmentOptions.MidlineRight);
    }

    // 저장 공간 사용량 문구와 게이지가 들어가는 하단 바를 만든다.
    private static void BuildBottomBar(GameObject root, out TextMeshProUGUI capacityText, out RectTransform capacityFill)
    {
        GameObject barGO = NewUIObject("BottomBar", root.transform);
        PlaceTopLeft(barGO, ScreenPadding, BottomBarTop, BottomBarWidth, BottomBarHeight);
        AddImage(barGO, UIProceduralSpriteFactory.LoadFill(20), PanelFill);
        AddStretchedImage(barGO, "Outline", UIProceduralSpriteFactory.LoadLine(20), PanelOutline);

        GameObject labelGO = NewUIObject("Label", barGO.transform);
        PlaceTopLeft(labelGO, PanelPadding, -3f, 300f, 30f);
        AddText(labelGO, "저장 공간", 24f, TextStrong, TextAlignmentOptions.MidlineLeft);

        GameObject valueGO = NewUIObject("Value", barGO.transform);
        PlaceTopLeft(valueGO, PanelPadding, -39f, 300f, 30f);
        capacityText = AddText(valueGO, "0/30", 24f, TextMuted, TextAlignmentOptions.MidlineLeft);

        GameObject trackGO = NewUIObject("ProgressTrack", barGO.transform);
        PlaceTopLeft(trackGO, 1320f, -30f, 520f, 12f);
        AddImage(trackGO, UIProceduralSpriteFactory.LoadFill(6), BoxFill);

        GameObject fillGO = NewUIObject("ProgressFill", trackGO.transform);
        capacityFill = fillGO.GetComponent<RectTransform>();
        capacityFill.anchorMin = Vector2.zero;
        capacityFill.anchorMax = new Vector2(0.4f, 1f);
        capacityFill.offsetMin = Vector2.zero;
        capacityFill.offsetMax = Vector2.zero;
        AddImage(fillGO, UIProceduralSpriteFactory.LoadFill(6), Accent);
    }

    // 본문 3분할에 공통으로 쓰이는 둥근 패널 박스를 만든다.
    private static GameObject BuildPanelBox(GameObject root, string name, float x, float width)
    {
        GameObject panelGO = NewUIObject(name, root.transform);
        PlaceTopLeft(panelGO, x, ContentTop, width, ContentHeight);
        AddImage(panelGO, UIProceduralSpriteFactory.LoadFill(20), PanelFill);
        AddStretchedImage(panelGO, "Outline", UIProceduralSpriteFactory.LoadLine(20), PanelOutline);
        return panelGO;
    }

    // ─────────────────────────────── 공통 헬퍼 ───────────────────────────────

    // RectTransform만 가진 빈 UI 오브젝트를 만든다.
    private static GameObject NewUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    // 오브젝트에 Image를 붙이고 스프라이트가 9-슬라이스면 Sliced로 설정한다.
    private static Image AddImage(GameObject go, Sprite sprite, Color color)
    {
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        return image;
    }

    // 부모를 가득 채우는 자식 Image(테두리 등)를 만든다. 클릭을 가로채지 않는다.
    private static Image AddStretchedImage(GameObject parent, string name, Sprite sprite, Color color)
    {
        GameObject go = NewUIObject(name, parent.transform);
        StretchFull(go.GetComponent<RectTransform>());
        Image image = AddImage(go, sprite, color);
        image.raycastTarget = false;
        return image;
    }

    // 오브젝트 자신에 TextMeshProUGUI를 붙인다.
    private static TextMeshProUGUI AddText(GameObject go, string content, float size, Color color, TextAlignmentOptions alignment)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (font != null) tmp.font = font;

        return tmp;
    }

    // RectTransform을 부모 전체로 늘린다.
    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // RectTransform을 부모 전체로 늘리되 사방에 여백을 준다.
    private static void StretchWithPadding(RectTransform rect, float padding)
    {
        StretchFull(rect);
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    // 부모 좌상단을 기준으로 위치·크기를 지정한다. (y는 아래로 갈수록 음수)
    private static void PlaceTopLeft(GameObject go, float x, float y, float width, float height)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, y);
    }

    // 부모 우상단을 기준으로 위치·크기를 지정한다.
    private static void PlaceTopRight(GameObject go, float x, float y, float width, float height)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, y);
    }

    // 부모 왼쪽 세로 중앙을 기준으로 아이콘 위치를 지정한다.
    private static void PlaceLeftCentered(GameObject go, float x, float width, float height)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, 0f);
    }

    // 부모 중앙을 기준으로 위치·크기를 지정한다.
    private static void PlaceCentered(GameObject go, float x, float y, float width, float height)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, y);
    }

    // SerializedObject의 Color 프로퍼티를 설정한다.
    private static void SetColor(SerializedObject so, string propertyPath, Color color)
    {
        so.FindProperty(propertyPath).colorValue = color;
    }

    // 계층을 프리팹 에셋으로 저장하고 임시 인스턴스를 제거한다.
    private static GameObject SaveAsPrefab(GameObject root, string path)
    {
        ItemCodexUIPrefabTool.EnsureAssetFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }
}
