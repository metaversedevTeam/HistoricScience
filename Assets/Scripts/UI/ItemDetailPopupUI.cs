using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 아이템 정보 팝업 UI — 도감에서 이미 해금한 아이템의 썸네일을 눌렀을 때 열리며,
// 아이템 이름·시대 배지·도감 번호·큰 썸네일·유물 설명을 한 화면에 보여준다.
// 보여주기만 하는 창이라 닫기(✕)와 확인 버튼은 모두 창을 닫는 동작만 한다.
public class ItemDetailPopupUI : OpenableUIBase<ItemDetailData>
{
    [Header("헤더")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TextMeshProUGUI _eraTagText;
    [SerializeField] private TextMeshProUGUI _codexNumberText;
    // 도감 번호 문구 형식. {0}에 도감 번호가 들어간다.
    [SerializeField] private string _codexNumberFormat = "도감 번호: No. {0:D3}";

    [Header("썸네일")]
    [SerializeField] private Image _thumbnail;
    // 아이콘이 없는 아이템을 열었을 때 빈 액자 대신 띄울 안내 문구
    [SerializeField] private TextMeshProUGUI _thumbnailEmptyText;

    [Header("설명")]
    [SerializeField] private TextMeshProUGUI _descriptionText;
    // 설명이 비어 있는 아이템에 대신 표시할 문구
    [SerializeField, TextArea(2, 4)] private string _emptyDescription = "아직 기록되지 않은 유물입니다.";

    [Header("확인")]
    [SerializeField] private Button _confirmButton;

    private void Awake()
    {
        _closeButton.onClick.AddListener(HandleCloseButtonClick);
        _confirmButton.onClick.AddListener(HandleConfirmButtonClick);
    }

    // 보여 줄 아이템과 도감 번호를 주입받고 화면을 채운다.
    protected override void ApplyData(ItemDetailData data)
    {
        ItemData item = data.Item;
        if (item == null)
        {
            Debug.LogWarning($"ItemDetailPopupUI({name}): 아이템 없이 열려 내용을 채울 수 없습니다.");
            return;
        }

        ApplyHeader(item, data.CodexNumber);
        ApplyThumbnail(item);
        ApplyDescription(item);
    }

    // 이름·시대 배지·도감 번호를 표시한다.
    private void ApplyHeader(ItemData item, int codexNumber)
    {
        _titleText.text = item.Nmae;
        _eraTagText.text = item.Age.ToTabName();
        _codexNumberText.text = string.Format(_codexNumberFormat, codexNumber);
    }

    // 큰 썸네일에 아이템 아이콘을 채운다. 아이콘이 없으면 안내 문구로 대체한다.
    private void ApplyThumbnail(ItemData item)
    {
        bool hasIcon = item.IconSprite != null;

        _thumbnail.gameObject.SetActive(hasIcon);
        _thumbnail.sprite = hasIcon ? item.IconSprite : null;
        _thumbnailEmptyText.gameObject.SetActive(!hasIcon);
    }

    // 유물 설명을 표시한다. 설명이 비어 있으면 기본 안내 문구를 대신 쓴다.
    private void ApplyDescription(ItemData item)
    {
        _descriptionText.text = string.IsNullOrWhiteSpace(item.Description)
            ? _emptyDescription
            : item.Description;
    }

    // 다음에 열릴 때 이전 아이템의 내용이 남지 않도록 표시를 비운다.
    protected override void OnReturnToPool()
    {
        _titleText.text = string.Empty;
        _codexNumberText.text = string.Empty;
        _eraTagText.text = string.Empty;
        _descriptionText.text = string.Empty;
        _thumbnail.sprite = null;
    }

    // 닫기 버튼을 누르면 UI를 닫는다. (ESC로 닫는 UIManager 경로와 동일한 동작)
    private void HandleCloseButtonClick() => Close();

    // 확인 버튼도 창을 닫는 것 말고는 할 일이 없으므로 닫기와 같게 동작한다.
    private void HandleConfirmButtonClick() => Close();
}

// 아이템 정보 팝업에 전달되는 페이로드 — 보여 줄 아이템과 도감 목록에서 매긴 번호
public readonly struct ItemDetailData
{
    public readonly ItemData Item;
    public readonly int CodexNumber;

    // 아이템과 도감 번호로 페이로드를 구성한다.
    public ItemDetailData(ItemData item, int codexNumber)
    {
        Item = item;
        CodexNumber = codexNumber;
    }
}
