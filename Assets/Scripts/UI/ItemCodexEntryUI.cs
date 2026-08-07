using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도감 격자에 놓이는 아이템 카드 하나. 획득 여부에 따라 썸네일·이름·상태 표시가 달라진다.
public class ItemCodexEntryUI : MonoBehaviour
{
    [Header("카드")]
    [SerializeField] private Image _cardFill;
    [SerializeField] private Image _cardOutline;

    [Header("썸네일")]
    [SerializeField] private Image _thumbnailOutline;
    [SerializeField] private Image _thumbnail;
    [SerializeField] private Image _lockIcon;

    [Header("정보")]
    [SerializeField] private TextMeshProUGUI _indexText;
    [SerializeField] private Image _ageBadgeFill;
    [SerializeField] private TextMeshProUGUI _ageBadgeText;
    [SerializeField] private TextMeshProUGUI _nameText;

    [Header("상태 바")]
    [SerializeField] private Image _statusFill;
    [SerializeField] private Image _statusOutline;
    [SerializeField] private Image _statusIcon;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private Sprite _discoveredIcon;
    [SerializeField] private Sprite _undiscoveredIcon;

    [Header("획득 색상")]
    [SerializeField] private Color _discoveredCardFill = new Color32(0x0C, 0x14, 0x24, 0xFF);
    [SerializeField] private Color _discoveredCardOutline = new Color32(0x8C, 0x3B, 0x1F, 0xFF);
    [SerializeField] private Color _discoveredThumbOutline = new Color32(0x1B, 0x3A, 0x6B, 0xFF);
    [SerializeField] private Color _discoveredName = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    [SerializeField] private Color _discoveredStatusFill = new Color32(0x07, 0x1C, 0x14, 0xFF);
    [SerializeField] private Color _discoveredStatusOutline = new Color32(0x16, 0xA3, 0x4A, 0xFF);
    [SerializeField] private Color _discoveredStatusText = new Color32(0x22, 0xC5, 0x5E, 0xFF);

    [Header("미획득 색상")]
    [SerializeField] private Color _undiscoveredCardFill = new Color32(0x09, 0x0D, 0x18, 0xFF);
    [SerializeField] private Color _undiscoveredCardOutline = new Color32(0x23, 0x2B, 0x3E, 0xFF);
    [SerializeField] private Color _undiscoveredThumbOutline = new Color32(0x1A, 0x21, 0x30, 0xFF);
    [SerializeField] private Color _undiscoveredName = new Color32(0x7B, 0x84, 0x96, 0xFF);
    [SerializeField] private Color _undiscoveredStatusFill = new Color32(0x0A, 0x0F, 0x1C, 0xFF);
    [SerializeField] private Color _undiscoveredStatusOutline = new Color32(0x2A, 0x33, 0x45, 0xFF);
    [SerializeField] private Color _undiscoveredStatusText = new Color32(0x7B, 0x84, 0x96, 0xFF);

    // 아이템 정보와 획득 여부를 카드에 반영한다. displayNumber는 "No. 001"에 쓰는 도감 번호다.
    public void Setup(ItemData item, int displayNumber, bool discovered)
    {
        _indexText.text = $"No. {displayNumber:D3}";
        _ageBadgeText.text = item.Age.ToShortName();
        _nameText.text = item.Nmae;

        ApplyThumbnail(item, discovered);
        ApplyStateColors(discovered);
    }

    // 획득한 아이템만 아이콘을 보여주고, 미획득이면 자물쇠 표시로 대체한다.
    private void ApplyThumbnail(ItemData item, bool discovered)
    {
        bool hasIcon = discovered && item.IconSprite != null;

        _thumbnail.gameObject.SetActive(hasIcon);
        _thumbnail.sprite = hasIcon ? item.IconSprite : null;
        _lockIcon.gameObject.SetActive(!discovered);
    }

    // 획득 여부에 따른 색상 팔레트와 상태 문구를 카드 전체에 적용한다.
    private void ApplyStateColors(bool discovered)
    {
        _cardFill.color = discovered ? _discoveredCardFill : _undiscoveredCardFill;
        _cardOutline.color = discovered ? _discoveredCardOutline : _undiscoveredCardOutline;
        _thumbnailOutline.color = discovered ? _discoveredThumbOutline : _undiscoveredThumbOutline;
        _nameText.color = discovered ? _discoveredName : _undiscoveredName;

        _statusFill.color = discovered ? _discoveredStatusFill : _undiscoveredStatusFill;
        _statusOutline.color = discovered ? _discoveredStatusOutline : _undiscoveredStatusOutline;
        _statusText.color = discovered ? _discoveredStatusText : _undiscoveredStatusText;
        _statusText.text = discovered ? "수집 완료" : "미획득";

        _statusIcon.color = discovered ? _discoveredStatusText : _undiscoveredStatusText;
        _statusIcon.sprite = discovered ? _discoveredIcon : _undiscoveredIcon;

        _ageBadgeFill.color = discovered
            ? new Color32(0x25, 0x63, 0xEB, 0xFF)
            : new Color32(0x25, 0x63, 0xEB, 0x99);
    }
}
