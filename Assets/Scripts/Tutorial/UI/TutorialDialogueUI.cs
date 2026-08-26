using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 튜토리얼 대사창 (피그마 dialogue-box). 아바타·말하는 이 배지·본문·진행 삼각형과 예·아니요 선택 버튼으로 이루어진다.
// 튜토리얼이 끝날 때까지 계속 떠 있어야 하고 ESC로 닫혀서도 안 되므로, UIManager의 개폐·풀링 대상이 아니라
// 채집 팝업처럼 캔버스 루트에 직접 붙는다. 대신 매 프레임 형제 순서를 맨 뒤로 옮겨 다른 UI 위에 그려지도록 한다.
public class TutorialDialogueUI : MonoBehaviour
{
    // 본문을 클릭해 다음으로 넘어가려 할 때 발생한다.
    public event Action AdvanceRequested;

    // 예·아니요 중 하나를 골랐을 때 발생한다. (예를 고르면 true)
    public event Action<bool> ChoiceSelected;

    // 대화창 본체. 선택 버튼이 뜰 때는 버튼이 본문을 덮지 않도록 키운다.
    [SerializeField] private RectTransform _box;

    // 대화창 본문 글자
    [SerializeField] private TextMeshProUGUI _bodyText;

    // 본문 아무 곳이나 눌러 다음으로 넘어가게 하는 버튼
    [SerializeField] private Button _advanceButton;

    // 더 읽을 대사가 있음을 알리는 삼각형
    [SerializeField] private Image _nextIndicator;

    // 대화창 오른쪽에 붙는 그림 카드
    [SerializeField] private GameObject _imageCard;

    // 그림 카드에 그릴 그림
    [SerializeField] private Image _cardImage;

    // 그림 카드 위쪽의 설명
    [SerializeField] private TextMeshProUGUI _cardCaption;

    // 예·아니요 버튼을 담는 묶음
    [SerializeField] private GameObject _choiceRow;

    // 예 버튼과 그 문구
    [SerializeField] private Button _yesButton;
    [SerializeField] private TextMeshProUGUI _yesLabel;

    // 아니요 버튼과 그 문구
    [SerializeField] private Button _noButton;
    [SerializeField] private TextMeshProUGUI _noLabel;

    // 마지막으로 화면에 반영한 내용. 같은 내용이면 다시 그리지 않는다.
    private TutorialDialogueContent _applied;

    // 아직 한 번도 내용을 반영하지 않았는지 여부
    private bool _isEmpty = true;

    // 지금 대사창이 화면 위쪽에 붙어 있는지 여부
    private bool _isDockedTop;

    // 다른 UI가 열려 있는지 다시 확인할 시각
    private float _nextDockCheckTime;

    // 코드로 대사창을 세워 부모(캔버스 루트) 아래에 붙이고 돌려준다.
    public static TutorialDialogueUI Create(Transform parent)
    {
        RectTransform root = TutorialUIBuilder.CreateRect("TutorialDialogueUI", parent);
        TutorialUIBuilder.Stretch(root);

        TutorialDialogueUI ui = root.gameObject.AddComponent<TutorialDialogueUI>();
        ui.BuildDialogueBox(root);
        return ui;
    }

    private void LateUpdate()
    {
        HandleKeepOnTop();
        HandleDock();
        HandleBlinkIndicator();
    }

    // 표시할 내용을 반영한다. 직전과 같은 내용이면 아무것도 하지 않는다.
    public void SetContent(in TutorialDialogueContent content)
    {
        if (!_isEmpty && _applied.SameAs(content)) return;

        _applied = content;
        _isEmpty = false;

        _bodyText.text = content.Body;

        bool hasChoices = content.HasChoices;

        // 선택 버튼은 대화창 아래쪽을 차지하므로, 버튼이 뜰 때만 본문이 밀리지 않게 창을 키운다.
        _box.sizeDelta = new Vector2(
            TutorialTheme.DialogueSize.x,
            hasChoices ? TutorialTheme.DialogueQuestionHeight : TutorialTheme.DialogueSize.y);

        _choiceRow.SetActive(hasChoices);
        _nextIndicator.gameObject.SetActive(!hasChoices && content.CanAdvance);
        _advanceButton.interactable = !hasChoices && content.CanAdvance;

        ApplyImageCard(content);

        if (!hasChoices) return;

        _yesLabel.text = content.YesLabel;
        _noLabel.text = content.NoLabel;
    }

