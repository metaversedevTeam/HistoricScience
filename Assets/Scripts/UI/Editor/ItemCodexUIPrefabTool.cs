using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Figma의 dogam-ui-game-style 도감 UI를 프리팹(패널·카드·탭)으로 만들어 내는 에디터 도구.
// 테스트 씬 생성은 ItemCodexUITestSceneTool이 담당한다.
public static class ItemCodexUIPrefabTool
{
    public const string PanelPrefabPath = "Assets/Prefabs/UI/ItemCodexUI.prefab";
    public const string EntryPrefabPath = "Assets/Prefabs/UI/ItemCodex/ItemCodexEntry.prefab";
    public const string TabPrefabPath = "Assets/Prefabs/UI/ItemCodex/ItemCodexAgeTab.prefab";

    private const string FontAssetPath = "Assets/UI/Fonts/Test/BMHANNA_11YRS_OTF SDF.asset";
    private const string ItemDataListPath = "Assets/Data/ScriptableObjects/자원/아이템 목록.asset";

    // 패널 레이아웃 기준값 (1920x1080 기준 해상도)
    private const float PanelWidth = 1000f;
    private const float PanelHeight = 856f;
    private const float PanelPadding = 28f;
    private const float ContentWidth = PanelWidth - PanelPadding * 2f;
    private const float CardWidth = 296f;
    private const float CardHeight = 274f;
    private const float CardSpacing = 28f;
    private const float CloseButtonSize = 46f;
    private const float CloseButtonGap = 8f;

    // 색상 팔레트
    private static readonly Color PanelFill = Hex(0x08, 0x0B, 0x14);
    private static readonly Color PanelOutline = Hex(0x2F, 0x6B, 0xFF);
    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color Accent = Hex(0xE2, 0x52, 0x1A);
    private static readonly Color AccentRed = Hex(0xE8, 0x41, 0x2B);
    private static readonly Color TextPrimary = Color.white;
    private static readonly Color TextMuted = Hex(0x8A, 0x93, 0xA6);
    private static readonly Color TextFaint = Hex(0x6C, 0x7A, 0x99);
    private static readonly Color FieldFill = Hex(0x0A, 0x10, 0x20);
    private static readonly Color FieldOutline = Hex(0x2B, 0x4A, 0x9E);
    private static readonly Color Divider = Hex(0x1E, 0x2A, 0x45);
    private static readonly Color FooterFill = Hex(0x07, 0x0A, 0x12);
    private static readonly Color TrackFill = Hex(0x05, 0x07, 0x0C);
    private static readonly Color ThumbFill = Hex(0x03, 0x05, 0x0A);

    [MenuItem("Tools/HistoricScience/Generate Item Codex UI Prefabs")]
    public static void Generate()
    {
        UIProceduralSpriteFactory.GenerateAll();

        GameObject tabPrefab = BuildTabPrefab();
        GameObject entryPrefab = BuildEntryPrefab();
        GameObject panelPrefab = BuildPanelPrefab(tabPrefab, entryPrefab);

        AssetDatabase.SaveAssets();
        Debug.Log($"[ItemCodexUIPrefabTool] '{AssetDatabase.GetAssetPath(panelPrefab)}' 도감 UI 프리팹을 생성했습니다.");
    }

    // ─────────────────────────────── 탭 프리팹 ───────────────────────────────

    // 시대 필터 알약 탭 프리팹을 만든다. 라벨 길이에 따라 가로 폭이 자동으로 늘어난다.
    private static GameObject BuildTabPrefab()
    {
        GameObject root = NewUIObject("ItemCodexAgeTab", null);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160f, 44f);

        Image fill = AddImage(root, UIProceduralSpriteFactory.LoadFill(20), Hex(0x0E, 0x15, 0x26));
        Image outline = AddStretchedImage(root, "Outline", UIProceduralSpriteFactory.LoadLine(20), Hex(0x2A, 0x35, 0x50));
        // 테두리는 라벨과 나란히 배치되면 안 되므로 레이아웃 계산에서 제외한다
        outline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        Button button = root.AddComponent<Button>();
        button.targetGraphic = fill;
        button.transition = Selectable.Transition.None;

