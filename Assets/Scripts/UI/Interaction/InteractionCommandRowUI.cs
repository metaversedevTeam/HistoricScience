using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상호작용 명령 목록의 한 줄. 메인 메뉴 버튼처럼 사각형으로 이루어지며, 나타날 때 강조색 막대가 왼쪽에서 덮었다가
// 오른쪽으로 걷히면서(지층 와이프) 글자가 드러나고, 오른쪽 끝 사각형이 45도 돌며 튀어나온다.
// 선택되면 메인 메뉴 버튼처럼 강조색 막대가 왼쪽부터 채워지고 글자가 오른쪽으로 밀리며 어두운 색으로 바뀐다.
public class InteractionCommandRowUI : MonoBehaviour
{
    // 줄 전체를 덮었다 걷히는 강조색 막대
    [SerializeField] private RectTransform _wipe;

    // 선택되었을 때 왼쪽부터 채워지는 강조색 막대(피벗이 왼쪽이어야 한다)
    [SerializeField] private RectTransform _highlightFill;

    // 오른쪽으로 밀려 들어올 글자 묶음
    [SerializeField] private RectTransform _content;

    // 명령 번호 글자(01, 02 …)
    [SerializeField] private TMP_Text _index;

    // 명령 이름 글자
    [SerializeField] private TMP_Text _label;

    // 오른쪽 끝에서 돌며 튀어나오는 사각형
    [SerializeField] private RectTransform _marker;

    // 줄 전체의 투명도
    [SerializeField] private CanvasGroup _group;

    // 막대가 덮었다 걷히는 데 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _wipeDuration = 0.32f;

    // 글자가 밀려 들어오는 거리
    [SerializeField] private float _contentShift = -18f;

    // 사각형이 튀어나오는 거리
    [SerializeField] private float _markerShift = 10f;

    // 선택되었을 때 글자가 오른쪽으로 밀리는 거리
    [SerializeField] private float _highlightContentShift = 10f;

    // 선택되었을 때 사각형이 오른쪽으로 밀리는 거리
    [SerializeField] private float _highlightMarkerShift = 4f;

    // 선택되었을 때 글자·사각형 색(채워진 막대 위에서 읽히는 잉크색)
    [SerializeField] private Color _highlightInkColor = new Color(0.165f, 0.137f, 0.11f, 1f);

    // 선택 상태로 바뀌는 속도(클수록 빠르다)
    [SerializeField, Min(0.1f)] private float _highlightResponse = 16f;

    // 글자 묶음의 원래 위치
    private Vector2 _contentBase;

    // 오른쪽 끝 사각형의 원래 위치
    private Vector2 _markerBase;

    // 번호·이름 글자와 사각형의 평소 색
    private Color _indexBaseColor;
    private Color _labelBaseColor;
    private Color _markerBaseColor;

    // 오른쪽 끝 사각형의 그래픽
    private Graphic _markerGraphic;

    // 원래 위치와 색을 한 번만 기억했는지 여부
    private bool _isBaseCached;

    // 이 줄의 연출이 시작되는 시각(목록 연출 시작 기준, 초)
    private float _delay;

    // 선택되어 있는지 여부
    private bool _isSelected;

    // 지금 선택 강조 정도(0~1)
    private float _highlight;

    // 줄에 표시할 번호와 명령 이름을 채우고, 목록 연출 시작 뒤 몇 초 후에 나타날지 정한다. 선택 강조는 처음부터 다시 시작한다.
    public void Bind(int index, string commandName, float delay)
    {
        CacheBase();

        _index.text = (index + 1).ToString("00");
        _label.text = commandName;
        _delay = delay;
        _highlight = 0f;
        Apply(0f);
    }

    // 이 줄을 선택하거나 선택을 푼다. 강조 연출은 Apply를 부를 때마다 조금씩 따라간다.
    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;
    }

    // 목록 연출 시작 뒤 지난 시간에 맞춰 등장 연출과 선택 강조 연출을 함께 반영한다.
    public void Apply(float elapsed)
    {
        CacheBase();

        float t = Mathf.Clamp01((elapsed - _delay) / _wipeDuration);

        float k = 1f - Mathf.Exp(-_highlightResponse * Time.unscaledDeltaTime);
        _highlight = Mathf.Lerp(_highlight, _isSelected ? 1f : 0f, k);

        float reveal = EaseOut(Mathf.Clamp01(t * 2f - 0.8f));
        ApplyWipe(t);
        ApplyContent(reveal, _highlight * reveal);
        ApplyMarker(t, _highlight * reveal);
    }

    // 앞 절반은 막대가 왼쪽부터 줄을 덮고, 뒤 절반은 막대가 오른쪽으로 걷히며 글자를 드러낸다.
    private void ApplyWipe(float t)
    {
        float cover = EaseOut(Mathf.Clamp01(t * 2f));
        float uncover = EaseInOut(Mathf.Clamp01(t * 2f - 1f));
        _wipe.anchorMin = new Vector2(uncover, 0f);
        _wipe.anchorMax = new Vector2(cover, 1f);
        _group.alpha = Mathf.Clamp01(t * 4f);
    }

    // 글자를 드러내며 밀어 넣고, 선택 강조 정도만큼 막대를 채우고 글자를 오른쪽으로 밀며 잉크색으로 바꾼다.
    private void ApplyContent(float reveal, float h)
    {
        _highlightFill.localScale = new Vector3(h, 1f, 1f);
        _content.anchoredPosition = _contentBase + new Vector2(_contentShift * (1f - reveal) + _highlightContentShift * h, 0f);

        Color indexColor = Color.Lerp(_indexBaseColor, _highlightInkColor, h);
        indexColor.a = reveal;
        _index.color = indexColor;

        Color labelColor = Color.Lerp(_labelBaseColor, _highlightInkColor, h);
        labelColor.a = reveal;
        _label.color = labelColor;
    }

    // 오른쪽 끝 사각형을 마름모로 돌리며 튀어나오게 하고, 선택 강조 정도만큼 더 밀며 반듯한 사각형으로 되돌리고 잉크색으로 바꾼다.
    private void ApplyMarker(float t, float h)
    {
        float pop = EaseOutBack(Mathf.Clamp01(t * 1.6f - 0.6f));
        _marker.anchoredPosition = _markerBase + new Vector2(_markerShift * (1f - pop) + _highlightMarkerShift * h, 0f);
        _marker.localRotation = Quaternion.Euler(0f, 0f, 45f * pop * (1f - h));
        _marker.localScale = Vector3.one * pop;

        if (_markerGraphic != null)
            _markerGraphic.color = Color.Lerp(_markerBaseColor, _highlightInkColor, h);
    }

    // 글자 묶음·사각형의 원래 위치와 글자·사각형의 평소 색을 처음 한 번만 기억한다.
    private void CacheBase()
    {
        if (_isBaseCached) return;

        _contentBase = _content.anchoredPosition;
        _markerBase = _marker.anchoredPosition;
        _indexBaseColor = _index.color;
        _labelBaseColor = _label.color;
        _markerGraphic = _marker.GetComponent<Graphic>();
        if (_markerGraphic != null)
            _markerBaseColor = _markerGraphic.color;
        _isBaseCached = true;
    }

    // 끝으로 갈수록 느려지는 곡선
    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    // 처음과 끝이 느린 곡선
    private static float EaseInOut(float t) => t * t * (3f - 2f * t);

    // 목표를 살짝 지나쳤다 돌아오는 곡선
    private static float EaseOutBack(float t)
    {
        const float k = 1.70158f;
        float u = t - 1f;
        return 1f + u * u * ((k + 1f) * u + k);
    }
}
