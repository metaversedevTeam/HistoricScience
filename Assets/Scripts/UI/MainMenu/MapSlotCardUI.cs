using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 맵 선택 화면의 맵 카드 한 장. 번호·슬롯 이름·저장 시각을 사각형 카드로 보여 주고,
// 누르면 선택(깜빡이는 테두리), 두 번 누르면 바로 시작을 요청한다. 목록에 놓일 때 왼쪽에서 미끄러져 들어온다.
public class MapSlotCardUI : MonoBehaviour, IPointerClickHandler
{
    // 카드 본체 버튼(호버 연출은 같은 오브젝트의 MainMenuButtonMotion이 맡는다)
    [SerializeField] private Button _button;

    // 등장할 때 미끄러질 카드 본체
    [SerializeField] private RectTransform _body;

    // 등장할 때 투명도를 다룰 캔버스 그룹(카드 맨 위 오브젝트)
    [SerializeField] private CanvasGroup _introGroup;

    // 목록 순번 글자
    [SerializeField] private TMP_Text _indexText;

    // 슬롯 이름 글자
    [SerializeField] private TMP_Text _nameText;

    // 저장 시각 글자
    [SerializeField] private TMP_Text _dateText;

    // 선택됐을 때만 켜지는 테두리 묶음
    [SerializeField] private GameObject _selectionFrame;

    // 등장 연출에 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _introDuration = 0.45f;

    // 등장 연출의 처음 위치가 원래 위치에서 떨어진 거리
    [SerializeField] private float _introOffset = -80f;

    // 저장 시각 표시 형식
    private const string DateFormat = "SAVED  {0:yyyy.MM.dd  HH:mm}";

    // 눌러서 선택했을 때 부를 콜백
    private Action<MapSlotCardUI> _onSelect;

    // 두 번 눌러 바로 시작하려 할 때 부를 콜백
    private Action<MapSlotCardUI> _onSubmit;

    // 카드 본체의 원래 위치
    private Vector2 _bodyBase;

    // 등장 연출이 시작되기까지 남은 지연(초)
    private float _introDelay;

    // 등장 연출이 시작된 뒤 지난 시간(초)
    private float _introElapsed;

    // 등장 연출이 재생 중인지 여부
    private bool _isIntroPlaying;

    // 이 카드가 가리키는 맵 저장 슬롯 이름
    public string Slot { get; private set; }

    private void Awake()
    {
        _bodyBase = _body.anchoredPosition;
        _button.onClick.AddListener(HandleClick);
    }

    private void Update()
    {
        HandleIntro();
    }

    // 카드에 표시할 정보와 콜백을 채우고, introDelay초 뒤에 등장 연출을 시작한다.
    public void Setup(int order, string slot, DateTime savedAt, float introDelay, Action<MapSlotCardUI> onSelect, Action<MapSlotCardUI> onSubmit)
    {
        Slot = slot;
        _indexText.text = (order + 1).ToString("00");
        _nameText.text = slot;
        _dateText.text = string.Format(DateFormat, savedAt);
        _onSelect = onSelect;
        _onSubmit = onSubmit;

        SetSelected(false);

        _introDelay = introDelay;
        _introElapsed = 0f;
        _isIntroPlaying = true;
        HandleApplyIntro(0f);
    }

    // 선택 여부에 따라 선택 테두리를 켜고 끈다.
    public void SetSelected(bool selected)
    {
        _selectionFrame.SetActive(selected);
    }

    // 두 번 누르면 바로 시작을 요청한다. (첫 번째 누름은 버튼의 onClick이 선택으로 처리한다)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || eventData.clickCount != 2)
            return;

        _onSubmit?.Invoke(this);
    }

    // 카드를 눌러 선택했음을 알린다.
    private void HandleClick()
    {
        AudioManager.PlayButtonClick();
        _onSelect?.Invoke(this);
    }

    // 지연이 지나면 등장 연출을 진행하고, 끝나면 멈춘다.
    private void HandleIntro()
    {
        if (!_isIntroPlaying)
            return;

        _introElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01((_introElapsed - _introDelay) / _introDuration);
        HandleApplyIntro(t);

        if (t >= 1f)
            _isIntroPlaying = false;
    }

    // 등장 진행도(0~1)를 위치와 투명도에 반영한다.
    private void HandleApplyIntro(float t)
    {
        float eased = 1f - Mathf.Pow(1f - t, 4f);
        _body.anchoredPosition = _bodyBase + new Vector2(_introOffset * (1f - eased), 0f);
        _introGroup.alpha = Mathf.Clamp01(t * 1.6f);
    }
}