        var layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(26, 26, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var element = root.AddComponent<LayoutElement>();
        element.minHeight = 44f;
        element.preferredHeight = 44f;

        TextMeshProUGUI label = AddText(root, "Label", "시대", 18f, Hex(0xC9, 0xD2, 0xE3), TextAlignmentOptions.Center);

        var tab = root.AddComponent<CodexAgeTabUI>();
        var so = new SerializedObject(tab);
        so.FindProperty("_button").objectReferenceValue = button;
        so.FindProperty("_fill").objectReferenceValue = fill;
        so.FindProperty("_outline").objectReferenceValue = outline;
        so.FindProperty("_label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();

        return SaveAsPrefab(root, TabPrefabPath);
    }

    // ─────────────────────────────── 카드 프리팹 ───────────────────────────────

    // 도감 격자에 놓이는 아이템 카드 프리팹을 만든다.
    private static GameObject BuildEntryPrefab()
    {
        GameObject root = NewUIObject("ItemCodexEntry", null);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);

        Image cardFill = AddImage(root, UIProceduralSpriteFactory.LoadFill(12), Hex(0x0C, 0x14, 0x24));
        Image cardOutline = AddStretchedImage(root, "Outline", UIProceduralSpriteFactory.LoadLine(12), Hex(0x8C, 0x3B, 0x1F));

        // 썸네일 영역
        GameObject thumbFrameGO = NewUIObject("ThumbnailFrame", root.transform);
        PlaceTopLeft(thumbFrameGO, 12f, -12f, 272f, 160f);
        AddImage(thumbFrameGO, UIProceduralSpriteFactory.LoadFill(8), ThumbFill);
        Image thumbOutline = AddStretchedImage(thumbFrameGO, "Outline", UIProceduralSpriteFactory.LoadLine(8), Hex(0x1B, 0x3A, 0x6B));

        GameObject thumbGO = NewUIObject("Thumbnail", thumbFrameGO.transform);
        StretchWithPadding(thumbGO.GetComponent<RectTransform>(), 26f);
        Image thumbnail = AddImage(thumbGO, null, Color.white);
        thumbnail.preserveAspect = true;
        thumbnail.raycastTarget = false;

        GameObject lockGO = NewUIObject("LockIcon", thumbFrameGO.transform);
        PlaceCentered(lockGO, 0f, 0f, 44f, 44f);
        Image lockIcon = AddImage(lockGO, UIProceduralSpriteFactory.LoadIcon("Icon_Lock"), Hex(0x4A, 0x54, 0x68));
        lockIcon.raycastTarget = false;

        // 번호 + 시대 배지
        GameObject indexGO = NewUIObject("IndexText", root.transform);
        PlaceTopLeft(indexGO, 12f, -180f, 160f, 22f);
        TextMeshProUGUI indexText = AddText(indexGO, "No. 001", 15f, Hex(0x9A, 0xA5, 0xBA), TextAlignmentOptions.MidlineLeft);

        GameObject badgeGO = NewUIObject("AgeBadge", root.transform);
        PlaceTopRight(badgeGO, -12f, -179f, 62f, 24f);
        Image badgeFill = AddImage(badgeGO, UIProceduralSpriteFactory.LoadFill(6), Hex(0x25, 0x63, 0xEB));
        TextMeshProUGUI badgeText = AddText(badgeGO, "Label", "구석기", 14f, Color.white, TextAlignmentOptions.Center);
        StretchWithPadding(badgeText.rectTransform, 0f);

        // 이름
        GameObject nameGO = NewUIObject("NameText", root.transform);
        PlaceTopLeft(nameGO, 12f, -206f, 272f, 30f);
        TextMeshProUGUI nameText = AddText(nameGO, "아이템 이름", 21f, TextPrimary, TextAlignmentOptions.MidlineLeft);
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        // 상태 바
        GameObject statusGO = NewUIObject("StatusBar", root.transform);
        PlaceTopLeft(statusGO, 12f, -238f, 272f, 26f);
        Image statusFill = AddImage(statusGO, UIProceduralSpriteFactory.LoadFill(8), Hex(0x07, 0x1C, 0x14));
        Image statusOutline = AddStretchedImage(statusGO, "Outline", UIProceduralSpriteFactory.LoadLine(8), Hex(0x16, 0xA3, 0x4A));

        GameObject statusIconGO = NewUIObject("Icon", statusGO.transform);
        PlaceCentered(statusIconGO, -44f, 0f, 16f, 16f);
        Image statusIcon = AddImage(statusIconGO, UIProceduralSpriteFactory.LoadIcon("Icon_Check"), Hex(0x22, 0xC5, 0x5E));

        GameObject statusTextGO = NewUIObject("Label", statusGO.transform);
        PlaceCentered(statusTextGO, 12f, 0f, 180f, 24f);
        TextMeshProUGUI statusText = AddText(statusTextGO, "수집 완료", 15f, Hex(0x22, 0xC5, 0x5E), TextAlignmentOptions.MidlineLeft);

        var entry = root.AddComponent<ItemCodexEntryUI>();
        var so = new SerializedObject(entry);
        so.FindProperty("_cardFill").objectReferenceValue = cardFill;
        so.FindProperty("_cardOutline").objectReferenceValue = cardOutline;
        so.FindProperty("_thumbnailOutline").objectReferenceValue = thumbOutline;
        so.FindProperty("_thumbnail").objectReferenceValue = thumbnail;
        so.FindProperty("_lockIcon").objectReferenceValue = lockIcon;
        so.FindProperty("_indexText").objectReferenceValue = indexText;
        so.FindProperty("_ageBadgeFill").objectReferenceValue = badgeFill;
        so.FindProperty("_ageBadgeText").objectReferenceValue = badgeText;
        so.FindProperty("_nameText").objectReferenceValue = nameText;
        so.FindProperty("_statusFill").objectReferenceValue = statusFill;
        so.FindProperty("_statusOutline").objectReferenceValue = statusOutline;
        so.FindProperty("_statusIcon").objectReferenceValue = statusIcon;
        so.FindProperty("_statusText").objectReferenceValue = statusText;
        so.FindProperty("_discoveredIcon").objectReferenceValue = UIProceduralSpriteFactory.LoadIcon("Icon_Check");
        so.FindProperty("_undiscoveredIcon").objectReferenceValue = UIProceduralSpriteFactory.LoadIcon("Icon_Clock");
        so.ApplyModifiedPropertiesWithoutUndo();

        return SaveAsPrefab(root, EntryPrefabPath);
    }

