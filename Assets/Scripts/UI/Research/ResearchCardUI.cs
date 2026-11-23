using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연구 목록 격자에 놓이는 연구 카드 하나. 연구 상태(완료·연구 가능·시대 잠금 등)에 따라
// 썸네일과 상태 바의 색·문구·아이콘이 달라지고, 카드를 누르면 상세 패널을 여는 콜백을 호출한다.
// 도감 카드와 같은 각진 패널 디자인을 쓴다.
public class ResearchCardUI : MonoBehaviour
{
    [Header("카드")]
    [SerializeField] private Button _cardButton;
    [SerializeField] private Graphic _cardFill;
    [SerializeField] private Graphic _cardOutline;

    [Header("썸네일")]
    [SerializeField] private Graphic _thumbnailOutline;
    [SerializeField] private Image _thumbnail;
    // 잠긴 연구의 썸네일 위를 덮는 자물쇠 오버레이
    [SerializeField] private GameObject _lockOverlay;

    [Header("정보")]
    [SerializeField] private TextMeshProUGUI _indexText;
    [SerializeField] private TextMeshProUGUI _nameText;
    // 연구에 필요한 시대를 보여 주는 배지 (비워 두면 표시하지 않는다)
    [SerializeField] private Graphic _ageBadgeFill;
    [SerializeField] private TextMeshProUGUI _ageBadgeText;

