using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// CommandButton 프리팹과, 그 결과를 눈으로 확인할 테스트 씬을 생성하는 에디터 도구
public static class CommandButtonPrefabTool
{
    private const string PrefabPath = "Assets/Prefabs/UI/CommandButton.prefab";
    private const string TestScenePath = "Assets/ExternalAssets/Test/Scenes/CommandButtonTest.unity";

    [MenuItem("Tools/HistoricScience/Generate Command Button Prefab And Test Scene")]
    public static void Generate()
    {
        GameObject prefab = CreateOrUpdatePrefab();
        CreateTestScene(prefab);
        Debug.Log($"[CommandButtonPrefabTool] '{PrefabPath}' 프리팹과 '{TestScenePath}' 테스트 씬을 생성했습니다.");
    }

    // CommandButton 계층을 만들어 프리팹 에셋으로 저장한다.
    private static GameObject CreateOrUpdatePrefab()
    {
        GameObject root = BuildCommandButtonHierarchy("CommandButton");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // 배경 이미지, 버튼, 텍스트 라벨로 구성된 커맨드 버튼 GameObject 계층을 만든다.
    private static GameObject BuildCommandButtonHierarchy(string name)
    {
        var root = new GameObject(name, typeof(RectTransform));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(150f, 150f);

        var img = root.AddComponent<Image>();
        img.color = new Color(0.851f, 0.851f, 0.851f, 1f);

        var btn = root.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = new Color(0.851f, 0.851f, 0.851f);
        colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f);
        colors.pressedColor     = new Color(0.7f, 0.7f, 0.7f);
        btn.colors = colors;
        btn.targetGraphic = img;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(root.transform, false);
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text             = "Command";
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin      = 14f;
        tmp.fontSizeMax      = 50f;
        tmp.color            = new Color32(0xFF, 0x00, 0x04, 0xFF);

        var view = root.AddComponent<CommandButtonView>();
        var so = new SerializedObject(view);
        so.FindProperty("_button").objectReferenceValue = btn;
        so.FindProperty("_label").objectReferenceValue = tmp;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    // Canvas 위에 프리팹 버튼 4개를 CommandPanel과 동일한 레이아웃으로 배치한 확인용 씬을 만든다.
    // 이미 열려 있는 씬(작업 중인 씬)을 건드리지 않도록 Additive로 새 씬을 만들고, 저장 후 다시 닫는다.
    private static void CreateTestScene(GameObject prefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

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
        containerRect.pivot     = new Vector2(0.5f, 0f);
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

        string[] sampleNames = { "이동", "건설", "채집", "정보" };
        foreach (string sampleName in sampleNames)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, containerGO.transform);
            var view = instance.GetComponent<CommandButtonView>();
            view.Bind(new CommandData(sampleName, null, () => Debug.Log($"[CommandButtonTest] {sampleName} 클릭됨")));
        }

        EditorSceneManager.SaveScene(scene, TestScenePath);
        EditorSceneManager.CloseScene(scene, true);
    }
}