    // 그림 카드를 켜고 끄며, 그림의 원래 비율을 지킨 크기로 맞춘다.
    private void ApplyImageCard(in TutorialDialogueContent content)
    {
        bool hasImage = content.HasImage;
        _imageCard.SetActive(hasImage);

        if (!hasImage) return;

        _cardImage.sprite = content.Image;
        _cardCaption.text = content.ImageCaption;
        _cardImage.rectTransform.sizeDelta = FitInside(content.Image, AvailableImageSize);
    }

    // 그림 카드에서 그림이 쓸 수 있는 영역 크기
    private static Vector2 AvailableImageSize => new Vector2(
        TutorialTheme.ImageCardSize.x - TutorialTheme.ImageCardPadding * 2f,
        TutorialTheme.ImageCardSize.y - TutorialTheme.ImageCardPadding * 2f - TutorialTheme.ImageCaptionHeight);

    // 그림을 원래 비율 그대로 지정한 영역 안에 들어가는 크기로 줄인다.
    private static Vector2 FitInside(Sprite sprite, Vector2 area)
    {
        Vector2 size = sprite.rect.size;
        if (size.x <= 0f || size.y <= 0f) return area;

        float scale = Mathf.Min(area.x / size.x, area.y / size.y);
        return size * scale;
    }

    // 화면 아래 가운데에 대사창 본체를 세운다. (피그마 dialogue-box)
    private void BuildDialogueBox(RectTransform root)
    {
        RectTransform box = TutorialUIBuilder.CreatePanel("DialogueBox", root, TutorialTheme.PanelFill, TutorialTheme.PanelBorder);
        TutorialUIBuilder.Anchor(
            box,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, TutorialTheme.DialogueBottomMargin),
            TutorialTheme.DialogueSize);
        _box = box;

        _advanceButton = box.gameObject.AddComponent<Button>();
        _advanceButton.transition = Selectable.Transition.None;
        _advanceButton.onClick.AddListener(HandleAdvanceClick);

