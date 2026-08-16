using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 피그마 '로딩' 프레임(1920x1080, 우주 배경 + 가운데 720x720 지구 아이콘)을 그대로 옮긴
// 로딩 화면(LoadingScreenUI) 프리팹을 생성하는 에디터 도구.
// 로딩 화면을 띄우는 주체는 IngameSceneManager이므로, 프리팹 연결은 그 컴포넌트에 한다.
public static class LoadingScreenUIPrefabTool
{
    public const string PrefabPath = "Assets/Prefabs/UI/LoadingScreenUI.prefab";

    // 메인 메뉴와 같은 우주 배경(가운데가 보라빛으로 밝아지는 방사형 그라디언트) 스프라이트 경로
    private const string BackgroundSpritePath = "Assets/Art/Sprites/UI/Generated/Menu/MenuBackground.png";

    // 메인 메뉴와 같은 지구 아이콘 스프라이트 경로
    private const string GlobeSpritePath = "Assets/Art/Textures/UI/MenuGlobe.png";

    // 피그마 프레임에서의 지구 아이콘 크기
    private static readonly Vector2 GlobeSize = new Vector2(720f, 720f);

    // 다른 UI보다 항상 위에 그려지도록 로딩 화면 캔버스에 줄 정렬 순서
    private const int SortingOrder = 100;

    // 피그마 렌더링과 맞춘 지구 아이콘의 불투명도
    private const float GlobeAlpha = 0.3f;

    [MenuItem("Tools/HistoricScience/Generate Loading Screen UI Prefab")]
    public static void Generate()
    {
        CreateOrUpdatePrefab();
        AssetDatabase.SaveAssets();
        Debug.Log($"[LoadingScreenUIPrefabTool] '{PrefabPath}' 프리팹을 생성했습니다. IngameSceneManager의 Loading Screen UI Prefab 필드에 연결해 주세요.");
    }

    // 로딩 화면 계층을 만들어 프리팹 에셋으로 저장한다.
    private static GameObject CreateOrUpdatePrefab()
    {
        EnsureFolder("Assets/Prefabs/UI");

        GameObject root = BuildHierarchy("LoadingScreenUI");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // 화면 전체를 덮는 배경과 가운데 지구 아이콘으로 이루어진 로딩 화면 GameObject 계층을 만든다.
    private static GameObject BuildHierarchy(string name)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
        StretchToParent(root.GetComponent<RectTransform>());

        // 다른 UI 위에 겹쳐 그려지도록 부모 캔버스와 별개의 정렬 순서를 갖게 한다.
        var canvas = root.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        // 로딩이 끝날 때까지 아래쪽 UI로 입력이 새지 않게 막는다.
        var canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;

        BuildBackground(root.transform);
        RectTransform globe = BuildGlobe(root.transform);

        var loadingScreen = root.AddComponent<LoadingScreenUI>();
        var so = new SerializedObject(loadingScreen);
        so.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("_globe").objectReferenceValue = globe;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    // 화면 전체를 덮는 우주 배경 이미지를 만든다. 뒤쪽 화면이 클릭되지 않도록 이 이미지가 입력을 받아 낸다.
    private static Image BuildBackground(Transform parent)
    {
        var go = new GameObject("Background", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        StretchToParent(go.GetComponent<RectTransform>());

        var image = go.AddComponent<Image>();
        image.sprite = LoadSprite(BackgroundSpritePath);
        image.color = Color.white;
        image.raycastTarget = true;

        return image;
    }

    // 화면 가운데에 지구 아이콘 이미지를 만든다. 회전하는 대상이므로 RectTransform을 돌려준다.
    private static RectTransform BuildGlobe(Transform parent)
    {
        var go = new GameObject("Globe", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = GlobeSize;

        var image = go.AddComponent<Image>();
        image.sprite = LoadSprite(GlobeSpritePath);
        image.color = new Color(1f, 1f, 1f, GlobeAlpha);
        image.preserveAspect = true;
        image.raycastTarget = false;

        return rect;
    }

    // RectTransform을 부모 영역 전체에 늘려 붙인다.
    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 스프라이트를 경로로 불러온다. 없으면 경고만 남기고 비어 있는 이미지로 둔다.
    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"[LoadingScreenUIPrefabTool] '{path}' 스프라이트를 찾지 못했습니다.");

        return sprite;
    }

    // 프리팹을 저장할 폴더가 없으면 상위부터 차례로 만든다.
    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

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
