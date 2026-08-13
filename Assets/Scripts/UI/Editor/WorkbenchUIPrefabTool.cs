using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Figma의 'inventory-crafting-ui' 프레임을 프리팹(대화상자·창고 슬롯·조합 슬롯)으로 만들어 내는 에디터 도구.
// 좌표와 색은 1920x1080 프레임에서 잰 값을 그대로 옮겼다.
public static class WorkbenchUIPrefabTool
{
    public const string PanelPrefabPath = "Assets/Prefabs/UI/Inventory/Workbench UI.prefab";
    public const string StorageSlotPrefabPath = "Assets/Prefabs/UI/Workbench/WorkbenchStorageSlot.prefab";
    public const string CraftingSlotPrefabPath = "Assets/Prefabs/UI/Workbench/WorkbenchCraftingSlot.prefab";

    private const string FontAssetPath = "Assets/UI/Fonts/Test/BMHANNA_11YRS_OTF SDF.asset";

    // 대화상자 (화면 좌상단 기준)
    private const float DialogX = 100f;
    private const float DialogY = -84f;
    private const float DialogWidth = 1720f;
    private const float DialogHeight = 912f;
    private const float DialogBorder = 4f;

    // 헤더
    private const float HeaderX = 40f;
    private const float HeaderY = -28f;
    private const float HeaderWidth = 1648f;
    private const float HeaderHeight = 44f;

    // 좌우 패널 (대화상자 기준)
    private const float PanelY = -96f;
    private const float PanelWidth = 812f;
    private const float PanelHeight = 788f;
    private const float StoragePanelX = 32f;
    private const float CraftPanelX = 876f;
    private const float PanelPadding = 24f;

    // 창고 격자 — 6열 80px 칸에 간격 12
    private const float StorageSlotSize = 80f;
    private const float StorageSlotSpacing = 12f;
    private const int StorageColumnCount = 6;

    // 조합 격자 — 5열 80px 칸에 간격 10, 12px 안쪽 여백을 둔 상자 안에 놓인다
    private const float CraftingBoxX = 38f;
    private const float CraftingBoxY = -113f;
    private const float CraftingBoxSize = 464f;
    private const float CraftingBoxPadding = 12f;
    private const float CraftingSlotSize = 80f;
    private const float CraftingSlotSpacing = 10f;
    private const int CraftingColumnCount = 5;

    // 결과 슬롯과 화살표 (제작대 패널 기준)
    private const float ArrowX = 536f;
    private const float ArrowY = -331f;
    private const float ResultSlotX = 598f;
    private const float ResultSlotY = -259f;
    private const float ResultSlotSize = 172f;

    // 제작대 하단 (제작대 패널 기준)
    private const float ProgressRowY = -640f;
    private const float ProgressRowHeight = 44f;
    private const float CraftButtonY = -704f;
    private const float CraftButtonHeight = 60f;
    private const float BottomContentWidth = PanelWidth - PanelPadding * 2f;

    // 색상 팔레트
    private static readonly Color DialogOutline = new Color32(0x29, 0x12, 0x8D, 0xFF);
    private static readonly Color PanelFill = new Color32(0x05, 0x05, 0x05, 0xFF);
    private static readonly Color PanelOutline = new Color32(0x1E, 0x15, 0x45, 0xFF);
    private static readonly Color SlotOutline = new Color32(0x3B, 0x2E, 0x6F, 0xFF);
    private static readonly Color TrackFill = new Color32(0x02, 0x01, 0x08, 0xFF);
    private static readonly Color Accent = new Color32(0xC2, 0x41, 0x0C, 0xFF);
    private static readonly Color TitleIcon = new Color32(0xEF, 0x44, 0x44, 0xFF);
    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color TextPrimary = Color.white;
    private static readonly Color TextMuted = new Color32(0xA1, 0xA1, 0xAB, 0xFF);
    private static readonly Color TextFaint = new Color32(0xD4, 0xD4, 0xD8, 0xFF);
    private static readonly Color CloseButtonFill = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color WarningColor = new Color32(0xEF, 0x44, 0x44, 0xFF);