    // ─────────────────────────────── 패널 프리팹 ───────────────────────────────

    // 도감 패널 전체(어두운 배경 + 중앙 패널) 프리팹을 만든다.
    private static GameObject BuildPanelPrefab(GameObject tabPrefab, GameObject entryPrefab)
    {
        GameObject root = NewUIObject("ItemCodexUI", null);
        StretchFull(root.GetComponent<RectTransform>());

        // 뒤 화면을 가리는 딤 — 클릭이 뒤로 새지 않도록 raycastTarget을 켜 둔다
        GameObject dimGO = NewUIObject("Dim", root.transform);
        StretchFull(dimGO.GetComponent<RectTransform>());
        AddImage(dimGO, null, Dim);

        GameObject panelGO = NewUIObject("Panel", root.transform);
        PlaceCentered(panelGO, 0f, 0f, PanelWidth, PanelHeight);
        AddImage(panelGO, UIProceduralSpriteFactory.LoadFill(16), PanelFill);
        AddStretchedImage(panelGO, "Outline", UIProceduralSpriteFactory.LoadLine(16), PanelOutline);

        BuildHeader(panelGO, out TMP_InputField searchInput, out Button closeButton);
        RectTransform tabParent = BuildTabRow(panelGO);
        BuildDivider(panelGO);
        BuildGrid(panelGO, out ScrollRect scrollRect, out RectTransform entryParent, out TextMeshProUGUI emptyText);
        BuildFooter(panelGO, out RectTransform progressFill, out TextMeshProUGUI progressText);

        var codexUI = root.AddComponent<ItemCodexUI>();
        var so = new SerializedObject(codexUI);
        so.FindProperty("_itemDataList").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<ItemDataList>(ItemDataListPath);
        so.FindProperty("_tabPrefab").objectReferenceValue = tabPrefab.GetComponent<CodexAgeTabUI>();
        so.FindProperty("_tabParent").objectReferenceValue = tabParent;
        so.FindProperty("_entryPrefab").objectReferenceValue = entryPrefab.GetComponent<ItemCodexEntryUI>();
        so.FindProperty("_entryParent").objectReferenceValue = entryParent;
        so.FindProperty("_scrollRect").objectReferenceValue = scrollRect;
        so.FindProperty("_emptyText").objectReferenceValue = emptyText;
        so.FindProperty("_searchInput").objectReferenceValue = searchInput;
        so.FindProperty("_closeButton").objectReferenceValue = closeButton;
        so.FindProperty("_progressFill").objectReferenceValue = progressFill;
        so.FindProperty("_progressText").objectReferenceValue = progressText;
        so.ApplyModifiedPropertiesWithoutUndo();

        return SaveAsPrefab(root, PanelPrefabPath);
    }

