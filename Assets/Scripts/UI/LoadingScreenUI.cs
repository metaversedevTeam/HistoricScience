using TMPro;
using UnityEngine;

// 맵을 연 뒤 등록된 청크가 전부 로딩될 때까지 화면 전체를 덮는 로딩 화면.
// 가운데의 지구 아이콘을 천천히 회전시키고, 아래쪽 프로그래스 바로 로딩 진행도를 보여 주며, 로딩이 끝날 때까지 아래쪽 UI로 가는 입력을 막는다.
// UIManager가 관리하는 UI이므로 UIManager.Instance.OpenUI로 띄우고 Close로 걷는다. 닫기 연출은 페이드 아웃이다.
public class LoadingScreenUI : OpenableUIBase
{
    // 창처럼 여닫는 UI가 아니므로 열기·닫기 효과음을 내지 않는다
    protected override bool UsesWindowSfx => false;

    // 로딩이 끝나기 전에 Esc로 걷히면 덜 만들어진 월드가 보이므로 Esc로는 닫히지 않는다
    public override bool ClosesOnEscape => false;

    // 페이드 아웃과 입력 차단에 쓰는 캔버스 그룹
    [SerializeField] private CanvasGroup _canvasGroup;

    // 천천히 회전시킬 가운데 지구 아이콘
    [SerializeField] private RectTransform _globe;

    // 지구 아이콘의 초당 회전 각도(도). 음수면 시계 방향으로 돈다.
    [SerializeField] private float _globeRotationSpeed = -10f;

    // 로딩이 끝난 뒤 화면이 완전히 사라지기까지 걸리는 시간(초). 0이면 즉시 사라진다.
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.4f;

    // 진행도만큼 채워지는 막대. 트랙(부모) 기준 오른쪽 앵커를 옮겨 비율을 표현한다.
    [SerializeField] private RectTransform _progressFill;

    // 진행도를 퍼센트로 보여 주는 문구
    [SerializeField] private TextMeshProUGUI _progressPercentText;

    // 표시 중인 진행도가 목표 진행도를 따라가는 속도(초당 비율). 0이면 즉시 따라간다.
    [SerializeField, Min(0f)] private float _progressFillSpeed = 1.5f;

    // 지금 페이드 아웃 중인지 여부
    private bool _isFadingOut;

    // 페이드 아웃이 시작된 뒤 지난 시간(초)
    private float _fadeElapsed;

    // 바깥에서 알려 준 목표 진행도(0~1)
    private float _targetProgress;

    // 지금 화면에 표시 중인 진행도(0~1). 목표 진행도를 부드럽게 따라간다.
    private float _displayedProgress;

    private void Awake()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Update()
    {
        HandleRotateGlobe();
        HandleFillProgress();
        HandleFadeOut();
    }

    // 열기 연출 훅 — 진행도를 0으로 되돌리고 연출 없이 바로 완전히 불투명한 상태로 띄운다.
    protected override void PlayOpenTransition()
    {
        _isFadingOut = false;
        _fadeElapsed = 0f;

        _targetProgress = 0f;
        _displayedProgress = 0f;
        HandleApplyProgress();

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
        }

        FinishOpen();
    }

    // 프로그래스 바에 표시할 로딩 진행도(0~1)를 정한다. 막대는 이 값을 향해 부드럽게 채워진다.
    public void SetProgress(float progress)
    {
        _targetProgress = Mathf.Clamp01(progress);
    }

    // 닫기 연출 훅 — 페이드 아웃 시간 동안 서서히 사라진 뒤 닫기를 완료한다. (Close(true)는 이 연출 없이 즉시 닫힌다)
    protected override void PlayCloseTransition()
    {
        // 남은 채움 연출을 기다리지 않고 사라지므로, 마지막으로 알려 준 진행도를 즉시 반영해 둔다.
        _displayedProgress = _targetProgress;
        HandleApplyProgress();

        if (_fadeOutDuration <= 0f || _canvasGroup == null)
        {
            FinishClose();
            return;
        }

        _isFadingOut = true;
        _fadeElapsed = 0f;

        // 사라지는 동안에는 아래쪽 UI를 바로 조작할 수 있도록 입력 차단만 먼저 풀어 준다.
        _canvasGroup.blocksRaycasts = false;
    }

    // 진행 중인 페이드 아웃을 멈춘다.
    protected override void StopTransition()
    {
        _isFadingOut = false;
    }

    // 풀로 돌아가기 전에 완전히 꺼진 상태(투명, 입력 차단 해제)로 정리한다.
    protected override void OnReturnToPool()
    {
        _isFadingOut = false;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    // 지구 아이콘을 초당 지정한 각도만큼 화면 축 기준으로 돌린다. 로딩 중 프레임이 밀려도 속도가 일정하도록 실제 경과 시간을 쓴다.
    private void HandleRotateGlobe()
    {
        if (_globe == null)
            return;

        _globe.Rotate(0f, 0f, _globeRotationSpeed * Time.unscaledDeltaTime);
    }

    // 표시 중인 진행도를 목표 진행도 쪽으로 옮기고, 그 결과를 막대와 퍼센트 문구에 반영한다.
    private void HandleFillProgress()
    {
        if (Mathf.Approximately(_displayedProgress, _targetProgress))
            return;

        _displayedProgress = _progressFillSpeed <= 0f
            ? _targetProgress
            : Mathf.MoveTowards(_displayedProgress, _targetProgress, _progressFillSpeed * Time.unscaledDeltaTime);

        HandleApplyProgress();
    }

    // 표시 중인 진행도를 채움 막대의 너비와 퍼센트 문구에 적용한다.
    private void HandleApplyProgress()
    {
        // 트랙 기준 오른쪽 앵커만 옮기므로, 트랙의 실제 픽셀 너비를 몰라도 비율대로 채워진다.
        if (_progressFill != null)
            _progressFill.anchorMax = new Vector2(_displayedProgress, 1f);

        if (_progressPercentText != null)
            _progressPercentText.text = $"{Mathf.RoundToInt(_displayedProgress * 100f)}%";
    }

    // 페이드 아웃이 시작되었으면 경과 시간만큼 알파를 낮추고, 다 사라지면 닫기를 완료한다.
    private void HandleFadeOut()
    {
        if (!_isFadingOut)
            return;

        _fadeElapsed += Time.unscaledDeltaTime;
        _canvasGroup.alpha = Mathf.Clamp01(1f - _fadeElapsed / _fadeOutDuration);

        if (_fadeElapsed >= _fadeOutDuration)
            FinishClose();
    }
}