    [MenuItem("Tools/HistoricScience/Generate Workbench UI Prefabs")]
    public static void Generate()
    {
        UIProceduralSpriteFactory.GenerateAll();

        GameObject storageSlotPrefab = BuildStorageSlotPrefab();
        GameObject craftingSlotPrefab = BuildCraftingSlotPrefab();
        GameObject panelPrefab = BuildPanelPrefab(storageSlotPrefab, craftingSlotPrefab);

        RelinkWorkbenchReferences(panelPrefab.GetComponent<WorkbenchUI>());

        AssetDatabase.SaveAssets();
        Debug.Log($"[WorkbenchUIPrefabTool] '{AssetDatabase.GetAssetPath(panelPrefab)}' 작업대 UI 프리팹을 생성했습니다.");
    }

    // ─────────────────────────────── 슬롯 프리팹 ───────────────────────────────

    // 좌측 창고 격자의 한 칸을 만든다. 빈 칸일 때는 아이콘과 수량이 꺼진다.
    private static GameObject BuildStorageSlotPrefab()
    {
        GameObject root = NewUIObject("WorkbenchStorageSlot", null);
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(StorageSlotSize, StorageSlotSize);

        AddImage(root, UIProceduralSpriteFactory.LoadFill(12), PanelFill);
        AddStretchedImage(root, "Outline", UIProceduralSpriteFactory.LoadLine(12), SlotOutline);

        GameObject iconGO = NewUIObject("Icon", root.transform);
        StretchWithPadding(iconGO.GetComponent<RectTransform>(), 4f);
        Image icon = AddImage(iconGO, null, TextPrimary);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        GameObject countGO = NewUIObject("Count", root.transform);
        PlaceBottomRight(countGO, -8f, 6f, 46f, 20f);
        TextMeshProUGUI countText = AddText(countGO, "0", 18f, TextPrimary, TextAlignmentOptions.MidlineRight);

        var slot = root.AddComponent<ItemSlotUI>();
        var so = new SerializedObject(slot);
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_countText").objectReferenceValue = countText;
        so.ApplyModifiedPropertiesWithoutUndo();

        return SaveAsPrefab(root, StorageSlotPrefabPath);
    }

    // 우측 조합 격자의 한 칸을 만든다. 격자 좌표는 배치할 때 칸마다 따로 넣는다.
    private static GameObject BuildCraftingSlotPrefab()
    {
        GameObject root = NewUIObject("WorkbenchCraftingSlot", null);
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(CraftingSlotSize, CraftingSlotSize);

        AddImage(root, UIProceduralSpriteFactory.LoadFill(12), PanelFill);
        AddStretchedImage(root, "Outline", UIProceduralSpriteFactory.LoadLine(12), SlotOutline);

        GameObject iconGO = NewUIObject("Icon", root.transform);
        StretchWithPadding(iconGO.GetComponent<RectTransform>(), 4f);
        Image icon = AddImage(iconGO, null, TextPrimary);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var slot = root.AddComponent<CraftingSlotUI>();
        var so = new SerializedObject(slot);
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.ApplyModifiedPropertiesWithoutUndo();

        return SaveAsPrefab(root, CraftingSlotPrefabPath);
    }

    // ─────────────────────────────── 대화상자 프리팹 ───────────────────────────────