    // 책 아이콘, 제목, 부제목, 이름 검색창, 닫기 버튼으로 이루어진 패널 헤더를 만든다.
    private static void BuildHeader(GameObject panel, out TMP_InputField searchInput, out Button closeButton)
    {
        GameObject iconBox = NewUIObject("TitleIcon", panel.transform);
        PlaceTopLeft(iconBox, PanelPadding, -PanelPadding, 46f, 46f);
        AddImage(iconBox, UIProceduralSpriteFactory.LoadFill(10), Hex(0x1A, 0x0B, 0x0B));
        AddStretchedImage(iconBox, "Outline", UIProceduralSpriteFactory.LoadLine(10), AccentRed);

        GameObject bookGO = NewUIObject("Icon", iconBox.transform);
        PlaceCentered(bookGO, 0f, 0f, 26f, 26f);
        AddImage(bookGO, UIProceduralSpriteFactory.LoadIcon("Icon_Book"), AccentRed);

        GameObject titleGO = NewUIObject("Title", panel.transform);
        PlaceTopLeft(titleGO, 88f, -22f, 520f, 40f);
        AddText(titleGO, "인류 문명 도감", 34f, TextPrimary, TextAlignmentOptions.MidlineLeft);

        GameObject subtitleGO = NewUIObject("Subtitle", panel.transform);
        PlaceTopLeft(subtitleGO, 90f, -62f, 520f, 24f);
        AddText(subtitleGO, "획득한 유물과 기술의 기록을 감상하십시오.", 16f, TextMuted, TextAlignmentOptions.MidlineLeft);

        closeButton = BuildCloseButton(panel);
        searchInput = BuildSearchField(panel);
    }

