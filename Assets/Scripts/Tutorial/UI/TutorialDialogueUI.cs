using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 튜토리얼 대사창. 메인 메뉴·일시정지 화면과 같은 사각형 디자인으로, 왼쪽 강조 막대·세 색 막대·말하는 이·단계 캡션·본문·키 안내와
// 예·아니요 선택 칸으로 이루어진다. 강조색은 지금 하는 일을 나타낸다. (대사 읽기는 하늘색, 질문은 초록, 목표 수행은 주황)
// 튜토리얼이 끝날 때까지 계속 떠 있어야 하고 ESC로 닫혀서도 안 되므로, UIManager의 개폐·풀링 대상이 아니라
// 채집 팝업처럼 캔버스 루트에 직접 붙는다. 대신 매 프레임 형제 순서를 맨 뒤로 옮겨 다른 UI 위에 그려지도록 한다.
public class TutorialDialogueUI : MonoBehaviour
{
    // 대화창이 지금 무엇을 기다리는지. 강조색과 아래쪽 키 안내가 이에 따라 바뀐다.
    private enum Mode
    {
        Talk,
        Question,
        Task,
    }

    // 본문을 클릭해 다음으로 넘어가려 할 때 발생한다.
    public event Action AdvanceRequested;

    // 예·아니요 중 하나를 골랐을 때 발생한다. (예를 고르면 true)
    public event Action<bool> ChoiceSelected;

    // 대화창 본체. 선택 칸이 뜰 때는 칸이 본문을 덮지 않도록 키운다.
    [SerializeField] private RectTransform _box;

    // 본문 아무 곳이나 눌러 다음으로 넘어가게 하는 버튼
    [SerializeField] private Button _advanceButton;

    // 대화창 왼쪽 강조 막대
    [SerializeField] private Image _accentBar;

    // 머리글 오른쪽 끝의 사각 표식
    [SerializeField] private Image _headerMarker;

    // 머리글 오른쪽의 "TUTORIAL · STEP 03 / 24" 캡션
    [SerializeField] private TextMeshProUGUI _stepCaption;

    // 대화창 본문 글자
    [SerializeField] private TextMeshProUGUI _bodyText;

    // 아래쪽 오른쪽의 키 안내 (SPACE NEXT 등)
    [SerializeField] private TextMeshProUGUI _footerHint;

    // 대화창 오른쪽에 붙는 그림 카드
    [SerializeField] private RectTransform _imageCard;

    // 그림 카드에 그릴 그림
    [SerializeField] private Image _cardImage;

    // 그림 카드 머리의 한글 설명
    [SerializeField] private TextMeshProUGUI _cardCaption;

    // 예·아니요 칸을 담는 묶음
    [SerializeField] private GameObject _choiceRow;

    // 예 칸과 그 문구
    [SerializeField] private Button _yesButton;
    [SerializeField] private TextMeshProUGUI _yesLabel;

    // 아니요 칸과 그 문구
    [SerializeField] private Button _noButton;
    [SerializeField] private TextMeshProUGUI _noLabel;

    // 마지막으로 화면에 반영한 내용. 같은 내용이면 다시 그리지 않는다.
    private TutorialDialogueContent _applied;

    // 아직 한 번도 내용을 반영하지 않았는지 여부
    private bool _isEmpty = true;

    // 지금 대화창이 기다리는 일
    private Mode _mode;

    // 마지막으로 표시한 단계 번호와 전체 단계 수. 바뀔 때만 캡션 글자를 다시 만든다.
    private int _shownStep = -1;
    private int _shownTotal = -1;

    // 본문이 원래 놓이는 위치. 내용이 바뀔 때 이 아래에서 떠오른다.
    private Vector2 _bodyBasePosition;