    // 작업대 UI 전체(딤 + 대화상자 + 창고/제작대 패널) 프리팹을 만든다.
    private static GameObject BuildPanelPrefab(GameObject storageSlotPrefab, GameObject craftingSlotPrefab)
    {
        GameObject root = NewUIObject("Workbench UI", null);
        StretchFull(root.GetComponent<RectTransform>());

        // 뒤 화면을 가리는 딤 — 클릭이 뒤로 새지 않도록 raycastTarget을 켜 둔다
        GameObject dimGO = NewUIObject("Dim", root.transform);
        StretchFull(dimGO.GetComponent<RectTransform>());
        AddImage(dimGO, null, Dim);

        // 대화상자 자체를 테두리 색으로 칠하고 안쪽을 덮어 4px 외곽선을 만든다
        GameObject dialogGO = NewUIObject("Dialog", root.transform);
        PlaceTopLeft(dialogGO, DialogX, DialogY, DialogWidth, DialogHeight);
        AddImage(dialogGO, UIProceduralSpriteFactory.LoadFill(24), DialogOutline);

        GameObject innerGO = NewUIObject("Inner", dialogGO.transform);
        StretchWithPadding(innerGO.GetComponent<RectTransform>(), DialogBorder);
        AddImage(innerGO, UIProceduralSpriteFactory.LoadFill(20), PanelFill);

        BuildHeader(dialogGO, out Button closeButton, out GameObject workerBadge);
        BuildStoragePanel(dialogGO, out RectTransform slotsContent);
        BuildCraftPanel(dialogGO, craftingSlotPrefab,
            out RectTransform craftingGrid, out Image resultIcon, out Button craftButton,
            out TextMeshProUGUI warningText, out GameObject progressRow);

        var workbenchUI = root.AddComponent<WorkbenchUI>();
        var so = new SerializedObject(workbenchUI);
        so.FindProperty("_slotPrefab").objectReferenceValue = storageSlotPrefab.GetComponent<ItemSlotUI>();
        so.FindProperty("_slotsContent").objectReferenceValue = slotsContent;
        so.FindProperty("_craftingGrid").objectReferenceValue = craftingGrid;
        so.FindProperty("_resultIcon").objectReferenceValue = resultIcon;
        so.FindProperty("_craftButton").objectReferenceValue = craftButton;
        so.FindProperty("_warningText").objectReferenceValue = warningText;
        so.FindProperty("_closeButton").objectReferenceValue = closeButton;
        so.FindProperty("_workerBadge").objectReferenceValue = workerBadge;
        so.FindProperty("_progressRow").objectReferenceValue = progressRow;
        so.ApplyModifiedPropertiesWithoutUndo();

        return SaveAsPrefab(root, PanelPrefabPath);
    }

    // 모루 아이콘·제목·시민 할당 배지·닫기 버튼으로 이루어진 헤더를 만든다.
    private static void BuildHeader(GameObject dialog, out Button closeButton, out GameObject workerBadge)
    {
        GameObject headerGO = NewUIObject("Header", dialog.transform);
        PlaceTopLeft(headerGO, HeaderX, HeaderY, HeaderWidth, HeaderHeight);

        GameObject iconGO = NewUIObject("Icon", headerGO.transform);
        PlaceLeftCentered(iconGO, 0f, 28f, 28f);
        AddImage(iconGO, UIProceduralSpriteFactory.LoadIcon("Icon_Anvil"), TitleIcon).raycastTarget = false;

        GameObject titleGO = NewUIObject("Title", headerGO.transform);
        PlaceTopLeft(titleGO, 47f, 0f, 700f, HeaderHeight);
        AddText(titleGO, "대장간 & 제작 시스템", 36f, TextPrimary, TextAlignmentOptions.MidlineLeft);

        workerBadge = BuildWorkerBadge(headerGO);
        closeButton = BuildCloseButton(headerGO);
    }

    // 우상단 '시민 N/M 할당됨' 알약 배지를 만든다. 값을 채울 데이터 소스는 아직 없다.
    private static GameObject BuildWorkerBadge(GameObject header)
    {
        GameObject badgeGO = NewUIObject("WorkerBadge", header.transform);
        PlaceTopRight(badgeGO, -68f, -4f, 158f, 40f);
        AddImage(badgeGO, UIProceduralSpriteFactory.LoadFill(20), PanelFill);
        AddStretchedImage(badgeGO, "Outline", UIProceduralSpriteFactory.LoadLine(20), SlotOutline);

        GameObject iconGO = NewUIObject("Icon", badgeGO.transform);
        PlaceLeftCentered(iconGO, 14f, 18f, 18f);
        AddImage(iconGO, UIProceduralSpriteFactory.LoadIcon("Icon_Users"), TextMuted).raycastTarget = false;

        GameObject labelGO = NewUIObject("Label", badgeGO.transform);
        PlaceTopLeft(labelGO, 38f, 0f, 108f, 40f);
        AddText(labelGO, "시민 0/0 할당됨", 18f, TextMuted, TextAlignmentOptions.MidlineLeft);

        return badgeGO;
    }

