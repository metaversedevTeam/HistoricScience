using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Hologram/BuildingPlacementController 프리팹과, 그 결과를 눈으로 확인할 테스트 씬을 생성하는 에디터 도구
public static class BuildingPlacementPrefabTool
{
    private const string HologramMaterialPath = "Assets/Art/Materials/Hologram.mat";
    private const string HologramPrefabPath = "Assets/Prefabs/Gameplay/Hologram.prefab";
    private const string ControllerPrefabPath = "Assets/Prefabs/Gameplay/BuildingPlacementController.prefab";
    private const string BuildCostRowPrefabPath = "Assets/Prefabs/UI/BuildCostRowUI.prefab";
    private const string BuildCostUiPrefabPath = "Assets/Prefabs/UI/BuildCostUI.prefab";
    private const string CitizenPrefabPath = "Assets/Prefabs/Object/Citizen.prefab";
    private const string ChunkPrefabPath = "Assets/Prefabs/Environment/MapChunkTerrain.prefab";
    private const string SeaPrefabPath = "Assets/Prefabs/Environment/Sea.prefab";
    private const string CommandButtonPrefabPath = "Assets/Prefabs/UI/CommandButton.prefab";
    private const string ItemDataListPath = "Assets/Data/ScriptableObjects/자원/아이템 목록.asset";
    private const string TestScenePath = "Assets/ExternalAssets/Test/Scenes/BuildingPlacementTest.unity";
    private const float SeaLevelNormalizedHeight = 0.12f;

    [MenuItem("Tools/HistoricScience/Generate Building Placement Prefabs And Test Scene")]
    public static void Generate()
    {
        HandleEnsureFolders();

        Material hologramMaterial = CreateOrUpdateHologramMaterial();
        Hologram hologramPrefab = CreateOrUpdateHologramPrefab(hologramMaterial);
        BuildCostRowUI costRowPrefab = CreateOrUpdateBuildCostRowPrefab();
        BuildCostUI costUiPrefab = CreateOrUpdateBuildCostUiPrefab(costRowPrefab);
        BuildingPlacementController controllerPrefab = CreateOrUpdateControllerPrefab(hologramPrefab, costUiPrefab);
        WireCitizenPrefab(controllerPrefab);
        CreateTestScene();

        Debug.Log($"[BuildingPlacementPrefabTool] '{HologramPrefabPath}', '{BuildCostRowPrefabPath}', '{BuildCostUiPrefabPath}', '{ControllerPrefabPath}' 프리팹과 '{TestScenePath}' 테스트 씬을 생성했습니다.");
    }

