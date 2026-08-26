using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 프리팹 없이 코드로 튜토리얼 UI 계층을 세우기 위한 생성 헬퍼 모음.
// 튜토리얼을 폴더 하나로 걷어낼 수 있게 하려고 UI 프리팹을 만들지 않고, 여기서 같은 모양을 코드로 조립한다.
public static class TutorialUIBuilder
{
    // 찾아 둔 한글 글꼴. 매번 다시 찾지 않도록 캐싱한다.
    private static TMP_FontAsset _font;

    // 프로젝트가 이미 쓰고 있는 한글 글꼴. 튜토리얼 전용 폰트 에셋을 따로 두지 않으려고 실행 중에 찾아 빌려 쓴다.
    public static TMP_FontAsset Font => _font != null ? _font : _font = ResolveFont();

    // 빈 RectTransform 오브젝트를 만들어 부모에 붙인다.
    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    // 스프라이트를 그리는 Image 오브젝트를 만든다. 클릭을 가로채지 않도록 기본적으로 레이캐스트 대상에서 빼 둔다.
    public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, Image.Type type = Image.Type.Simple)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = type;
        image.raycastTarget = false;
        return image;
    }

    // 글자 오브젝트를 만든다. 글꼴은 프로젝트가 쓰고 있는 한글 글꼴을 그대로 따른다.
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

    // 피그마 키트와 같은 모양(둥근 사각형 + 테두리 + 가운데 글자)의 버튼을 만든다.
    public static Button CreateButton(string name, Transform parent, Vector2 size, string label, Color labelColor, out TextMeshProUGUI labelText)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.sizeDelta = size;

        Image background = rect.gameObject.AddComponent<Image>();
        background.sprite = TutorialSpriteLibrary.ChipFill;
        background.type = Image.Type.Sliced;
        background.color = TutorialTheme.ButtonFill;
        background.raycastTarget = true;

        Image outline = CreateImage("Outline", rect, TutorialSpriteLibrary.ChipOutline, TutorialTheme.PanelBorder, Image.Type.Sliced);
        Stretch(outline.rectTransform);

        labelText = CreateText("Label", rect, label, TutorialTheme.ButtonFontSize, labelColor, TextAlignmentOptions.Center);
        Stretch(labelText.rectTransform);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        return button;
    }

    // 피그마 대화창과 같은 모양(둥근 사각형 채움 + 테두리)의 패널을 만들고 그 RectTransform을 돌려준다.
    public static RectTransform CreatePanel(string name, Transform parent, Color fill, Color border)
    {
        Image background = CreateImage(name, parent, TutorialSpriteLibrary.PanelFill, fill, Image.Type.Sliced);
        // 대화창은 뒤쪽 월드 클릭을 받지 않아야 하므로 이 패널만 레이캐스트를 받는다.
        background.raycastTarget = true;

        Image outline = CreateImage("Outline", background.rectTransform, TutorialSpriteLibrary.PanelOutline, border, Image.Type.Sliced);
        Stretch(outline.rectTransform);

        return background.rectTransform;
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
