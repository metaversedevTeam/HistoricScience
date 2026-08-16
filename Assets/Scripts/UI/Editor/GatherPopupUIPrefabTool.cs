using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 아이템 획득 팝업(GatherPopupUI) 프리팹을 생성하는 에디터 도구.
// 팝업을 띄우는 주체는 ResourceInventory를 구독하는 ItemGainPopupPresenter이므로, 프리팹 연결은 그 컴포넌트에 한다.
public static class GatherPopupUIPrefabTool
{
    public const string PrefabPath = "Assets/Prefabs/UI/GatherPopupUI.prefab";

    [MenuItem("Tools/HistoricScience/Generate Gather Popup UI Prefab")]
    public static void Generate()
    {
        CreateOrUpdatePrefab();
        AssetDatabase.SaveAssets();
        Debug.Log($"[GatherPopupUIPrefabTool] '{PrefabPath}' 프리팹을 생성했습니다. ItemGainPopupPresenter의 프리팹 필드에 연결해 주세요.");
    }

    // 팝업 계층을 만들어 프리팹 에셋으로 저장한다.
    private static GameObject CreateOrUpdatePrefab()
    {
        GameObject root = BuildHierarchy("GatherPopupUI");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // 아이콘 이미지와 개수 텍스트를 가로로 나열한 팝업 GameObject 계층을 만든다.
    private static GameObject BuildHierarchy(string name)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        var rect = root.GetComponent<RectTransform>();
        // 화면 좌표를 그대로 anchoredPosition으로 쓰기 위해 앵커·피벗을 중앙으로 맞춘다.
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(140f, 56f);

        var canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = root.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image icon = BuildIcon(root.transform);
        TextMeshProUGUI countText = BuildCountText(root.transform);

        var popup = root.AddComponent<GatherPopupUI>();
        var so = new SerializedObject(popup);
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_countText").objectReferenceValue = countText;
        so.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    // 아이템 아이콘을 표시할 Image 자식을 만든다.
    private static Image BuildIcon(Transform parent)
    {
        var go = new GameObject("Icon", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(48f, 48f);

        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;

        return image;
    }

    // 획득 개수를 표시할 텍스트 자식을 만든다.
    private static TextMeshProUGUI BuildCountText(Transform parent)
    {
        var go = new GameObject("CountText", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(80f, 48f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "+1";
        tmp.fontSize = 36f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return tmp;
    }

}
