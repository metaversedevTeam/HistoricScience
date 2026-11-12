using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 메인 메뉴의 사각형 버튼 연출. 마우스를 올리거나 선택하면 강조색 막대가 왼쪽에서 채워지고,
// 글자가 오른쪽으로 밀리며 어두운 색으로 바뀌고, 오른쪽 끝 사각형이 앞으로 튀어나온다. 누르면 살짝 눌리는 느낌을 준다.
// 버튼이 잠겨 있으면(interactable=false) 반응하지 않고 흐리게 보인다.
[RequireComponent(typeof(Button))]
public class MainMenuButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
{
    // 강조색으로 왼쪽부터 채워질 막대(피벗이 왼쪽이어야 한다)
    [SerializeField] private RectTransform _fill;

    // 오른쪽으로 밀릴 글자 묶음
    [SerializeField] private RectTransform _content;

    // 색이 바뀔 버튼 이름 글자
    [SerializeField] private TMP_Text _label;

    // 색이 바뀔 번호 글자
    [SerializeField] private TMP_Text _index;

    // 강조될 때 앞으로 튀어나오며 도는 오른쪽 끝 사각형
    [SerializeField] private RectTransform _marker;

    // 잠겼을 때 흐리게 만들 캔버스 그룹
    [SerializeField] private CanvasGroup _group;

    // 평소 글자 색
    [SerializeField] private Color _normalTextColor = new Color(0.92f, 0.94f, 1f, 1f);

    // 강조됐을 때 글자 색(채워진 막대 위에서 읽히는 어두운 색)
    [SerializeField] private Color _highlightTextColor = new Color(0.02f, 0.016f, 0.05f, 1f);

    // 강조됐을 때 글자가 밀리는 거리
    [SerializeField] private float _contentShift = 28f;

    // 강조됐을 때 오른쪽 끝 사각형이 밀리는 거리
    [SerializeField] private float _markerShift = 18f;

    // 강조 상태로 바뀌는 속도(클수록 빠르다)
    [SerializeField, Min(0.1f)] private float _response = 14f;

    // 잠겼을 때 투명도
    [SerializeField, Range(0f, 1f)] private float _lockedAlpha = 0.35f;

    // 연결된 버튼
    private Button _button;

    // 번호 글자의 평소 색(강조색)
    private Color _indexBaseColor;

    // 오른쪽 끝 사각형의 그래픽(채워진 막대에 묻히지 않도록 강조될 때 어두운 색으로 바꾼다)
    private Graphic _markerGraphic;

    // 오른쪽 끝 사각형의 평소 색(강조색)
    private Color _markerBaseColor;

    // 글자 묶음의 원래 위치
    private Vector2 _contentBase;

    // 오른쪽 끝 사각형의 원래 위치
    private Vector2 _markerBase;

    // 마우스가 올라와 있는지 여부
    private bool _isHovered;

    // 키보드·패드로 선택되어 있는지 여부
    private bool _isSelected;

    // 눌려 있는지 여부
    private bool _isPressed;

    // 지금 강조 정도(0~1)
    private float _highlight;

    // 지금 눌림 정도(0~1)
    private float _press;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.transition = Selectable.Transition.None;

        if (_index != null)
            _indexBaseColor = _index.color;
        if (_content != null)
            _contentBase = _content.anchoredPosition;
        if (_marker != null)
        {
            _markerBase = _marker.anchoredPosition;
            _markerGraphic = _marker.GetComponent<Graphic>();
            if (_markerGraphic != null)
                _markerBaseColor = _markerGraphic.color;
        }
    }

    private void OnDisable()
    {
        _isHovered = false;
        _isSelected = false;
        _isPressed = false;
        _highlight = 0f;
        _press = 0f;
        HandleApply();
    }

    private void Update()
    {
        HandleAnimate();
        HandleApply();
    }

    // 마우스가 올라오면 강조한다.
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
    }

    // 마우스가 벗어나면 강조를 푼다.
    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        _isPressed = false;
    }

    // 키보드·패드로 선택되면 강조한다.
    public void OnSelect(BaseEventData eventData)
    {
        _isSelected = true;
    }

    // 선택이 풀리면 강조를 푼다.
    public void OnDeselect(BaseEventData eventData)
    {
        _isSelected = false;
    }

    // 누르기 시작하면 눌림 연출을 켠다.
    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
    }

    // 손을 떼면 눌림 연출을 끈다.
    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
    }

    // 목표 강조·눌림 정도를 향해 부드럽게 따라간다.
    private void HandleAnimate()
    {
        bool isInteractable = _button.interactable;
        float targetHighlight = isInteractable && (_isHovered || _isSelected) ? 1f : 0f;
        float targetPress = isInteractable && _isPressed ? 1f : 0f;

        float k = 1f - Mathf.Exp(-_response * Time.unscaledDeltaTime);
        _highlight = Mathf.Lerp(_highlight, targetHighlight, k);
        _press = Mathf.Lerp(_press, targetPress, k * 1.5f);

        if (_group != null)
            _group.alpha = Mathf.Lerp(_group.alpha, isInteractable ? 1f : _lockedAlpha, k);
    }

    // 지금 강조·눌림 정도를 막대·글자·사각형·크기에 반영한다.
    private void HandleApply()
    {
        float h = _highlight;

        if (_fill != null)
            _fill.localScale = new Vector3(h, 1f, 1f);

        if (_content != null)
            _content.anchoredPosition = _contentBase + new Vector2(_contentShift * h, 0f);

        if (_label != null)
            _label.color = Color.Lerp(_normalTextColor, _highlightTextColor, h);

        if (_index != null)
            _index.color = Color.Lerp(_indexBaseColor, _highlightTextColor, h);

        if (_marker != null)
        {
            _marker.anchoredPosition = _markerBase + new Vector2(_markerShift * h, 0f);
            _marker.localRotation = Quaternion.Euler(0f, 0f, 45f * h);
        }

        if (_markerGraphic != null)
            _markerGraphic.color = Color.Lerp(_markerBaseColor, _highlightTextColor, h);

        float scale = 1f - 0.03f * _press;
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
