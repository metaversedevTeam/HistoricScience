using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 치트 관리 목록에 놓이는 스위치 한 줄. 켜짐·꺼짐에 따라 배경과 트랙 색, 손잡이 위치가 달라진다.
// 일회성 치트처럼 더 이상 누를 수 없게 된 줄은 켜진 모습 그대로 흐려지며 입력을 받지 않는다.
public class CheatToggleRowUI : MonoBehaviour
{
    [Header("구성")]
    [SerializeField] private Button _button;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _background;
    // 꺼져 있는 줄의 아래쪽에만 보이는 구분선
    [SerializeField] private Image _separator;

    [Header("스위치")]
    [SerializeField] private Image _track;
    [SerializeField] private Image _knob;
    // 손잡이가 트랙 중앙을 기준으로 좌우 끝까지 움직이는 거리
    [SerializeField] private float _knobTravel = 18f;

    [Header("문구")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;

    [Header("색상")]
    [SerializeField] private Color _onBackground = new Color32(0x10, 0x0C, 0x27, 0xFF);
    [SerializeField] private Color _offBackground = new Color32(0x0B, 0x08, 0x1F, 0x00);
    [SerializeField] private Color _onTrack = new Color32(0x00, 0xE5, 0xFF, 0xFF);
    [SerializeField] private Color _offTrack = new Color32(0x33, 0x41, 0x55, 0xFF);
    [SerializeField] private Color _onKnob = new Color32(0x0B, 0x08, 0x1F, 0xFF);
    [SerializeField] private Color _offKnob = new Color32(0x94, 0xA3, 0xB8, 0xFF);
    // 더 이상 누를 수 없는 줄을 흐리게 보이도록 낮추는 투명도
    [SerializeField] private float _lockedAlpha = 0.45f;

    // 줄을 눌렀을 때 호출할 콜백. 켜고 끄는 판단은 치트 관리 UI가 한다.
    private Action _onClick;

    private void Awake()
    {
        _button.onClick.AddListener(HandleClick);
    }

    // 줄에 표시할 문구와 눌렀을 때 호출할 콜백을 지정한다.
    public void Setup(string title, string description, Action onClick)
    {
        _titleText.text = title;
        _descriptionText.text = description;
        _onClick = onClick;
    }

    // 스위치의 켜짐·꺼짐 표시를 갱신한다.
    public void SetOn(bool isOn)
    {
        _background.color = isOn ? _onBackground : _offBackground;
        _track.color = isOn ? _onTrack : _offTrack;
        _knob.color = isOn ? _onKnob : _offKnob;

        Vector2 knobPosition = _knob.rectTransform.anchoredPosition;
        knobPosition.x = isOn ? _knobTravel : -_knobTravel;
        _knob.rectTransform.anchoredPosition = knobPosition;

        _separator.enabled = !isOn;
    }

    // 줄을 누를 수 있는지 지정한다. 잠긴 줄은 흐려지고 입력도 받지 않는다.
    public void SetInteractable(bool interactable)
    {
        _button.interactable = interactable;
        _canvasGroup.alpha = interactable ? 1f : _lockedAlpha;
        _canvasGroup.blocksRaycasts = interactable;
    }

    private void HandleClick() => _onClick?.Invoke();
}
