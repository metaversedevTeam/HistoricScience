using UnityEngine;
using UnityEngine.UI;

// 메뉴 배경의 사각형을 끝없이 움직이는 모션. 제자리 기준으로 떠다니고, 회전하고, 크기·투명도가 숨 쉬듯 변한다.
// 게임 일시정지와 무관하게 돌도록 실제 경과 시간을 쓴다.
public class UIRectFloatMotion : MonoBehaviour
{
    // 떠다니는 폭(캔버스 단위). x·y가 서로 다른 주기로 흔들려 8자에 가까운 궤적을 그린다.
    [SerializeField] private Vector2 _driftAmplitude = new Vector2(16f, 10f);

    // 한 번 떠다니는 데 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _driftPeriod = 9f;

    // 초당 회전 각도(도). 음수면 시계 방향
    [SerializeField] private float _rotationSpeed;

    // 크기가 원래 크기에서 벗어나는 비율(0이면 변하지 않음)
    [SerializeField, Range(0f, 1f)] private float _scalePulse;

    // 투명도가 원래 값에서 줄어드는 비율(0이면 변하지 않음)
    [SerializeField, Range(0f, 1f)] private float _alphaPulse;

    // 크기·투명도가 한 번 숨 쉬는 데 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _pulsePeriod = 4f;

    // 여러 사각형이 같은 박자로 움직이지 않도록 주는 시작 위상(0~1)
    [SerializeField, Range(0f, 1f)] private float _phase;

    // 움직일 사각형
    private RectTransform _rect;

    // 투명도를 바꿀 그래픽(없으면 투명도 변화는 건너뛴다)
    private Graphic _graphic;

    // 움직임의 기준이 되는 처음 위치
    private Vector2 _basePosition;

    // 크기 변화의 기준이 되는 처음 크기
    private Vector3 _baseScale;

    // 투명도 변화의 기준이 되는 처음 투명도
    private float _baseAlpha;

    // 정렬을 시작한 순간의 위치
    private Vector2 _settleFromPosition;

    // 정렬을 시작한 순간의 회전 각도(도)
    private float _settleFromAngle;

    // 정렬이 끝났을 때의 회전 각도(도). 시작 각도에서 가장 가까운 직각이다.
    private float _settleToAngle;

    // 정렬을 시작한 순간의 크기
    private Vector3 _settleFromScale;

    // 정렬을 시작한 순간의 투명도
    private float _settleFromAlpha;

    // 떠다니는 중심이 되는 위치. 바꾸면 그 자리를 중심으로 계속 떠다닌다.
    public Vector2 BasePosition
    {
        get => _basePosition;
        set
        {
            _basePosition = value;

            // 떠다니지 않는 사각형은 Update가 위치를 건드리지 않으므로 바로 옮겨 둔다.
            if (_driftAmplitude == Vector2.zero && _rect != null)
                _rect.anchoredPosition = value;
        }
    }

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _graphic = GetComponent<Graphic>();
        _basePosition = _rect.anchoredPosition;
        _baseScale = _rect.localScale;
        _baseAlpha = _graphic != null ? _graphic.color.a : 1f;
    }

    private void Update()
    {
        float time = Time.unscaledTime;
        HandleDrift(time);
        HandleRotate();
        HandlePulse(time);
    }

    // 움직임을 멈추고, 지금 자세에서 정렬된 자세(기준 위치·크기·투명도, 가장 가까운 직각 방향)로 돌아갈 준비를 한다.
    public void BeginSettle()
    {
        enabled = false;

        _settleFromPosition = _rect.anchoredPosition;
        _settleFromAngle = _rect.localEulerAngles.z;
        _settleToAngle = Mathf.Round(_settleFromAngle / 90f) * 90f;
        _settleFromScale = _rect.localScale;
        _settleFromAlpha = _graphic != null ? _graphic.color.a : 1f;
    }

    // BeginSettle 이후, 정렬된 자세로 돌아간 정도(0~1)를 적용한다.
    public void ApplySettle(float progress)
    {
        _rect.anchoredPosition = Vector2.LerpUnclamped(_settleFromPosition, _basePosition, progress);
        _rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(_settleFromAngle, _settleToAngle, progress));
        _rect.localScale = Vector3.LerpUnclamped(_settleFromScale, _baseScale, progress);

        if (_graphic != null)
        {
            Color c = _graphic.color;
            c.a = Mathf.Lerp(_settleFromAlpha, _baseAlpha, progress);
            _graphic.color = c;
        }
    }

    // 처음 위치를 중심으로 x·y를 서로 다른 박자의 사인파로 흔든다.
    private void HandleDrift(float time)
    {
        if (_driftAmplitude == Vector2.zero)
            return;

        float angle = (time / _driftPeriod + _phase) * Mathf.PI * 2f;
        Vector2 offset = new Vector2(Mathf.Sin(angle) * _driftAmplitude.x, Mathf.Sin(angle * 2f) * _driftAmplitude.y);
        _rect.anchoredPosition = _basePosition + offset;
    }

    // 화면 축 기준으로 일정한 속도로 돌린다.
    private void HandleRotate()
    {
        if (Mathf.Approximately(_rotationSpeed, 0f))
            return;

        _rect.Rotate(0f, 0f, _rotationSpeed * Time.unscaledDeltaTime);
    }

    // 크기와 투명도를 같은 박자로 키웠다 줄였다 한다.
    private void HandlePulse(float time)
    {
        if (_scalePulse <= 0f && _alphaPulse <= 0f)
            return;

        float wave = 0.5f + 0.5f * Mathf.Sin((time / _pulsePeriod + _phase) * Mathf.PI * 2f);

        if (_scalePulse > 0f)
            _rect.localScale = _baseScale * (1f + _scalePulse * (wave - 0.5f));

        if (_alphaPulse > 0f && _graphic != null)
        {
            Color c = _graphic.color;
            c.a = _baseAlpha * (1f - _alphaPulse * (1f - wave));
            _graphic.color = c;
        }
    }
}