    // 우상단 닫기(X) 버튼을 만든다.
    private static Button BuildCloseButton(GameObject header)
    {
        GameObject buttonGO = NewUIObject("CloseButton", header.transform);
        PlaceTopRight(buttonGO, 0f, 0f, 44f, 44f);
        Image fill = AddImage(buttonGO, UIProceduralSpriteFactory.LoadFill(12), CloseButtonFill);
        AddStretchedImage(buttonGO, "Outline", UIProceduralSpriteFactory.LoadLine(12), SlotOutline);

        GameObject iconGO = NewUIObject("Icon", buttonGO.transform);
        PlaceCentered(iconGO, 0f, 0f, 18f, 18f);
        AddImage(iconGO, UIProceduralSpriteFactory.LoadIcon("Icon_Close"), TextPrimary).raycastTarget = false;

        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = fill;
        button.transition = Selectable.Transition.None;
        return button;
    }

    // 좌측 창고 패널(제목 + 스크롤되는 6열 격자)을 만든다.
    private static void BuildStoragePanel(GameObject dialog, out RectTransform slotsContent)
    {
        GameObject panelGO = BuildPanelBox(dialog, "StoragePanel", StoragePanelX);

        GameObject labelGO = NewUIObject("Label", panelGO.transform);
        PlaceTopLeft(labelGO, PanelPadding, -20f, 300f, 28f);
        AddText(labelGO, "창고", 26f, TextPrimary, TextAlignmentOptions.MidlineLeft);

        GameObject scrollGO = NewUIObject("SlotScroll", panelGO.transform);
        PlaceTopLeft(scrollGO, PanelPadding, -64f, BottomContentWidth, PanelHeight - 64f - PanelPadding);
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
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
        grid.cellSize = new Vector2(StorageSlotSize, StorageSlotSize);
        grid.spacing = new Vector2(StorageSlotSpacing, StorageSlotSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = StorageColumnCount;
        grid.childAlignment = TextAnchor.UpperLeft;

        var fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;

        slotsContent = content;
    }

    // 우측 제작대 패널(조합 격자 + 결과 슬롯 + 진행 게이지 + 제작 버튼)을 만든다.
    private static void BuildCraftPanel(GameObject dialog, GameObject craftingSlotPrefab,
        out RectTransform craftingGrid, out Image resultIcon, out Button craftButton,
        out TextMeshProUGUI warningText, out GameObject progressRow)
    {
        GameObject panelGO = BuildPanelBox(dialog, "CraftPanel", CraftPanelX);

        GameObject labelGO = NewUIObject("Label", panelGO.transform);
        PlaceTopLeft(labelGO, PanelPadding, -20f, 300f, 28f);
        AddText(labelGO, "제작대", 26f, TextPrimary, TextAlignmentOptions.MidlineLeft);

        craftingGrid = BuildCraftingGrid(panelGO, craftingSlotPrefab);

        GameObject arrowGO = NewUIObject("Arrow", panelGO.transform);
        PlaceTopLeft(arrowGO, ArrowX, ArrowY, 26f, 28f);
        AddImage(arrowGO, UIProceduralSpriteFactory.LoadIcon("Icon_ArrowRight"), Accent).raycastTarget = false;

        resultIcon = BuildResultSlot(panelGO);
        progressRow = BuildProgressRow(panelGO);

        // 경고 문구는 진행 게이지와 같은 자리를 쓴다. 게이지가 꺼져 있는 동안 비는 공간이다.
        GameObject warningGO = NewUIObject("WarningText", panelGO.transform);
        PlaceTopLeft(warningGO, PanelPadding, ProgressRowY, BottomContentWidth, ProgressRowHeight);
        warningText = AddText(warningGO, "조합법이 없습니다.", 24f, WarningColor, TextAlignmentOptions.Center);

        craftButton = BuildCraftButton(panelGO);
    }

    // 5x5 조합 격자 상자를 만들고 칸마다 격자 좌표를 넣는다.
    private static RectTransform BuildCraftingGrid(GameObject panel, GameObject craftingSlotPrefab)
    {
        GameObject boxGO = NewUIObject("CraftingBox", panel.transform);
        PlaceTopLeft(boxGO, CraftingBoxX, CraftingBoxY, CraftingBoxSize, CraftingBoxSize);
        AddImage(boxGO, UIProceduralSpriteFactory.LoadFill(16), PanelFill);
        AddStretchedImage(boxGO, "Outline", UIProceduralSpriteFactory.LoadLine(16), PanelOutline);

        GameObject gridGO = NewUIObject("Grid", boxGO.transform);
        float inner = CraftingBoxSize - CraftingBoxPadding * 2f;
        PlaceTopLeft(gridGO, CraftingBoxPadding, -CraftingBoxPadding, inner, inner);

        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CraftingSlotSize, CraftingSlotSize);
        grid.spacing = new Vector2(CraftingSlotSpacing, CraftingSlotSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = CraftingColumnCount;
        grid.childAlignment = TextAnchor.UpperLeft;

        int slotCount = CraftingColumnCount * CraftingColumnCount;
        for (int i = 0; i < slotCount; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(craftingSlotPrefab, gridGO.transform);
            instance.name = $"Slot_{i}";

            var so = new SerializedObject(instance.GetComponent<CraftingSlotUI>());
            so.FindProperty("_coord").vector2IntValue = new Vector2Int(i % CraftingColumnCount, i / CraftingColumnCount);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return gridGO.GetComponent<RectTransform>();
    }

    // 주황빛 글로우를 두른 결과 미리보기 슬롯을 만들고 결과 아이콘 이미지를 반환한다.
    private static Image BuildResultSlot(GameObject panel)
    {
        GameObject slotGO = NewUIObject("ResultSlot", panel.transform);
        PlaceTopLeft(slotGO, ResultSlotX, ResultSlotY, ResultSlotSize, ResultSlotSize);

        // 바깥으로 갈수록 옅어지는 테두리를 겹쳐 번짐 효과를 흉내낸다
        AddGlowRing(slotGO, "Glow3", 16f, 0.10f);
        AddGlowRing(slotGO, "Glow2", 10f, 0.22f);
        AddGlowRing(slotGO, "Glow1", 5f, 0.40f);

        AddImage(slotGO, UIProceduralSpriteFactory.LoadFill(20), PanelFill);
        AddStretchedImage(slotGO, "Outline", UIProceduralSpriteFactory.LoadLine(20), Accent);
        // 디자인의 3px 테두리를 2px 선 두 겹으로 낸다
        StretchWithPadding(AddStretchedImage(slotGO, "OutlineInner", UIProceduralSpriteFactory.LoadLine(20), Accent).rectTransform, 2f);

        GameObject iconGO = NewUIObject("Icon", slotGO.transform);
        PlaceCentered(iconGO, 0f, 0f, 96f, 96f);
        Image icon = AddImage(iconGO, null, TextPrimary);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        return icon;
    }

    // 결과 슬롯 밖으로 퍼지는 반투명 테두리 한 겹을 만든다.
    private static void AddGlowRing(GameObject parent, string name, float spread, float alpha)
    {
        Image ring = AddStretchedImage(parent, name, UIProceduralSpriteFactory.LoadLine(20),
            new Color(Accent.r, Accent.g, Accent.b, alpha));
        StretchWithPadding(ring.rectTransform, -spread);
    }

    // '제작 진행 상태' 문구와 게이지가 들어가는 줄을 만든다. 제작 시간이 붙기 전까지는 런타임에서 꺼진다.
    private static GameObject BuildProgressRow(GameObject panel)
    {
        GameObject rowGO = NewUIObject("ProgressRow", panel.transform);
        PlaceTopLeft(rowGO, PanelPadding, ProgressRowY, BottomContentWidth, ProgressRowHeight);

        GameObject labelGO = NewUIObject("Label", rowGO.transform);
        PlaceTopLeft(labelGO, 0f, 0f, 300f, 24f);
        AddText(labelGO, "제작 진행 상태", 20f, TextFaint, TextAlignmentOptions.MidlineLeft);

        GameObject valueGO = NewUIObject("Value", rowGO.transform);
        PlaceTopRight(valueGO, 0f, 0f, 300f, 24f);
        AddText(valueGO, "0.0초 / 0.0초", 20f, Accent, TextAlignmentOptions.MidlineRight);

        GameObject trackGO = NewUIObject("Track", rowGO.transform);
        PlaceTopLeft(trackGO, 0f, -28f, BottomContentWidth, 16f);
        AddImage(trackGO, UIProceduralSpriteFactory.LoadFill(8), TrackFill);
        AddStretchedImage(trackGO, "Outline", UIProceduralSpriteFactory.LoadLine(8), PanelOutline);

        GameObject fillGO = NewUIObject("Fill", trackGO.transform);
        RectTransform fill = fillGO.GetComponent<RectTransform>();
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        AddImage(fillGO, UIProceduralSpriteFactory.LoadFill(8), Accent);

        return rowGO;
    }

    // 패널 하단을 가로지르는 '제작 시작' 버튼을 만든다.
    private static Button BuildCraftButton(GameObject panel)
    {
        GameObject buttonGO = NewUIObject("CraftButton", panel.transform);
        PlaceTopLeft(buttonGO, PanelPadding, CraftButtonY, BottomContentWidth, CraftButtonHeight);
        Image fill = AddImage(buttonGO, UIProceduralSpriteFactory.LoadFill(12), Accent);

        GameObject labelGO = NewUIObject("Label", buttonGO.transform);
        StretchFull(labelGO.GetComponent<RectTransform>());
        AddText(labelGO, "제작 시작", 30f, TextPrimary, TextAlignmentOptions.Center);

        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = fill;
        button.transition = Selectable.Transition.None;
        return button;
    }

    // 좌우 본문에 공통으로 쓰이는 둥근 패널 박스를 만든다.
    private static GameObject BuildPanelBox(GameObject dialog, string name, float x)
    {
        GameObject panelGO = NewUIObject(name, dialog.transform);
        PlaceTopLeft(panelGO, x, PanelY, PanelWidth, PanelHeight);
        AddImage(panelGO, UIProceduralSpriteFactory.LoadFill(20), PanelFill);
        AddStretchedImage(panelGO, "Outline", UIProceduralSpriteFactory.LoadLine(20), PanelOutline);
        return panelGO;
    }

    // ─────────────────────────────── 참조 복구 ───────────────────────────────

    // 프리팹을 새로 만들면 컴포넌트의 로컬 파일 ID가 바뀌어 이 UI를 여는 쪽의 참조가 끊긴다. 생성 직후 다시 이어 준다.
    private static void RelinkWorkbenchReferences(WorkbenchUI panel)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponent<Lab>() == null) continue;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            var so = new SerializedObject(contents.GetComponent<Lab>());
            so.FindProperty("_workbenchUiPrefab").objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);

            Debug.Log($"[WorkbenchUIPrefabTool] '{path}'의 작업대 UI 참조를 새 프리팹으로 다시 연결했습니다.");
        }
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

    // RectTransform을 부모 전체로 늘리되 사방에 여백을 준다. 음수를 주면 부모보다 커진다.
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

    // 부모 우하단을 기준으로 위치·크기를 지정한다.
    private static void PlaceBottomRight(GameObject go, float x, float y, float width, float height)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
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

    // 계층을 프리팹 에셋으로 저장하고 임시 인스턴스를 제거한다.
    private static GameObject SaveAsPrefab(GameObject root, string path)
    {
        ItemCodexUIPrefabTool.EnsureAssetFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }
}
