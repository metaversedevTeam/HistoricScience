using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 맵 관리 화면의 목록 한 줄. 슬롯 이름과 저장일을 보여 주고 불러오기·삭제 버튼을 제공하며,
// 줄 자체를 누르면 선택 상태(테두리 강조)가 된다.
public class MapListRowUI : MonoBehaviour
{
    [SerializeField] private Button _selectButton;
    [SerializeField] private Image _background;
    [SerializeField] private Image _outline;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _dateText;
    [SerializeField] private Button _loadButton;
    [SerializeField] private Button _deleteButton;

    [Header("선택됨")]
    [SerializeField] private Color _selectedBackground = new Color32(0x16, 0x12, 0x2C, 0x99);
    [SerializeField] private Color _selectedOutline = new Color32(0x3B, 0x82, 0xF6, 0xFF);

    [Header("선택 안 됨")]
    [SerializeField] private Color _normalBackground = new Color32(0x0D, 0x0A, 0x1B, 0xCC);
    [SerializeField] private Color _normalOutline = new Color32(0x1E, 0x15, 0x45, 0xFF);

    // 저장일 앞에 붙는 안내 문구. 날짜만 밝은 색으로 강조하기 위해 리치 텍스트를 쓴다.
    private const string DateFormat = "저장일: <color=#F8FAFC>{0:yyyy.MM.dd}</color>";

    private Action<MapListRowUI> _onLoad;
    private Action<MapListRowUI> _onDelete;
    private Action<MapListRowUI> _onSelect;

    // 이 줄이 가리키는 맵 저장 슬롯 이름
    public string Slot { get; private set; }

    private void Awake()
    {
        _selectButton.onClick.AddListener(HandleSelectClick);
        _loadButton.onClick.AddListener(HandleLoadClick);
        _deleteButton.onClick.AddListener(HandleDeleteClick);
    }

    // 줄에 표시할 슬롯 정보와 버튼 콜백을 채운다.
    public void Setup(string slot, DateTime savedAt, Action<MapListRowUI> onSelect, Action<MapListRowUI> onLoad, Action<MapListRowUI> onDelete)
    {
        Slot = slot;
        _titleText.text = slot;
        _dateText.text = string.Format(DateFormat, savedAt);
        _onSelect = onSelect;
        _onLoad = onLoad;
        _onDelete = onDelete;

        SetSelected(false);
    }

    // 선택 여부에 따라 배경과 테두리 색을 바꾼다.
    public void SetSelected(bool selected)
    {
        _background.color = selected ? _selectedBackground : _normalBackground;
        _outline.color = selected ? _selectedOutline : _normalOutline;
    }

    // 줄을 눌러 선택했음을 알린다.
    private void HandleSelectClick()
    {
        _onSelect?.Invoke(this);
    }

    // 불러오기 요청을 알린다. 선택 표시도 이 줄로 옮긴다.
    private void HandleLoadClick()
    {
        _onSelect?.Invoke(this);
        _onLoad?.Invoke(this);
    }

    // 삭제 요청을 알린다.
    private void HandleDeleteClick()
    {
        _onDelete?.Invoke(this);
    }
}