    // 패널 우상단의 닫기(X) 버튼을 만든다.
    private static Button BuildCloseButton(GameObject panel)
    {
        GameObject buttonGO = NewUIObject("CloseButton", panel.transform);
        PlaceTopRight(buttonGO, -PanelPadding, -PanelPadding, CloseButtonSize, CloseButtonSize);

        // ColorTint가 색을 곱하므로, 실제 색은 Button의 상태 색으로 지정하고 이미지는 흰색으로 둔다
        Image fill = AddImage(buttonGO, UIProceduralSpriteFactory.LoadFill(10), Color.white);

        GameObject iconGO = NewUIObject("Icon", buttonGO.transform);
        PlaceCentered(iconGO, 0f, 0f, 20f, 20f);
        Image icon = AddImage(iconGO, UIProceduralSpriteFactory.LoadIcon("Icon_Close"), Hex(0xC9, 0xCE, 0xD8));
        icon.raycastTarget = false;

        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = fill;

        ColorBlock colors = button.colors;
        colors.normalColor = Hex(0x1B, 0x1F, 0x28);
        colors.highlightedColor = Hex(0x28, 0x2E, 0x3C);
        colors.pressedColor = Hex(0x11, 0x15, 0x1D);
        colors.selectedColor = Hex(0x1B, 0x1F, 0x28);
        colors.disabledColor = Hex(0x11, 0x15, 0x1D);
        button.colors = colors;

        return button;
    }

    // 돋보기 아이콘과 자리표시자를 갖춘 이름 검색 입력 필드를 만든다.
    private static TMP_InputField BuildSearchField(GameObject panel)
    {
        GameObject fieldGO = NewUIObject("SearchField", panel.transform);
        // 닫기 버튼 왼쪽에 붙여 배치한다
        PlaceTopRight(fieldGO, -(PanelPadding + CloseButtonSize + CloseButtonGap), -PanelPadding, 272f, 46f);
        Image fill = AddImage(fieldGO, UIProceduralSpriteFactory.LoadFill(20), FieldFill);
        AddStretchedImage(fieldGO, "Outline", UIProceduralSpriteFactory.LoadLine(20), FieldOutline);

        GameObject searchIconGO = NewUIObject("Icon", fieldGO.transform);
        PlaceLeftCentered(searchIconGO, 18f, 18f, 18f);
        AddImage(searchIconGO, UIProceduralSpriteFactory.LoadIcon("Icon_Search"), TextFaint);

        GameObject viewportGO = NewUIObject("TextArea", fieldGO.transform);
        RectTransform viewport = viewportGO.GetComponent<RectTransform>();
        StretchFull(viewport);
        viewport.offsetMin = new Vector2(46f, 4f);
        viewport.offsetMax = new Vector2(-16f, -4f);
        viewportGO.AddComponent<RectMask2D>();

        GameObject placeholderGO = NewUIObject("Placeholder", viewportGO.transform);
        StretchWithPadding(placeholderGO.GetComponent<RectTransform>(), 0f);
        TextMeshProUGUI placeholder = AddText(placeholderGO, "이름 검색...", 17f, TextFaint, TextAlignmentOptions.MidlineLeft);

        GameObject textGO = NewUIObject("Text", viewportGO.transform);
        StretchWithPadding(textGO.GetComponent<RectTransform>(), 0f);
        TextMeshProUGUI text = AddText(textGO, string.Empty, 17f, TextPrimary, TextAlignmentOptions.MidlineLeft);
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

        return input;
    }

    // 시대 필터 탭이 가로로 늘어설 행을 만든다.
    private static RectTransform BuildTabRow(GameObject panel)
    {
        GameObject rowGO = NewUIObject("AgeTabs", panel.transform);
        PlaceTopLeft(rowGO, PanelPadding, -112f, ContentWidth, 44f);

        var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return rowGO.GetComponent<RectTransform>();
    }

    // 탭 행과 카드 격자를 나누는 가로 구분선을 만든다.
    private static void BuildDivider(GameObject panel)
    {
        GameObject dividerGO = NewUIObject("Divider", panel.transform);
        PlaceTopLeft(dividerGO, PanelPadding, -178f, ContentWidth, 1f);
        AddImage(dividerGO, null, Divider);
    }

