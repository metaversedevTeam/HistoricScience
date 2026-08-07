using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도감 상단의 시대 필터 탭 하나. 선택 여부에 따라 배경·글자색이 바뀌고, 잠긴 탭은 눌리지 않는다.
public class CodexAgeTabUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _fill;
    [SerializeField] private Image _outline;
    [SerializeField] private TextMeshProUGUI _label;

    [Header("선택됨")]
    [SerializeField] private Color _selectedFill = new Color32(0xE2, 0x52, 0x1A, 0xFF);
    [SerializeField] private Color _selectedOutline = new Color32(0xE2, 0x52, 0x1A, 0x00);
    [SerializeField] private Color _selectedLabel = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

    [Header("선택 안 됨")]
    [SerializeField] private Color _normalFill = new Color32(0x0E, 0x15, 0x26, 0xFF);
    [SerializeField] private Color _normalOutline = new Color32(0x2A, 0x35, 0x50, 0xFF);
    [SerializeField] private Color _normalLabel = new Color32(0xC9, 0xD2, 0xE3, 0xFF);

    [Header("잠김")]
    [SerializeField] private Color _lockedFill = new Color32(0x0A, 0x0F, 0x1C, 0xFF);
    [SerializeField] private Color _lockedOutline = new Color32(0x20, 0x27, 0x3A, 0xFF);
    [SerializeField] private Color _lockedLabel = new Color32(0x5A, 0x64, 0x7A, 0xFF);

    private Action _onClick;
    private bool _locked;

    private void Awake()
    {
        _button.onClick.AddListener(HandleClick);
    }

    // 탭의 라벨과 클릭 콜백을 설정한다. locked면 상호작용을 끄고 잠금 색으로 표시한다.
    public void Setup(string label, bool locked, Action onClick)
    {
        _label.text = label;
        _locked = locked;
        _onClick = onClick;
        _button.interactable = !locked;
        SetSelected(false);
    }

    // 선택 상태에 맞춰 배경·테두리·글자색을 갱신한다. 잠긴 탭은 항상 잠금 색을 유지한다.
    public void SetSelected(bool selected)
    {
        if (_locked)
        {
            Apply(_lockedFill, _lockedOutline, _lockedLabel);
            return;
        }

        if (selected)
            Apply(_selectedFill, _selectedOutline, _selectedLabel);
        else
            Apply(_normalFill, _normalOutline, _normalLabel);
    }

    // 세 그래픽에 색을 한 번에 반영한다.
    private void Apply(Color fill, Color outline, Color label)
    {
        _fill.color = fill;
        _outline.color = outline;
        _label.color = label;
    }

    // 버튼 클릭을 등록된 콜백으로 전달한다.
    private void HandleClick() => _onClick?.Invoke();
}
