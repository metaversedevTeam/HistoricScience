using System;
using TMPro;
using UnityEngine;

// 메뉴 화면 사이의 전환 연출. 왼쪽 끝에서 색 띠와 큰 사각형이 차례로 뻗어 나와 화면 왼쪽 영역을 감싸고,
// 다 덮인 순간 바깥에 알려 화면을 바꾸게 한 뒤, 사각형들이 다시 왼쪽으로 접혀 들어가며 바뀐 화면을 드러낸다.
public class MenuWipeTransition : MonoBehaviour
{
    // 전환 연출에 쓰이는 사각형 한 겹. 왼쪽 끝을 기준으로 폭이 늘었다 줄었다 하며, 겹마다 출발 시점을 어긋나게 줄 수 있다.
    [Serializable]
    private class WipeLayer
    {
        // 폭을 바꿀 사각형(왼쪽 끝 앵커·피벗이어야 한다)
        [SerializeField] private RectTransform _target;

        // 다 뻗었을 때의 폭(캔버스 단위)
        [SerializeField, Min(0f)] private float _width = 1180f;

        // 덮기 연출이 시작된 뒤 이 겹이 뻗기 시작하기까지의 시간(초)
        [SerializeField, Min(0f)] private float _inDelay;

        // 걷기 연출이 시작된 뒤 이 겹이 접히기 시작하기까지의 시간(초)
        [SerializeField, Min(0f)] private float _outDelay;

        // 덮기 연출에서 이 겹이 다 뻗기까지 걸리는 전체 시간(초)
        public float InEnd(float duration) => _inDelay + duration;

        // 걷기 연출에서 이 겹이 다 접히기까지 걸리는 전체 시간(초)
        public float OutEnd(float duration) => _outDelay + duration;

        // 덮기 연출의 경과 시간에 맞춰 폭을 늘리고, 지금 뻗은 정도(0~1)를 돌려준다.
        public float ApplyIn(float elapsed, float duration)
        {
            float t = Mathf.Clamp01((elapsed - _inDelay) / duration);
            float eased = 1f - Mathf.Pow(1f - t, 4f);
            SetProgress(eased);
            return eased;
        }

        // 걷기 연출의 경과 시간에 맞춰 폭을 줄이고, 지금 뻗은 정도(0~1)를 돌려준다.
        public float ApplyOut(float elapsed, float duration)
        {
            float t = Mathf.Clamp01((elapsed - _outDelay) / duration);
            float eased = 1f - t * t * t;
            SetProgress(eased);
            return eased;
        }