    // 세로 스크롤되는 3열 카드 격자와, 결과가 없을 때의 안내 문구를 만든다.
    private static void BuildGrid(GameObject panel, out ScrollRect scrollRect, out RectTransform entryParent, out TextMeshProUGUI emptyText)
    {
        const float GridTop = -196f;
        const float GridHeight = 520f;

        GameObject scrollGO = NewUIObject("EntryScroll", panel.transform);
        PlaceTopLeft(scrollGO, PanelPadding, GridTop, ContentWidth, GridHeight);
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
        content.sizeDelta = new Vector2(0f, 0f);

        var grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CardWidth, CardHeight);
        grid.spacing = new Vector2(CardSpacing, CardSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperLeft;

        var fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;

        GameObject emptyGO = NewUIObject("EmptyText", panel.transform);
        PlaceTopLeft(emptyGO, PanelPadding, GridTop, ContentWidth, GridHeight);
        emptyText = AddText(emptyGO, "검색 결과가 없습니다.", 20f, TextFaint, TextAlignmentOptions.Center);
        emptyGO.SetActive(false);

        entryParent = content;
    }

    // 별 아이콘, 달성도 문구, 진행 게이지로 이루어진 하단 요약 박스를 만든다.
    private static void BuildFooter(GameObject panel, out RectTransform progressFill, out TextMeshProUGUI progressText)
    {
        GameObject footerGO = NewUIObject("ProgressFooter", panel.transform);
        PlaceTopLeft(footerGO, PanelPadding, -736f, ContentWidth, 92f);
        AddImage(footerGO, UIProceduralSpriteFactory.LoadFill(12), FooterFill);
        AddStretchedImage(footerGO, "Outline", UIProceduralSpriteFactory.LoadLine(12), Divider);

        GameObject starGO = NewUIObject("StarIcon", footerGO.transform);
        PlaceTopLeft(starGO, 20f, -20f, 22f, 22f);
        AddImage(starGO, UIProceduralSpriteFactory.LoadIcon("Icon_Star"), Accent);

        GameObject labelGO = NewUIObject("Label", footerGO.transform);
        PlaceTopLeft(labelGO, 50f, -20f, 400f, 24f);
        AddText(labelGO, "도감 전체 달성도", 18f, TextPrimary, TextAlignmentOptions.MidlineLeft);

        GameObject statGO = NewUIObject("ProgressText", footerGO.transform);
        PlaceTopRight(statGO, -20f, -20f, 400f, 24f);
        progressText = AddText(statGO, "수집 완료: 0/0 (0%)", 18f, Accent, TextAlignmentOptions.MidlineRight);

        GameObject trackGO = NewUIObject("ProgressTrack", footerGO.transform);
        PlaceTopLeft(trackGO, 20f, -56f, ContentWidth - 40f, 12f);
        AddImage(trackGO, UIProceduralSpriteFactory.LoadFill(6), TrackFill);

        GameObject fillGO = NewUIObject("ProgressFill", trackGO.transform);
        progressFill = fillGO.GetComponent<RectTransform>();
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0.24f, 1f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
        AddImage(fillGO, UIProceduralSpriteFactory.LoadFill(6), Accent);
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

    // 부모 아래에 이름 붙은 자식을 만들고 거기에 TextMeshProUGUI를 붙인다.
    private static TextMeshProUGUI AddText(GameObject parent, string name, string content, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = NewUIObject(name, parent.transform);
        return AddText(go, content, size, color, alignment);
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

    // 부모 왼쪽 세로 중앙을 기준으로 정사각형 아이콘 위치를 지정한다.
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

    // 계층을 프리팹 에셋으로 저장하고 임시 인스턴스를 제거한다.
    private static GameObject SaveAsPrefab(GameObject root, string path)
    {
        EnsureAssetFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // "Assets/A/B" 형태의 폴더 경로를 단계별로 만들어 둔다.
    public static void EnsureAssetFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

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

    // 0~255 RGB 값을 Color로 바꾼다.
    private static Color Hex(byte r, byte g, byte b) => new Color32(r, g, b, 255);
}