    // 본문이 떠오르기 시작한 뒤 지난 실제 시간(초)
    private float _revealElapsed = float.MaxValue;

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
        HandleRevealBody();
        HandleBlinkFooter();
    }

    // 표시할 내용을 반영한다. 직전과 같은 내용이면 아무것도 하지 않는다.
    public void SetContent(in TutorialDialogueContent content)
    {
        if (!_isEmpty && _applied.SameAs(content)) return;

        bool bodyChanged = _isEmpty || _applied.Body != content.Body;

        _applied = content;
        _isEmpty = false;

        _bodyText.text = content.Body;
        if (bodyChanged)
            _revealElapsed = 0f;

        bool hasChoices = content.HasChoices;
        ApplyMode(hasChoices ? Mode.Question : content.CanAdvance ? Mode.Talk : Mode.Task);

        // 선택 칸은 대화창 아래쪽을 차지하므로, 칸이 뜰 때만 본문이 밀리지 않게 창을 키운다.
        _box.sizeDelta = new Vector2(
            TutorialTheme.DialogueSize.x,
            hasChoices ? TutorialTheme.DialogueQuestionHeight : TutorialTheme.DialogueSize.y);

        _choiceRow.SetActive(hasChoices);
        _advanceButton.interactable = !hasChoices && content.CanAdvance;

        ApplyImageCard(content);

        if (!hasChoices) return;

        _yesLabel.text = content.YesLabel;
        _noLabel.text = content.NoLabel;
    }

    // 머리글의 단계 캡션을 "지금 단계 / 전체 단계"로 갱신한다. 값이 그대로면 아무것도 하지 않는다.
    public void SetProgress(int step, int total)
    {
        if (step == _shownStep && total == _shownTotal) return;

        _shownStep = step;
        _shownTotal = total;
        _stepCaption.text = $"TUTORIAL  ·  STEP {step:00} / {total:00}";
    }

    // 지금 하는 일에 맞춰 강조색과 아래쪽 키 안내를 바꾼다.
    private void ApplyMode(Mode mode)
    {
        _mode = mode;

        Color accent = GetAccent(mode);
        _accentBar.color = accent;
        _headerMarker.color = accent;

        string key = ColorUtility.ToHtmlStringRGB(accent);
        _footerHint.gameObject.SetActive(mode != Mode.Question);
        _footerHint.text = mode == Mode.Talk
            ? $"<color=#{key}>SPACE</color>   NEXT"
            : $"<color=#{key}>TASK</color>   IN PROGRESS";
        _footerHint.alpha = 1f;
    }

    // 하는 일별 강조색을 돌려준다.
    private static Color GetAccent(Mode mode)
    {
        switch (mode)
        {
            case Mode.Question: return TutorialTheme.Green;
            case Mode.Task: return TutorialTheme.Orange;
            default: return TutorialTheme.Cyan;
        }
    }

    // 그림 카드를 켜고 끄며, 그림의 원래 비율을 지킨 크기로 맞춘다.
    private void ApplyImageCard(in TutorialDialogueContent content)
    {
        bool hasImage = content.HasImage;
        _imageCard.gameObject.SetActive(hasImage);

        if (!hasImage) return;

        _cardImage.sprite = content.Image;
        _cardCaption.text = content.ImageCaption;
        _cardImage.rectTransform.sizeDelta = FitInside(content.Image, AvailableImageSize);
    }

    // 그림 카드에서 그림이 쓸 수 있는 영역 크기
    private static Vector2 AvailableImageSize => new Vector2(
        TutorialTheme.ImageCardSize.x - TutorialTheme.ImageCardPadding * 2f,
        TutorialTheme.ImageCardSize.y - TutorialTheme.ImageCardPadding * 3f - TutorialTheme.ImageCaptionHeight);

    // 그림을 원래 비율 그대로 지정한 영역 안에 들어가는 크기로 줄인다.
    private static Vector2 FitInside(Sprite sprite, Vector2 area)
    {
        Vector2 size = sprite.rect.size;
        if (size.x <= 0f || size.y <= 0f) return area;

        float scale = Mathf.Min(area.x / size.x, area.y / size.y);
        return size * scale;
    }

    // 화면 아래 가운데에 대사창 본체를 세운다.
    private void BuildDialogueBox(RectTransform root)
    {
        RectTransform box = TutorialUIBuilder.CreatePanel("DialogueBox", root);
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

        _accentBar = TutorialUIBuilder.CreateLeftAccent(box, TutorialTheme.Cyan);

        BuildTriBars(box);
        BuildHeader(box);
        BuildBody(box);
        BuildFooter(box);
        BuildChoiceRow(box);
        BuildImageCard(box);
    }

    // 대화창 왼쪽 위에 하늘·초록·주황 세 색 막대를 세운다. (일시정지 화면 Accent-Bars)
    private void BuildTriBars(RectTransform box)
    {
        Vector3 widths = TutorialTheme.TriBarWidths;
        float x = TutorialTheme.ContentPadding;

        x = AddTriBar(box, "Bar-Cyan", TutorialTheme.Cyan, x, widths.x);
        x = AddTriBar(box, "Bar-Green", TutorialTheme.Green, x, widths.y);
        AddTriBar(box, "Bar-Orange", TutorialTheme.Orange, x, widths.z);
    }

    // 세 색 막대 중 하나를 지정한 위치에 세우고, 다음 막대가 놓일 x 위치를 돌려준다.
    private static float AddTriBar(RectTransform box, string name, Color color, float x, float width)
    {
        Image bar = TutorialUIBuilder.CreateSolid(name, box, color);
        TutorialUIBuilder.Anchor(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -20f), new Vector2(width, TutorialTheme.TriBarHeight));
        return x + width + TutorialTheme.TriBarGap;
    }

    // 말하는 이 이름, 단계 캡션, 사각 표식과 그 아래 구분선을 세운다.
    private void BuildHeader(RectTransform box)
    {
        float pad = TutorialTheme.ContentPadding;
        float headerCenter = -(TutorialTheme.HeaderTop + TutorialTheme.HeaderHeight * 0.5f);

        TextMeshProUGUI speaker = TutorialUIBuilder.CreateText("Speaker", box, TutorialTheme.SpeakerName, TutorialTheme.SpeakerFontSize, TutorialTheme.TitleText, TextAlignmentOptions.MidlineLeft);
        speaker.characterSpacing = 2.3f;
        TutorialUIBuilder.Anchor(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(pad, headerCenter), new Vector2(300f, TutorialTheme.HeaderHeight));

        _headerMarker = TutorialUIBuilder.CreateSolid("Marker", box, TutorialTheme.Cyan);
        TutorialUIBuilder.Anchor(_headerMarker.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-pad, headerCenter), Vector2.one * TutorialTheme.MarkerSize);

        _stepCaption = TutorialUIBuilder.CreateCaption("StepCaption", box, string.Empty, TutorialTheme.CaptionFontSize, TutorialTheme.CaptionText, TutorialTheme.CaptionSpacing, TextAlignmentOptions.MidlineRight);
        TutorialUIBuilder.Anchor(_stepCaption.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-pad - TutorialTheme.MarkerSize - 16f, headerCenter), new Vector2(420f, TutorialTheme.HeaderHeight));

        Image divider = TutorialUIBuilder.CreateSolid("Divider", box, TutorialTheme.PanelBorder);
        RectTransform rect = divider.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(pad, -TutorialTheme.DividerTop - 1f);
        rect.offsetMax = new Vector2(-pad, -TutorialTheme.DividerTop);
    }

    // 구분선 아래에 본문 글자를 세운다.
    private void BuildBody(RectTransform box)
    {
        float pad = TutorialTheme.ContentPadding;
        float width = TutorialTheme.DialogueSize.x - pad * 2f;

        _bodyText = TutorialUIBuilder.CreateText("Body", box, string.Empty, TutorialTheme.BodyFontSize, TutorialTheme.TitleText, TextAlignmentOptions.TopLeft);
        _bodyText.lineSpacing = 8f;
        TutorialUIBuilder.Anchor(_bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -TutorialTheme.BodyTop), new Vector2(width, TutorialTheme.BodyHeight));
        _bodyBasePosition = _bodyText.rectTransform.anchoredPosition;
    }

    // 대화창 오른쪽 아래에 키 안내를 세운다. (일시정지 화면 Hint "ESC RESUME")
    private void BuildFooter(RectTransform box)
    {
        _footerHint = TutorialUIBuilder.CreateCaption("FooterHint", box, string.Empty, TutorialTheme.CaptionFontSize, TutorialTheme.MutedText, TutorialTheme.CaptionSpacing, TextAlignmentOptions.MidlineRight);
        TutorialUIBuilder.Anchor(_footerHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-TutorialTheme.ContentPadding, TutorialTheme.FooterBottom), new Vector2(420f, TutorialTheme.FooterHeight));
    }

    // 대화창 아래쪽에 예·아니요 선택 칸을 나란히 세운다. 칸의 번호가 답하는 키(1·2)를 알려 준다.
    private void BuildChoiceRow(RectTransform box)
    {
        Vector2 size = TutorialTheme.ChoiceButtonSize;
        float gap = TutorialTheme.ChoiceGap;

        RectTransform row = TutorialUIBuilder.CreateRect("ChoiceRow", box);
        TutorialUIBuilder.Anchor(row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(TutorialTheme.ContentPadding, 26f), new Vector2(size.x * 2f + gap, size.y));
        _choiceRow = row.gameObject;

        _yesButton = TutorialUIBuilder.CreateSlotButton("YesButton", row, size, TutorialTheme.Cyan, "1", out _yesLabel);
        TutorialUIBuilder.Anchor((RectTransform)_yesButton.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, size);
        _yesButton.onClick.AddListener(HandleYesClick);

        _noButton = TutorialUIBuilder.CreateSlotButton("NoButton", row, size, TutorialTheme.Orange, "2", out _noLabel);
        TutorialUIBuilder.Anchor((RectTransform)_noButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, size);
        _noButton.onClick.AddListener(HandleNoClick);

        _choiceRow.SetActive(false);
    }

    // 대화창 오른쪽 옆에 그림 카드를 세운다. 대화창의 자식이라 대화창이 위아래로 옮겨 가도 함께 따라간다.
    // 일시정지 화면 표식(Emblem)처럼 왼쪽 위·오른쪽 아래 모서리에 작은 사각 표식을 붙인다.
    private void BuildImageCard(RectTransform box)
    {
        RectTransform card = TutorialUIBuilder.CreatePanel("ImageCard", box);
        TutorialUIBuilder.Anchor(card, new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(TutorialTheme.ImageCardGap, 0f), TutorialTheme.ImageCardSize);
        _imageCard = card;
        // 보여 주기만 하는 카드이므로, 뒤쪽 월드 클릭을 가로채지 않게 한다.
        card.GetComponent<Image>().raycastTarget = false;

        float pad = TutorialTheme.ImageCardPadding;
        float width = TutorialTheme.ImageCardSize.x - pad * 2f;

        TextMeshProUGUI label = TutorialUIBuilder.CreateCaption("Label", card, "REFERENCE", TutorialTheme.CaptionFontSize, TutorialTheme.Cyan, TutorialTheme.CaptionSpacing, TextAlignmentOptions.TopLeft);
        TutorialUIBuilder.Anchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -pad), new Vector2(width, 22f));

        _cardCaption = TutorialUIBuilder.CreateText("Caption", card, string.Empty, TutorialTheme.ImageCaptionFontSize, TutorialTheme.MutedText, TextAlignmentOptions.TopLeft);
        TutorialUIBuilder.Anchor(_cardCaption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -pad - 26f), new Vector2(width, TutorialTheme.ImageCaptionHeight - 26f));

        // 그림은 캡션 아래 남은 공간 가운데에 두고, 실제 크기는 스프라이트 비율에 맞춰 그릴 때 정한다.
        _cardImage = TutorialUIBuilder.CreateImage("Picture", card, null, Color.white);
        TutorialUIBuilder.Anchor(_cardImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, pad), AvailableImageSize);
        _cardImage.preserveAspect = true;

        float corner = TutorialTheme.CornerMarkerSize;
        Image cornerA = TutorialUIBuilder.CreateSolid("CornerA", card, TutorialTheme.Cyan);
        TutorialUIBuilder.Anchor(cornerA.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * corner);
        Image cornerB = TutorialUIBuilder.CreateSolid("CornerB", card, TutorialTheme.Cyan);
        TutorialUIBuilder.Anchor(cornerB.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * corner);

        card.gameObject.SetActive(false);
    }

    // 창고·작업대·도감처럼 화면을 크게 덮는 UI가 열려 있으면, 그 UI를 가리지 않도록 대사창을 화면 위쪽으로 옮긴다.
    // 열려 있는 관리형 UI가 있는지는 활성 상태의 ManagedUIBase를 훑어 판단한다.
    private void HandleDock()
    {
        if (Time.unscaledTime < _nextDockCheckTime) return;

        _nextDockCheckTime = Time.unscaledTime + 0.2f;
        ApplyDock(HasOtherManagedUIOpen());
    }

    // 관리형 UI(창고·작업대·도감 등)가 열려 있는지 확인한다.
    private static bool HasOtherManagedUIOpen()
    {
        foreach (ManagedUIBase ui in FindObjectsByType<ManagedUIBase>(FindObjectsSortMode.None))
        {
            if (ui.State == UIState.Opening || ui.State == UIState.Open) return true;
        }

        return false;
    }

    // 대사창을 화면 위쪽 또는 아래쪽에 붙인다. 그림 카드도 대사창의 같은 쪽 변에 맞춰 화면 밖으로 넘치지 않게 한다.
    private void ApplyDock(bool dockTop)
    {
        if (_isDockedTop == dockTop) return;

        _isDockedTop = dockTop;

        Vector2 anchor = new Vector2(0.5f, dockTop ? 1f : 0f);
        _box.anchorMin = anchor;
        _box.anchorMax = anchor;
        _box.pivot = anchor;
        _box.anchoredPosition = new Vector2(0f, dockTop ? -TutorialTheme.ScreenMargin : TutorialTheme.DialogueBottomMargin);

        float edge = dockTop ? 1f : 0f;
        _imageCard.anchorMin = new Vector2(1f, edge);
        _imageCard.anchorMax = new Vector2(1f, edge);
        _imageCard.pivot = new Vector2(0f, edge);
        _imageCard.anchoredPosition = new Vector2(TutorialTheme.ImageCardGap, 0f);
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

    // 내용이 바뀐 직후 본문을 조금 아래에서 떠오르며 나타나게 한다. 일시정지 중에도 돌도록 실제 시간을 쓴다.
    private void HandleRevealBody()
    {
        if (_revealElapsed == float.MaxValue) return;

        _revealElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_revealElapsed / TutorialTheme.BodyRevealDuration);
        float eased = 1f - (1f - t) * (1f - t);

        _bodyText.alpha = eased;
        _bodyText.rectTransform.anchoredPosition = _bodyBasePosition + Vector2.down * (TutorialTheme.BodyRevealShift * (1f - eased));

        if (t >= 1f)
            _revealElapsed = float.MaxValue;
    }

    // 대사를 넘길 수 있을 때 키 안내를 천천히 깜빡여 Space로 넘기면 된다는 것을 알린다.
    private void HandleBlinkFooter()
    {
        if (_mode != Mode.Talk) return;

        _footerHint.alpha = Mathf.Lerp(0.45f, 1f, (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f);
    }

    // 본문 클릭을 다음으로 넘어가려는 요청으로 알린다.
    private void HandleAdvanceClick()
    {
        AudioManager.PlayButtonClick();
        AdvanceRequested?.Invoke();
    }

    // 예를 골랐음을 알린다.
    private void HandleYesClick()
    {
        AudioManager.PlayConfirm();
        ChoiceSelected?.Invoke(true);
    }

    // 아니요를 골랐음을 알린다.
    private void HandleNoClick()
    {
        AudioManager.PlayCancel();
        ChoiceSelected?.Invoke(false);
    }
}