        // 뻗은 정도(0~1)를 폭으로 바꿔 적용한다.
        public void SetProgress(float progress)
        {
            if (_target != null)
                _target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _width * progress);
        }
    }

    // 전환이 지금 어느 단계인지
    private enum Phase
    {
        Idle,
        Cover,
        Hold,
        Reveal
    }

    // 아래에서 위 순서(나중 것이 위에 그려짐)로 놓인 사각형 겹들. 덮을 때는 아래 겹부터, 걷을 때는 위 겹부터 움직이도록 지연을 준다.
    [SerializeField] private WipeLayer[] _layers = Array.Empty<WipeLayer>();

    // 각 겹이 다 뻗는 데 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _coverDuration = 0.45f;

    // 다 덮인 채로 머무는 시간(초)
    [SerializeField, Min(0f)] private float _holdDuration = 0.18f;

    // 각 겹이 다 접히는 데 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _revealDuration = 0.5f;

    // 전환 중에는 아래 화면의 입력을 막을 캔버스 그룹
    [SerializeField] private CanvasGroup _blocker;

    // 큰 사각형 위에 띄울 다음 화면 이름 글자
    [SerializeField] private TMP_Text _caption;

    // 글자를 담아 큰 사각형이 뻗을 때 함께 미끄러질 묶음
    [SerializeField] private RectTransform _captionGroup;

    // 글자 묶음이 미끄러지는 거리
    [SerializeField] private float _captionSlide = 120f;

    // 전환 중 계속 도는 사각형 장식(비워 두면 돌리지 않는다)
    [SerializeField] private RectTransform _spinner;

    // 장식 사각형의 초당 회전 각도(도)
    [SerializeField] private float _spinnerSpeed = 270f;

    // 전환 연출이 재생 중인지 여부. 재생 중에는 새 전환 요청을 받지 않는 데 쓴다.
    public bool IsPlaying => _phase != Phase.Idle;

    // 지금 단계
    private Phase _phase = Phase.Idle;

    // 지금 단계가 시작된 뒤 지난 시간(초)
    private float _elapsed;

    // 다 덮인 순간 부를 콜백
    private Action _onCovered;

    // 다 걷힌 뒤 부를 콜백
    private Action _onFinished;

    // 글자 묶음의 원래 위치
    private Vector2 _captionBase;

    private void Awake()
    {
        if (_captionGroup != null)
            _captionBase = _captionGroup.anchoredPosition;

        HandleResetIdle();
    }

    private void Update()
    {
        if (_phase == Phase.Idle)
            return;

        _elapsed += Time.unscaledDeltaTime;
        HandleAdvancePhase();
        HandleSpin();
    }

    // 다음 화면 이름을 띄우며 전환을 시작한다. 다 덮인 순간 onCovered에서 화면을 바꾸면 된다.
    public void Play(string caption, Action onCovered, Action onFinished = null)
    {
        if (IsPlaying)
            return;

        if (_caption != null)
            _caption.text = caption;

        _onCovered = onCovered;
        _onFinished = onFinished;
        _phase = Phase.Cover;
        _elapsed = 0f;

        if (_blocker != null)
            _blocker.blocksRaycasts = true;

        HandleApplyCover();
    }

    // 지금 단계의 연출을 적용하고, 단계가 끝났으면 다음 단계로 넘긴다.
    private void HandleAdvancePhase()
    {
        switch (_phase)
        {
            case Phase.Cover:
                if (!HandleApplyCover())
                    return;
                HandleEnterPhase(Phase.Hold);
                _onCovered?.Invoke();
                break;

            case Phase.Hold:
                if (_elapsed >= _holdDuration)
                    HandleEnterPhase(Phase.Reveal);
                break;

            case Phase.Reveal:
                if (!HandleApplyReveal())
                    return;
                HandleResetIdle();
                Action finished = _onFinished;
                _onFinished = null;
                finished?.Invoke();
                break;
        }
    }

    // 다음 단계로 넘어가며 경과 시간을 되돌린다.
    private void HandleEnterPhase(Phase phase)
    {
        _phase = phase;
        _elapsed = 0f;
    }

    // 모든 겹을 덮기 연출 시간에 맞추고, 전부 다 뻗었는지 돌려준다.
    private bool HandleApplyCover()
    {
        float end = 0f;
        float topProgress = 0f;
        foreach (WipeLayer layer in _layers)
        {
            topProgress = layer.ApplyIn(_elapsed, _coverDuration);
            end = Mathf.Max(end, layer.InEnd(_coverDuration));
        }

        HandleApplyCaption(topProgress);
        return _elapsed >= end;
    }

    // 모든 겹을 걷기 연출 시간에 맞추고, 전부 다 접혔는지 돌려준다.
    private bool HandleApplyReveal()
    {
        float end = 0f;
        float topProgress = 0f;
        foreach (WipeLayer layer in _layers)
        {
            topProgress = layer.ApplyOut(_elapsed, _revealDuration);
            end = Mathf.Max(end, layer.OutEnd(_revealDuration));
        }

        HandleApplyCaption(topProgress);
        return _elapsed >= end;
    }

    // 맨 위 겹이 뻗은 정도에 맞춰 글자 묶음을 왼쪽에서 따라 들어오게 한다.
    private void HandleApplyCaption(float progress)
    {
        if (_captionGroup != null)
            _captionGroup.anchoredPosition = _captionBase + new Vector2(-_captionSlide * (1f - progress), 0f);
    }

    // 장식 사각형을 돌린다.
    private void HandleSpin()
    {
        if (_spinner != null)
            _spinner.Rotate(0f, 0f, -_spinnerSpeed * Time.unscaledDeltaTime);
    }

    // 모든 겹을 접어 두고 입력 막기를 푼다.
    private void HandleResetIdle()
    {
        _phase = Phase.Idle;
        _elapsed = 0f;
        _onCovered = null;

        foreach (WipeLayer layer in _layers)
            layer.SetProgress(0f);

        if (_blocker != null)
            _blocker.blocksRaycasts = false;
    }
}
