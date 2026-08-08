using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// 도감을 여는 근거지(HomeBase) 건물 프리팹을 Base 모델로 생성하고, 저장 프리팹 목록과 시민의 건설 목록에 등록하는 에디터 도구
public static class HomeBasePrefabTool
{
    private const string PrefabPath = "Assets/Prefabs/Object/Home Base.prefab";
    private const string ModelPrefabPath = "Assets/Art/Models/Base Model/Base Model.prefab";
    private const string BottomPrefabPath = "Assets/Art/Models/BuildingBottom/Building Bottom.prefab";
    private const string CodexUiPrefabPath = "Assets/Prefabs/UI/ItemCodexUI.prefab";
    private const string CodexIconPath = "Assets/Art/Sprites/UI/Generated/Icon_Book.png";
    private const string StoneItemPath = "Assets/Data/ScriptableObjects/자원/아이템/돌.asset";
    private const string RegistryPath = "Assets/Data/ScriptableObjects/저장 프리팹 목록.asset";
    private const string CitizenPrefabPath = "Assets/Prefabs/Object/Citizen.prefab";

    // 저장 프리팹 목록과 프리팹의 SavableHandler가 공유해야 하는 식별 키
    private const string PrefabId = "HomeBase";
    // 건설에 필요한 돌 수량
    private const int StoneBuildCost = 5;

    // CapsuleCollider.direction의 Y축 값
    private const int CapsuleDirectionY = 1;

    // 프리팹 안에서 모델·바닥 자식을 다시 찾기 위한 이름. 스케일을 이어받을 때 쓴다.
    private const string ModelChildName = "Base Model";
    private const string BottomChildName = "Building Bottom";

    // 프리팹이 아직 없을 때 쓸 기본 스케일. 이미 프리팹이 있으면 에디터에서 조정한 스케일을 그대로 이어받는다.
    private static readonly Vector3 DefaultModelScale = new Vector3(3f, 3f, 3f);
    private static readonly Vector3 DefaultBottomScale = new Vector3(2f, 1f, 2f);

    [MenuItem("Tools/HistoricScience/Generate Home Base Prefab")]
    public static void Generate()
    {
        GameObject modelPrefab = LoadRequired<GameObject>(ModelPrefabPath);
        GameObject bottomPrefab = LoadRequired<GameObject>(BottomPrefabPath);
        if (modelPrefab == null || bottomPrefab == null) return;

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject root = new GameObject("Home Base");

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localScale = ReadChildScale(existing, ModelChildName, DefaultModelScale);

        GameObject bottom = (GameObject)PrefabUtility.InstantiatePrefab(bottomPrefab, root.transform);
        bottom.transform.localPosition = Vector3.zero;
        bottom.transform.localScale = ReadChildScale(existing, BottomChildName, DefaultBottomScale);

        Bounds modelBounds = CalculateLocalBounds(root, model);
        float footprintRadius = Mathf.Max(modelBounds.extents.x, modelBounds.extents.z);
        // 아래에서 root를 파괴하고 나면 model에 접근할 수 없으므로 로그에 쓸 값을 미리 꺼내 둔다.
        Vector3 modelScale = model.transform.localScale;

        AddObjectComponents(root, bottom, modelBounds, footprintRadius);
        AddHomeBaseComponent(root, model);

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        RegisterInSavableRegistry(prefabAsset);
        RegisterInCitizenBuildables(prefabAsset);

        AssetDatabase.SaveAssets();
        Debug.Log($"[HomeBasePrefabTool] '{PrefabPath}' 프리팹을 생성했습니다. 모델 스케일 {modelScale.x:0.##}, 모델 크기 {modelBounds.size}, 캡슐 반지름 {footprintRadius:0.##}");
    }

    // 이미 만들어 둔 프리팹에서 자식의 스케일을 읽어 온다. 에디터에서 손으로 조정한 모델 크기가 재생성 때 되돌아가지 않게 하기 위한 것으로,
    // 프리팹이나 해당 자식이 없으면 기본값을 그대로 쓴다.
    private static Vector3 ReadChildScale(GameObject existingPrefab, string childName, Vector3 fallback)
    {
        if (existingPrefab == null) return fallback;

        Transform child = existingPrefab.transform.Find(childName);
        return child != null ? child.localScale : fallback;
    }