    // 프리팹을 저장할 폴더가 없으면 생성한다.
    private static void HandleEnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Gameplay"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Gameplay");
    }

    // 반투명 URP Lit 셰이더로 홀로그램 재질을 만들거나 갱신한다.
    private static Material CreateOrUpdateHologramMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(HologramMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, HologramMaterialPath);
        }

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetColor("_BaseColor", new Color(0.2f, 0.5f, 1f, 0.5f));

        EditorUtility.SetDirty(material);
        return material;
    }

    // MeshFilter/MeshRenderer/Hologram 컴포넌트로 구성된 홀로그램 프리팹을 만들거나 갱신한다.
    private static Hologram CreateOrUpdateHologramPrefab(Material material)
    {
        var root = new GameObject("Hologram", typeof(MeshFilter), typeof(MeshRenderer));
        var meshRenderer = root.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

        var hologram = root.AddComponent<Hologram>();
        var so = new SerializedObject(hologram);
        so.FindProperty("_meshFilter").objectReferenceValue = root.GetComponent<MeshFilter>();
        so.FindProperty("_meshRenderer").objectReferenceValue = meshRenderer;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, HologramPrefabPath);
        Object.DestroyImmediate(root);

        return prefabAsset.GetComponent<Hologram>();
    }

    // BuildingPlacementController 컴포넌트를 홀로그램/건설 비용 UI/Ground 레이어와 연결한 프리팹을 만들거나 갱신한다.
    private static BuildingPlacementController CreateOrUpdateControllerPrefab(Hologram hologramPrefab, BuildCostUI costUiPrefab)
    {
        var root = new GameObject("BuildingPlacementController");
        var controller = root.AddComponent<BuildingPlacementController>();

        var so = new SerializedObject(controller);
        so.FindProperty("_hologramPrefab").objectReferenceValue = hologramPrefab;
        so.FindProperty("_buildCostUiPrefab").objectReferenceValue = costUiPrefab;
        so.FindProperty("_groundLayer").intValue = LayerMask.GetMask("Ground");
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, ControllerPrefabPath);
        Object.DestroyImmediate(root);

        return prefabAsset.GetComponent<BuildingPlacementController>();
    }

    // 자원 아이콘 + 보유/필요 수량 텍스트로 구성된 건설 비용 행 프리팹을 만들거나 갱신한다.
    private static BuildCostRowUI CreateOrUpdateBuildCostRowPrefab()
    {
        var root = new GameObject("BuildCostRowUI", typeof(RectTransform), typeof(VerticalLayoutGroup));
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(70f, 90f);

        var layout = root.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.spacing = 4f;

        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(root.transform, false);
        var iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(48f, 48f);
        var icon = iconGO.GetComponent<Image>();

        var textGO = new GameObject("CountText", typeof(RectTransform));
        textGO.transform.SetParent(root.transform, false);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(70f, 30f);
        var countText = textGO.AddComponent<TextMeshProUGUI>();
        countText.alignment = TextAlignmentOptions.Center;
        countText.fontSize = 24f;
        countText.text = "0 / 0";

        var row = root.AddComponent<BuildCostRowUI>();
        var so = new SerializedObject(row);
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_countText").objectReferenceValue = countText;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, BuildCostRowPrefabPath);
        Object.DestroyImmediate(root);

        return prefabAsset.GetComponent<BuildCostRowUI>();
    }

    // 건설 비용 행들을 가로로 나열해 보여주는 건설 비용 UI 프리팹을 만들거나 갱신한다. UIManager의 공용 Canvas 아래 배치되므로 자체 Canvas는 두지 않는다.
    private static BuildCostUI CreateOrUpdateBuildCostUiPrefab(BuildCostRowUI rowPrefab)
    {
        var root = new GameObject("BuildCostUI", typeof(RectTransform), typeof(Image));
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.sizeDelta = new Vector2(600f, 110f);
        rootRect.anchoredPosition = new Vector2(0f, 240f);

        var background = root.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.5f);

        var containerGO = new GameObject("RowContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        containerGO.transform.SetParent(root.transform, false);
        var containerRect = containerGO.GetComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        var layout = containerGO.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var costUi = root.AddComponent<BuildCostUI>();
        var so = new SerializedObject(costUi);
        so.FindProperty("_rowPrefab").objectReferenceValue = rowPrefab;
        so.FindProperty("_rowContainer").objectReferenceValue = containerRect;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, BuildCostUiPrefabPath);
        Object.DestroyImmediate(root);

        return prefabAsset.GetComponent<BuildCostUI>();
    }

    // Citizen 프리팹의 위치 지정 컨트롤러 필드가 비어 있으면 새로 만든 프리팹을 연결한다.
    private static void WireCitizenPrefab(BuildingPlacementController controllerPrefab)
    {
        using var editScope = new PrefabUtility.EditPrefabContentsScope(CitizenPrefabPath);
        Citizen citizen = editScope.prefabContentsRoot.GetComponent<Citizen>();
        if (citizen == null)
        {
            Debug.LogWarning($"[BuildingPlacementPrefabTool] '{CitizenPrefabPath}'에서 Citizen 컴포넌트를 찾지 못했습니다.");
            return;
        }

        var so = new SerializedObject(citizen);
        SerializedProperty prop = so.FindProperty("_buildingPlacementControllerPrefab");
        if (prop.objectReferenceValue != null)
            return;

        prop.objectReferenceValue = controllerPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 지형·카메라·커맨드 패널·시민이 배치된 확인용 씬을 만든다. 기존에 열려 있던 씬을 대체하므로 저장 후 호출해야 한다.
    private static void CreateTestScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        HandleCreateLight();
        Terrain terrain = HandleCreateTerrain();
        HandleCreateSea(terrain);
        HandleBakeNavMesh();

        PlayerManager playerManager = HandleCreateCameraAndSystems();
        HandleCreateCommandPanelUI(playerManager);
        HandleSpawnCitizen(terrain);

        EditorSceneManager.SaveScene(scene, TestScenePath);
    }

    // 지형이 잘 보이도록 디렉셔널 라이트를 추가한다.
    private static void HandleCreateLight()
    {
        var lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    // MapChunkManager로 청크 하나를 소환해 프리팹에 설정된 값 그대로 보로노이 지형을 굽는다.
    private static Terrain HandleCreateTerrain()
    {
        GameObject terrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChunkPrefabPath);

        var managerObject = new GameObject("MapChunkManager");
        MapChunkManager mapChunkManager = managerObject.AddComponent<MapChunkManager>();

        var so = new SerializedObject(mapChunkManager);
        so.FindProperty("m_ChunkPrefab").objectReferenceValue = terrainPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject chunkObject = mapChunkManager.DrawChunk(Vector2Int.zero);
        chunkObject.name = "BuildingPlacementTestTerrain";

        return chunkObject.GetComponent<Terrain>();
    }

    // 걷기 불가 영역(해수면 아래)을 눈으로 구분할 수 있도록 바다 평면을 배치한다.
    private static void HandleCreateSea(Terrain terrain)
    {
        TerrainData terrainData = terrain.terrainData;

        GameObject seaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SeaPrefabPath);
        GameObject seaObject = (GameObject)PrefabUtility.InstantiatePrefab(seaPrefab);

        // 기본 Plane 메시는 10x10 유닛이므로, 터레인의 가로/세로 크기에 맞추려면 10으로 나눈 배율로 스케일해야 한다.
        seaObject.transform.localScale = new Vector3(terrainData.size.x / 10f, 1f, terrainData.size.z / 10f);
        seaObject.transform.position = terrain.transform.position + new Vector3(
            terrainData.size.x * 0.5f,
            terrainData.size.y * SeaLevelNormalizedHeight,
            terrainData.size.z * 0.5f);
    }

    // 테스트 씬은 청크가 하나뿐이라 전체를 한 번만 굽는 것으로 충분하다.
    private static void HandleBakeNavMesh()
    {
        var navMeshObject = new GameObject("NavMesh");
        NavMeshSurface navMeshSurface = navMeshObject.AddComponent<NavMeshSurface>();
        navMeshSurface.collectObjects = CollectObjects.All;
        navMeshSurface.layerMask = LayerMask.GetMask("Ground");
        navMeshSurface.BuildNavMesh();
    }

    // 메인 카메라에 CameraController/PlayerManager/InputManager/ResourceInventory를 모두 붙여 상호 참조를 연결한다.
    private static PlayerManager HandleCreateCameraAndSystems()
    {
        var camObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camObject.tag = "MainCamera";
        camObject.transform.SetPositionAndRotation(new Vector3(250f, 300f, -150f), Quaternion.Euler(55f, 0f, 0f));

        var resourceInventory = camObject.AddComponent<ResourceInventory>();
        ItemDataList itemDataList = AssetDatabase.LoadAssetAtPath<ItemDataList>(ItemDataListPath);
        var inventorySo = new SerializedObject(resourceInventory);
        inventorySo.FindProperty("_itemDataList").objectReferenceValue = itemDataList;
        inventorySo.ApplyModifiedPropertiesWithoutUndo();

        var inputManager = camObject.AddComponent<InputManager>();

        var playerManager = camObject.AddComponent<PlayerManager>();
        var playerSo = new SerializedObject(playerManager);
        playerSo.FindProperty("_inputManager").objectReferenceValue = inputManager;
        playerSo.FindProperty("_resourceInventory").objectReferenceValue = resourceInventory;
        playerSo.ApplyModifiedPropertiesWithoutUndo();

        var cameraController = camObject.AddComponent<CameraController>();
        var cameraSo = new SerializedObject(cameraController);
        cameraSo.FindProperty("_playerManager").objectReferenceValue = playerManager;
        cameraSo.ApplyModifiedPropertiesWithoutUndo();

        return playerManager;
    }

    // 선택된 오브젝트의 명령(건축/취소 포함)을 하단에 버튼으로 표시하는 커맨드 패널을 Canvas 위에 구성한다.
    private static void HandleCreateCommandPanelUI(PlayerManager playerManager)
    {
        var canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var containerGO = new GameObject("ButtonContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        containerGO.transform.SetParent(canvasGO.transform, false);
        var containerRect = containerGO.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.sizeDelta = new Vector2(0f, 230f);
        containerRect.anchoredPosition = Vector2.zero;

        var layout = containerGO.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 30, 30);
        layout.spacing = 50f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var panelGO = new GameObject("TempCommandPanelUI", typeof(TempCommandPanelUI));
        panelGO.transform.SetParent(canvasGO.transform, false);

        CommandButtonView commandButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CommandButtonPrefabPath).GetComponent<CommandButtonView>();

        var panelSo = new SerializedObject(panelGO.GetComponent<TempCommandPanelUI>());
        panelSo.FindProperty("_buttonContainer").objectReferenceValue = containerGO.transform;
        panelSo.FindProperty("_commandButtonPrefab").objectReferenceValue = commandButtonPrefab;
        panelSo.FindProperty("_playerManager").objectReferenceValue = playerManager;
        panelSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // 지형 중앙 표면 위에 시민 프리팹을 소환해, 선택 후 '건물 짓기' 명령으로 배치 흐름을 바로 시험해볼 수 있게 한다.
    private static void HandleSpawnCitizen(Terrain terrain)
    {
        GameObject citizenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CitizenPrefabPath);
        GameObject citizenInstance = (GameObject)PrefabUtility.InstantiatePrefab(citizenPrefab);

        Vector3 center = terrain.transform.position + new Vector3(terrain.terrainData.size.x * 0.5f, 0f, terrain.terrainData.size.z * 0.5f);
        center.y = terrain.transform.position.y + terrain.SampleHeight(center);
        citizenInstance.transform.position = center;
    }
}
