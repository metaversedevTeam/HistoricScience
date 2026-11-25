using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 프리팹 없이 코드로 튜토리얼 UI 계층을 세우기 위한 생성 헬퍼 모음.
// 튜토리얼을 폴더 하나로 걷어낼 수 있게 하려고 UI 프리팹을 만들지 않고, 일시정지 화면과 같은 모양을 여기서 코드로 조립한다.
public static class TutorialUIBuilder
{
    // 찾아 둔 한글 글꼴. 매번 다시 찾지 않도록 캐싱한다.
    private static TMP_FontAsset _font;

    // 프로젝트가 이미 쓰고 있는 한글 글꼴(BMHANNA). 튜토리얼 전용 폰트 에셋을 따로 두지 않으려고 실행 중에 찾아 빌려 쓴다.
    public static TMP_FontAsset Font => _font != null ? _font : _font = ResolveFont();

    // 영문 캡션·번호에 쓰는 글꼴. 일시정지 화면처럼 TMP 기본 글꼴(Liberation Sans)을 쓴다.
    public static TMP_FontAsset LatinFont => TMP_Settings.defaultFontAsset;

    // 빈 RectTransform 오브젝트를 만들어 부모에 붙인다.
    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    // 스프라이트를 그리는 Image 오브젝트를 만든다. 스프라이트를 비우면 단색 사각형이 된다.
    // 클릭을 가로채지 않도록 기본적으로 레이캐스트 대상에서 빼 둔다.
    public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // 단색 사각형을 만든다. 강조 막대·표식·구분선에 쓴다.
    public static Image CreateSolid(string name, Transform parent, Color color)
    {
        return CreateImage(name, parent, null, color);
    }

    // 한글 글자 오브젝트를 만든다. 글꼴은 프로젝트가 쓰고 있는 한글 글꼴을 그대로 따른다.
    public static TextMeshProUGUI CreateText(string name, Transform parent, string content, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

        if (Font != null)
            text.font = Font;

        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    // 일시정지 화면의 RESUME·ESC 같은 영문 캡션을 만든다. 굵은 영문 글꼴에 자간을 넓게 준다.
    public static TextMeshProUGUI CreateCaption(string name, Transform parent, string content, float fontSize, Color color, float spacing, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

        if (LatinFont != null)
            text.font = LatinFont;

        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = spacing;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    // 일시정지 화면 메뉴 칸과 같은 모양(모서리 없는 어두운 판 + 얇은 테두리)의 패널을 만들고 그 RectTransform을 돌려준다.
    public static RectTransform CreatePanel(string name, Transform parent)
    {
        Image background = CreateSolid(name, parent, TutorialTheme.PanelFill);
        // 대화창은 뒤쪽 월드 클릭을 받지 않아야 하므로 이 패널만 레이캐스트를 받는다.
        background.raycastTarget = true;

        CreateOutline("Border", background.rectTransform, TutorialTheme.PanelBorder, TutorialTheme.BorderThickness);
        return background.rectTransform;
    }

    // 부모 크기에 맞춘 사각 테두리를 만든다.
    public static RectOutlineGraphic CreateOutline(string name, Transform parent, Color color, float thickness)
    {
        RectTransform rect = CreateRect(name, parent);
        Stretch(rect);

        RectOutlineGraphic outline = rect.gameObject.AddComponent<RectOutlineGraphic>();
        outline.color = color;
        outline.Thickness = thickness;
        outline.raycastTarget = false;
        return outline;
    }

    // 판의 왼쪽 끝에 세로로 붙는 강조 막대를 만든다.
    public static Image CreateLeftAccent(Transform panel, Color color)
    {
        Image accent = CreateSolid("Accent", panel, color);
        RectTransform rect = accent.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(TutorialTheme.AccentBarWidth, 0f);
        return accent;
    }

    // 일시정지 화면 메뉴 칸과 같은 모양의 선택 버튼(강조 막대 + 번호 + 한글 문구 + 오른쪽 사각 표식)을 만든다.
    // 마우스를 올리면 강조색이 옅게 차오른다.
    public static Button CreateSlotButton(string name, Transform parent, Vector2 size, Color accent, string index, out TextMeshProUGUI label)
    {
        RectTransform rect = CreatePanel(name, parent);
        rect.sizeDelta = size;

        // 평소에는 투명하다가 마우스를 올리거나 누르면 강조색이 옅게 비친다.
        Color fillColor = accent;
        fillColor.a = 0.16f;
        Image fill = CreateSolid("Fill", rect, fillColor);
        Stretch(fill.rectTransform);

        CreateLeftAccent(rect, accent);

        TextMeshProUGUI indexText = CreateCaption("Index", rect, index, TutorialTheme.IndexFontSize, accent, TutorialTheme.IndexSpacing, TextAlignmentOptions.MidlineLeft);
        Anchor(indexText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(40f, size.y));

        label = CreateText("Label", rect, string.Empty, TutorialTheme.ButtonFontSize, TutorialTheme.TitleText, TextAlignmentOptions.MidlineLeft);
        Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(72f, 0f), new Vector2(size.x - 120f, size.y));

        Image marker = CreateSolid("Marker", rect, accent);
        Anchor(marker.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), Vector2.one * TutorialTheme.MarkerSize);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = new Color(1f, 1f, 1f, 0f);
        colors.pressedColor = new Color(1f, 1f, 1f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        return button;
    }

    // RectTransform을 부모 전체로 늘린다.
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // RectTransform을 부모의 한 지점에 고정하고 크기를 지정한다.
    public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    // 프로젝트에 이미 올라와 있는 글꼴 중 한글을 그릴 수 있는 것을 찾는다. 없으면 TMP 기본 글꼴로 돌아간다.
    private static TMP_FontAsset ResolveFont()
    {
        foreach (TMP_FontAsset candidate in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (candidate != null && candidate.HasCharacter('가'))
                return candidate;
        }

        foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font != null && text.font.HasCharacter('가'))
                return text.font;
        }

        Debug.LogWarning("TutorialUIBuilder: 한글을 그릴 수 있는 글꼴을 찾지 못해 TMP 기본 글꼴을 사용합니다.");
        return TMP_Settings.defaultFontAsset;
    }
}