    // 클릭·선택·저장·지면 정렬 등 다른 건물과 동일한 오브젝트 공용 컴포넌트를 붙이고, 모델 크기에 맞춰 판정 형상과 반경을 설정한다.
    private static void AddObjectComponents(GameObject root, GameObject bottom, Bounds modelBounds, float footprintRadius)
    {
        root.AddComponent<ClickableObject>();
        root.AddComponent<SelectableObject>();
        root.AddComponent<ChunkBoundObject>();

        // 캡슐은 높이가 지름보다 작으면 어차피 구로 취급되므로, 인스펙터에 보이는 값과 실제 형상이 어긋나지 않도록 미리 지름까지 올려 둔다.
        float capsuleHeight = Mathf.Max(modelBounds.size.y, footprintRadius * 2f);
        Vector3 capsuleCenter = new Vector3(0f, modelBounds.center.y, 0f);

        // 모델 프리팹에는 콜라이더가 없어, 이것이 없으면 클릭 레이캐스트에 잡히지 않아 선택할 수 없다.
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.direction = CapsuleDirectionY;
        collider.radius = footprintRadius;
        collider.height = capsuleHeight;
        collider.center = capsuleCenter;

        NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.radius = footprintRadius;
        obstacle.height = capsuleHeight;
        obstacle.center = capsuleCenter;

        int groundLayer = LayerMask.GetMask("Ground");

        GroundSnapper snapper = root.AddComponent<GroundSnapper>();
        SetLayerMask(snapper, "_groundLayer", groundLayer);

        HitableObject hitable = root.AddComponent<HitableObject>();
        SetSerializedFloat(hitable, "_hitRadius", footprintRadius);

        BottomMaterialController bottomController = root.AddComponent<BottomMaterialController>();
        var bottomSo = new SerializedObject(bottomController);
        bottomSo.FindProperty("_bottomRenderer").objectReferenceValue = bottom.GetComponentInChildren<MeshRenderer>();
        bottomSo.FindProperty("_groundLayer").intValue = groundLayer;
        bottomSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // HomeBase 컴포넌트를 붙이고 도감 UI·아이콘·배치 미리보기 모델·건설 비용·저장 식별 키를 연결한다.
    private static void AddHomeBaseComponent(GameObject root, GameObject model)
    {
        HomeBase homeBase = root.AddComponent<HomeBase>();

        GameObject codexUiPrefab = LoadRequired<GameObject>(CodexUiPrefabPath);
        Sprite codexIcon = LoadRequired<Sprite>(CodexIconPath);
        ResourceData stone = LoadRequired<ResourceData>(StoneItemPath);

        var so = new SerializedObject(homeBase);
        so.FindProperty("_itemCodexUiPrefab").objectReferenceValue = codexUiPrefab != null ? codexUiPrefab.GetComponent<ItemCodexUI>() : null;
        so.FindProperty("_codexButtonIcon").objectReferenceValue = codexIcon;
        so.FindProperty("_buildingIcon").objectReferenceValue = codexIcon;
        so.FindProperty("_buildingModel").objectReferenceValue = model;
        so.FindProperty("_savable._prefabId").stringValue = PrefabId;

        SerializedProperty cost = so.FindProperty("_buildCost");
        cost.arraySize = 1;
        SerializedProperty costEntry = cost.GetArrayElementAtIndex(0);
        costEntry.FindPropertyRelative("Resource").objectReferenceValue = stone;
        costEntry.FindPropertyRelative("Count").intValue = StoneBuildCost;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 모델의 모든 렌더러를 합친 크기를 루트 로컬 공간 기준으로 계산한다. 콜라이더·판정 반경을 모델에 맞추는 데 쓴다.
    private static Bounds CalculateLocalBounds(GameObject root, GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        bounds.center -= root.transform.position;
        return bounds;
    }

    // 저장 프리팹 목록에 근거지 항목을 추가한다. 같은 PrefabId가 이미 있으면 프리팹 참조만 갱신한다.
    private static void RegisterInSavableRegistry(GameObject prefabAsset)
    {
        SavablePrefabRegistry registry = LoadRequired<SavablePrefabRegistry>(RegistryPath);
        if (registry == null) return;

        var so = new SerializedObject(registry);
        SerializedProperty entries = so.FindProperty("_entries");

        SerializedProperty target = FindEntryByPrefabId(entries, PrefabId);
        if (target == null)
        {
            entries.arraySize++;
            target = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            target.FindPropertyRelative("PrefabId").stringValue = PrefabId;
        }

        target.FindPropertyRelative("Prefab").objectReferenceValue = prefabAsset;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(registry);
    }

    // 저장 프리팹 목록에서 주어진 PrefabId를 가진 항목을 찾는다. 없으면 null을 반환한다.
    private static SerializedProperty FindEntryByPrefabId(SerializedProperty entries, string prefabId)
    {
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("PrefabId").stringValue == prefabId)
                return entry;
        }
        return null;
    }

    // 시민의 건물 선택 목록에 근거지를 추가해 '건물 짓기'에서 고를 수 있게 한다. 이미 들어 있으면 아무것도 하지 않는다.
    private static void RegisterInCitizenBuildables(GameObject prefabAsset)
    {
        using var editScope = new PrefabUtility.EditPrefabContentsScope(CitizenPrefabPath);
        Citizen citizen = editScope.prefabContentsRoot.GetComponent<Citizen>();
        if (citizen == null)
        {
            Debug.LogWarning($"[HomeBasePrefabTool] '{CitizenPrefabPath}'에서 Citizen 컴포넌트를 찾지 못했습니다.");
            return;
        }

        var so = new SerializedObject(citizen);
        SerializedProperty buildables = so.FindProperty("_buildablePrefabs");

        for (int i = 0; i < buildables.arraySize; i++)
        {
            if (buildables.GetArrayElementAtIndex(i).objectReferenceValue == prefabAsset)
                return;
        }

        buildables.arraySize++;
        buildables.GetArrayElementAtIndex(buildables.arraySize - 1).objectReferenceValue = prefabAsset;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 컴포넌트의 LayerMask 필드를 설정한다.
    private static void SetLayerMask(Object target, string propertyPath, int mask)
    {
        var so = new SerializedObject(target);
        so.FindProperty(propertyPath).intValue = mask;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 컴포넌트의 float 필드를 설정한다.
    private static void SetSerializedFloat(Object target, string propertyPath, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(propertyPath).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 에셋을 불러오고, 없으면 어떤 경로가 비었는지 알 수 있도록 오류를 남긴다.
    private static T LoadRequired<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            Debug.LogError($"[HomeBasePrefabTool] '{path}' 에셋을 찾지 못했습니다.");

        return asset;
    }
}