    [Header("상태 바")]
    [SerializeField] private Graphic _statusFill;
    [SerializeField] private Graphic _statusOutline;
    [SerializeField] private Image _statusIcon;
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("상태 아이콘")]
    [SerializeField] private Sprite _checkIcon;
    [SerializeField] private Sprite _bulbIcon;
    [SerializeField] private Sprite _lockIcon;

    [Header("열린 카드 색상")]
    [SerializeField] private Color _unlockedCardFill = new Color32(0x0D, 0x0D, 0x1C, 0xE6);
    [SerializeField] private Color _unlockedCardOutline = new Color32(0x38, 0xBD, 0xF8, 0x59);
    [SerializeField] private Color _unlockedThumbOutline = new Color32(0x38, 0xBD, 0xF8, 0x40);
    [SerializeField] private Color _unlockedName = new Color32(0xEB, 0xF0, 0xFF, 0xFF);
    [SerializeField] private Color _unlockedAgeBadge = new Color32(0x38, 0xBD, 0xF8, 0x59);

    [Header("잠긴 카드 색상")]
    [SerializeField] private Color _lockedCardFill = new Color32(0x09, 0x09, 0x14, 0xE6);
    [SerializeField] private Color _lockedCardOutline = new Color32(0xFF, 0xFF, 0xFF, 0x1F);
    [SerializeField] private Color _lockedThumbOutline = new Color32(0xFF, 0xFF, 0xFF, 0x14);
    [SerializeField] private Color _lockedName = new Color32(0x94, 0xA3, 0xB8, 0xFF);
    [SerializeField] private Color _lockedAgeBadge = new Color32(0x94, 0xA3, 0xB8, 0x33);

    [Header("완료 상태 바 색상")]
    [SerializeField] private Color _completedFill = new Color32(0x4A, 0xDE, 0x80, 0x1F);
    [SerializeField] private Color _completedOutline = new Color32(0x4A, 0xDE, 0x80, 0xFF);
    [SerializeField] private Color _completedText = new Color32(0x4A, 0xDE, 0x80, 0xFF);

    [Header("연구 가능 상태 바 색상")]
    [SerializeField] private Color _availableFill = new Color32(0xFB, 0x92, 0x3C, 0xFF);
    [SerializeField] private Color _availableOutline = new Color32(0xFB, 0x92, 0x3C, 0x00);
    [SerializeField] private Color _availableText = new Color32(0x05, 0x05, 0x0E, 0xFF);

    [Header("잠금 상태 바 색상")]
    [SerializeField] private Color _lockedStatusFill = new Color32(0x05, 0x05, 0x0E, 0x99);
    [SerializeField] private Color _lockedStatusOutline = new Color32(0xFF, 0xFF, 0xFF, 0x1F);
    [SerializeField] private Color _lockedStatusText = new Color32(0x94, 0xA3, 0xB8, 0xFF);

    // 카드를 눌렀을 때 호출할 콜백. 상세 패널 열기에 쓴다.
    private Action _onClick;

    private void Awake()
    {
        _cardButton.onClick.AddListener(HandleCardClick);
    }

    // 연구 정보와 상태를 카드에 반영한다. displayNumber는 "No. 001"에 쓰는 목록 번호,
    // onClick은 카드를 눌렀을 때 상세 패널을 여는 콜백이다.
    public void Setup(ResearchData research, int displayNumber, ResearchState state, Action onClick)
    {
        _onClick = onClick;

        _indexText.text = $"No. {displayNumber:D3}";
        _nameText.text = research.ResearchName;
        if (_ageBadgeText != null) _ageBadgeText.text = research.RequiredAge.ToShortName();

        bool locked = state == ResearchState.AgeLocked || state == ResearchState.PrerequisiteLocked;
        ApplyThumbnail(research, locked);
        ApplyCardColors(locked);
        ApplyStatusBar(research, state);
    }

    // 잠긴 연구는 썸네일을 어둡게 덮고 자물쇠를 보여준다.
    private void ApplyThumbnail(ResearchData research, bool locked)
    {
        bool hasThumbnail = research.Thumbnail != null;

        _thumbnail.gameObject.SetActive(hasThumbnail);
        _thumbnail.sprite = research.Thumbnail;
        _thumbnail.color = locked ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
        _lockOverlay.SetActive(locked);
    }

    // 잠금 여부에 따른 카드 배경·테두리·이름·시대 배지 색을 적용한다.
    private void ApplyCardColors(bool locked)
    {
        _cardFill.color = locked ? _lockedCardFill : _unlockedCardFill;
        _cardOutline.color = locked ? _lockedCardOutline : _unlockedCardOutline;
        _thumbnailOutline.color = locked ? _lockedThumbOutline : _unlockedThumbOutline;
        _nameText.color = locked ? _lockedName : _unlockedName;
        if (_ageBadgeFill != null) _ageBadgeFill.color = locked ? _lockedAgeBadge : _unlockedAgeBadge;
    }

    // 상태에 맞는 색·아이콘·문구로 상태 바를 그린다.
    private void ApplyStatusBar(ResearchData research, ResearchState state)
    {
        switch (state)
        {
            case ResearchState.Completed:
                ApplyStatus(_completedFill, _completedOutline, _completedText, _checkIcon, "연구 완료");
                break;

            case ResearchState.Available:
                ApplyStatus(_availableFill, _availableOutline, _availableText, _bulbIcon, "연구");
                break;

            case ResearchState.PrerequisiteLocked:
                ApplyStatus(_lockedStatusFill, _lockedStatusOutline, _lockedStatusText, _lockIcon, "선행 연구 필요");
                break;

            default:
                ApplyStatus(_lockedStatusFill, _lockedStatusOutline, _lockedStatusText, _lockIcon, $"{research.RequiredAge.ToTabName()} 필요");
                break;
        }
    }

    // 상태 바의 배경·테두리·아이콘·문구를 한 번에 반영한다.
    private void ApplyStatus(Color fill, Color outline, Color text, Sprite icon, string label)
    {
        _statusFill.color = fill;
        _statusOutline.color = outline;
        _statusText.color = text;
        _statusText.text = label;

        _statusIcon.sprite = icon;
        _statusIcon.color = text;
        _statusIcon.gameObject.SetActive(icon != null);
    }

    // 카드 클릭을 등록된 콜백으로 전달한다.
    private void HandleCardClick() => _onClick?.Invoke();
}