        BuildAvatar(box);
        BuildContent(box);
        BuildNextIndicator(box);
        BuildChoiceRow(box);
        BuildImageCard(box);
    }

    // 대화창 오른쪽 옆에 그림 카드를 세운다. 대화창의 자식이라 대화창이 위아래로 옮겨 가도 함께 따라간다.
    private void BuildImageCard(RectTransform box)
    {
        // 카드가 대화창보다 길어 세로 가운데에 두면 아래쪽 커맨드 패널을 침범하므로, 대화창과 아랫변을 맞춘다.
        RectTransform card = TutorialUIBuilder.CreatePanel("ImageCard", box, TutorialTheme.PanelFill, TutorialTheme.PanelBorder);
        TutorialUIBuilder.Anchor(
            card,
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(TutorialTheme.ImageCardGap, 0f),
            TutorialTheme.ImageCardSize);
        _imageCard = card.gameObject;
        // 보여 주기만 하는 카드이므로, 뒤쪽 월드 클릭을 가로채지 않게 한다.
        card.GetComponent<Image>().raycastTarget = false;

        _cardCaption = TutorialUIBuilder.CreateText("Caption", card, string.Empty, TutorialTheme.ImageCaptionFontSize, TutorialTheme.Accent, TextAlignmentOptions.Center);
        TutorialUIBuilder.Anchor(
            _cardCaption.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -TutorialTheme.ImageCardPadding),
            new Vector2(TutorialTheme.ImageCardSize.x - TutorialTheme.ImageCardPadding * 2f, TutorialTheme.ImageCaptionHeight));

        // 그림은 카드의 남은 공간 가운데에 두고, 실제 크기는 스프라이트 비율에 맞춰 그릴 때 정한다.
        _cardImage = TutorialUIBuilder.CreateImage("Picture", card, null, Color.white);
        TutorialUIBuilder.Anchor(
            _cardImage.rectTransform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, TutorialTheme.ImageCardPadding),
            AvailableImageSize);
        _cardImage.preserveAspect = true;

        _imageCard.SetActive(false);
    }

    // 대사창 왼쪽에 안내자 아바타를 세운다. (피그마 character-avatar-container)
    private void BuildAvatar(RectTransform box)
    {
        Vector2 size = new Vector2(TutorialTheme.AvatarSize, TutorialTheme.AvatarSize);

        // 선택 버튼 때문에 창이 길어져도 본문과 나란히 있도록, 세로 가운데가 아니라 위쪽을 기준으로 붙인다.
        Image fill = TutorialUIBuilder.CreateImage("Avatar", box, TutorialSpriteLibrary.Circle, TutorialTheme.AvatarFill);
        TutorialUIBuilder.Anchor(
            fill.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(TutorialTheme.AvatarLeftMargin, -TutorialTheme.AvatarTopMargin),
            size);

        Sprite portrait = TutorialImageLibrary.GuideAvatar;
        if (portrait != null)
        {
            Image image = TutorialUIBuilder.CreateImage("Portrait", fill.rectTransform, portrait, Color.white);
            TutorialUIBuilder.Stretch(image.rectTransform);
        }
        else
        {
            // 안내자 이미지가 아직 없으면 이름 첫 글자로 자리를 채운다.
            TextMeshProUGUI placeholder = TutorialUIBuilder.CreateText("Placeholder", fill.rectTransform, TutorialTheme.SpeakerName.Substring(0, 1), 34f, TutorialTheme.Accent, TextAlignmentOptions.Center);
            TutorialUIBuilder.Stretch(placeholder.rectTransform);
        }

        Image outline = TutorialUIBuilder.CreateImage("Ring", fill.rectTransform, TutorialSpriteLibrary.CircleOutline, TutorialTheme.Accent);
        TutorialUIBuilder.Stretch(outline.rectTransform);
    }

    // 대사창 가운데에 말하는 이 배지와 본문 글자를 세운다. (피그마 dialogue-content)
    private void BuildContent(RectTransform box)
    {
        float width = TutorialTheme.DialogueSize.x - TutorialTheme.ContentLeftMargin - TutorialTheme.ContentRightMargin;

        RectTransform content = TutorialUIBuilder.CreateRect("Content", box);
        TutorialUIBuilder.Anchor(content, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(TutorialTheme.ContentLeftMargin, -52f), new Vector2(width, 100f));

        Image badge = TutorialUIBuilder.CreateImage("SpeakerBadge", content, TutorialSpriteLibrary.ChipFill, TutorialTheme.ChipFill, Image.Type.Sliced);
        TutorialUIBuilder.Anchor(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, TutorialTheme.BadgeSize);

        TextMeshProUGUI badgeText = TutorialUIBuilder.CreateText("Label", badge.rectTransform, TutorialTheme.SpeakerName, TutorialTheme.BadgeFontSize, TutorialTheme.Accent, TextAlignmentOptions.Center);
        TutorialUIBuilder.Stretch(badgeText.rectTransform);

        _bodyText = TutorialUIBuilder.CreateText("Body", content, string.Empty, TutorialTheme.BodyFontSize, TutorialTheme.BodyText, TextAlignmentOptions.TopLeft);
        TutorialUIBuilder.Anchor(_bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -34f), new Vector2(width, 66f));
    }

    // 대사창 오른쪽 아래에 진행 삼각형을 세운다. (피그마 next-indicator)
    private void BuildNextIndicator(RectTransform box)
    {
        _nextIndicator = TutorialUIBuilder.CreateImage("NextIndicator", box, TutorialSpriteLibrary.TriangleUp, TutorialTheme.Amber);
        TutorialUIBuilder.Anchor(_nextIndicator.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 18f), TutorialTheme.NextIndicatorSize);
    }

    // 대사창 오른쪽 아래에 예·아니요 선택 버튼을 세운다.
    private void BuildChoiceRow(RectTransform box)
    {
        Vector2 size = TutorialTheme.ChoiceButtonSize;
        const float Gap = 12f;

        RectTransform row = TutorialUIBuilder.CreateRect("ChoiceRow", box);
        TutorialUIBuilder.Anchor(row, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 18f), new Vector2(size.x * 2f + Gap, size.y));
        _choiceRow = row.gameObject;

        _yesButton = TutorialUIBuilder.CreateButton("YesButton", row, size, string.Empty, TutorialTheme.Accent, out _yesLabel);
        TutorialUIBuilder.Anchor(_yesButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, size);
        _yesButton.onClick.AddListener(HandleYesClick);

        _noButton = TutorialUIBuilder.CreateButton("NoButton", row, size, string.Empty, TutorialTheme.BodyText, out _noLabel);
        TutorialUIBuilder.Anchor(_noButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, size);
        _noButton.onClick.AddListener(HandleNoClick);

        _choiceRow.SetActive(false);
    }

    // 창고·작업대·도감처럼 화면을 크게 덮는 UI가 열려 있으면, 그 UI를 가리지 않도록 대사창을 화면 위쪽으로 옮긴다.
    // 열려 있는 관리형 UI가 있는지는 활성 상태의 ManagedUIBase를 훑어 판단하며, 튜토리얼 자신의 강조 UI는 세지 않는다.
    private void HandleDock()
    {
        if (Time.time >= _nextDockCheckTime)
        {
            _nextDockCheckTime = Time.time + 0.2f;
            ApplyDock(HasOtherManagedUIOpen());
        }
    }

    // 튜토리얼이 만든 것이 아닌 관리형 UI가 열려 있는지 확인한다.
    private static bool HasOtherManagedUIOpen()
    {
        foreach (ManagedUIBase ui in FindObjectsByType<ManagedUIBase>(FindObjectsSortMode.None))
        {
            if (ui is TutorialHighlightUI) continue;
            if (ui.State == UIState.Opening || ui.State == UIState.Open) return true;
        }

        return false;
    }

    // 대사창을 화면 위쪽 또는 아래쪽에 붙인다. 바뀐 것이 없으면 아무것도 하지 않는다.
    private void ApplyDock(bool dockTop)
    {
        if (_isDockedTop == dockTop) return;

        _isDockedTop = dockTop;

        Vector2 anchor = new Vector2(0.5f, dockTop ? 1f : 0f);
        _box.anchorMin = anchor;
        _box.anchorMax = anchor;
        _box.pivot = anchor;
        _box.anchoredPosition = new Vector2(0f, dockTop ? -TutorialTheme.ScreenMargin : TutorialTheme.DialogueBottomMargin);
    }

    // 다른 UI 위에 그려지도록 형제 순서를 맨 뒤로 옮긴다.
    private void HandleKeepOnTop()
    {
        Transform parent = transform.parent;
        if (parent == null) return;

        int last = parent.childCount - 1;
        if (transform.GetSiblingIndex() != last)
            transform.SetSiblingIndex(last);
    }

    // 진행 삼각형을 천천히 깜빡여 클릭할 수 있음을 알린다.
    private void HandleBlinkIndicator()
    {
        if (!_nextIndicator.gameObject.activeSelf) return;

        Color color = TutorialTheme.Amber;
        color.a = Mathf.Lerp(0.35f, 1f, (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f);
        _nextIndicator.color = color;
    }

    // 본문 클릭을 다음으로 넘어가려는 요청으로 알린다.
    private void HandleAdvanceClick() => AdvanceRequested?.Invoke();

    // 예를 골랐음을 알린다.
    private void HandleYesClick() => ChoiceSelected?.Invoke(true);

    // 아니요를 골랐음을 알린다.
    private void HandleNoClick() => ChoiceSelected?.Invoke(false);
}
