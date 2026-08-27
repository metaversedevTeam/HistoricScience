using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 창고 격자에 놓이는 슬롯 하나. 아이템이 담기면 아이콘·이름·수량을 보여주고, 비었으면 빈 칸 테두리만 남는다.
// 테두리와 수량 글자색은 아이템의 시대에 따라 달라진다.
public class WarehouseSlotUI : MonoBehaviour
{
    [Header("슬롯")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _outline;
    [SerializeField] private Image _selectedOutline;

    [Header("아이콘")]
    [SerializeField] private RectTransform _iconArea;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _placeholderIcon;

    [Header("정보")]
    [SerializeField] private RectTransform _meta;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _countText;

    [Header("빈 칸 색상")]
    [SerializeField] private Color _emptyOutline = new Color32(0x33, 0x33, 0x33, 0xFF);

    [Header("시대 색상")]
    [SerializeField] private AgePalette[] _palettes =
    {
        new AgePalette(Age.nature, new Color32(0x33, 0x33, 0x33, 0xFF), new Color32(0x16, 0xA3, 0x4A, 0xFF)),
        new AgePalette(Age.Paleolithic, new Color32(0x0F, 0x76, 0x6E, 0xFF), new Color32(0xFF, 0xFF, 0xFF, 0xFF)),
        new AgePalette(Age.Neolithic, new Color32(0x25, 0x63, 0xEB, 0xFF), new Color32(0xFF, 0xFF, 0xFF, 0xFF)),
        new AgePalette(Age.bronzeAge, new Color32(0xB4, 0x53, 0x09, 0xFF), new Color32(0xFF, 0xFF, 0xFF, 0xFF))
    };

    // 이 슬롯이 담고 있는 아이템. 빈 칸이면 null이다.
    public ItemData Item { get; private set; }

    private Action<WarehouseSlotUI> _onClick;

    private void Awake()
    {
        _button.onClick.AddListener(HandleClick);
    }

    // 슬롯 클릭 시 호출될 콜백을 등록한다. 격자에 생성된 직후 한 번만 부르면 된다.
    public void SetClickHandler(Action<WarehouseSlotUI> onClick)
    {
        _onClick = onClick;
    }

    // 아이템과 보유 수량을 슬롯에 반영하고, 시대 색으로 테두리·수량 글자색을 칠한다.
    public void Setup(ItemData item, int count)
    {
        Item = item;

        _iconArea.gameObject.SetActive(true);
        _meta.gameObject.SetActive(true);
        _button.interactable = true;

        bool hasIcon = item.IconSprite != null;
        _icon.gameObject.SetActive(hasIcon);
        _icon.sprite = item.IconSprite;
        _placeholderIcon.gameObject.SetActive(!hasIcon);

        _nameText.text = item.Nmae;
        _countText.text = $"x{count}";

        AgePalette palette = FindPalette(item.Age);
        _outline.color = palette.Outline;
        _countText.color = palette.Count;
    }

    // 슬롯을 빈 칸으로 되돌린다. 아이콘·문구를 감추고 테두리만 남긴다.
    public void SetupEmpty()
    {
        Item = null;

        _iconArea.gameObject.SetActive(false);
        _meta.gameObject.SetActive(false);
        _button.interactable = false;
        _outline.color = _emptyOutline;

        SetSelected(false);
    }

    // 선택 강조 테두리를 켜고 끈다.
    public void SetSelected(bool selected)
    {
        _selectedOutline.gameObject.SetActive(selected);
    }

    // 시대에 해당하는 색상 조합을 찾는다. 등록되지 않은 시대는 빈 칸 색과 흰색 수량으로 대체한다.
    private AgePalette FindPalette(Age age)
    {
        for (int i = 0; i < _palettes.Length; i++)
        {
            if (_palettes[i].Age == age)
                return _palettes[i];
        }

        return new AgePalette(age, _emptyOutline, Color.white);
    }

    // 빈 칸이 아닐 때만 클릭을 콜백으로 전달한다.
    private void HandleClick()
    {
        if (Item == null) return;

        AudioManager.PlayButtonClick();
        _onClick?.Invoke(this);
    }

    // 시대 하나에 대응하는 슬롯 테두리 색과 수량 글자색의 조합.
    [Serializable]
    private struct AgePalette
    {
        public Age Age;
        public Color Outline;
        public Color Count;

        public AgePalette(Age age, Color outline, Color count)
        {
            Age = age;
            Outline = outline;
            Count = count;
        }
    }
}
