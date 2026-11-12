using System;
using UnityEngine;

// 메인 메뉴가 켜질 때마다 등록된 사각형 요소들을 순서대로 미끄러뜨리고, 늘리고, 나타나게 하는 등장 연출.
// 각 요소는 "처음 상태(밀린 위치·줄어든 크기·투명)"에서 원래 배치로 돌아오며, 맵 화면에서 돌아올 때도 다시 재생된다.
public class MainMenuIntroSequence : MonoBehaviour
{
    // 등장 연출 한 칸. 대상 하나가 언제부터 얼마 동안 어떤 상태에서 원래 배치로 돌아오는지 정한다.
    [Serializable]
    private class IntroStep
    {
        // 움직일 대상
        [SerializeField] private RectTransform _target;

        // 연출이 시작된 뒤 이 칸이 움직이기 시작하기까지의 시간(초)
        [SerializeField, Min(0f)] private float _delay;

        // 원래 배치로 돌아오는 데 걸리는 시간(초)
        [SerializeField, Min(0.01f)] private float _duration = 0.6f;

        // 처음 위치가 원래 위치에서 떨어져 있는 거리
        [SerializeField] private Vector2 _fromOffset;

        // 처음 크기(원래 크기 대비 비율). (1,1)이면 크기 변화가 없다.
        [SerializeField] private Vector2 _fromScale = Vector2.one;

        // 투명한 상태에서 나타나게 할지 여부
        [SerializeField] private bool _fade = true;

        // 원래 위치
        private Vector2 _basePosition;

        // 원래 크기
        private Vector3 _baseScale;

        // 투명도를 다룰 캔버스 그룹(필요할 때 대상에 붙인다)
        private CanvasGroup _group;

        // 원래 배치를 기억해 두고, 나타나기 연출에 쓸 캔버스 그룹을 준비한다.
        public void Cache()
        {
            if (_target == null)
                return;

            _basePosition = _target.anchoredPosition;
            _baseScale = _target.localScale;

            if (_fade)
            {
                _group = _target.GetComponent<CanvasGroup>();
                if (_group == null)
                    _group = _target.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // 연출 시작부터 지난 시간에 맞춰 대상을 처음 상태와 원래 배치 사이로 옮긴다. 다 돌아왔으면 true를 돌려준다.
        public bool Apply(float elapsed)
        {
            if (_target == null)
                return true;

            float t = Mathf.Clamp01((elapsed - _delay) / _duration);
            float eased = 1f - Mathf.Pow(1f - t, 4f);

            _target.anchoredPosition = _basePosition + _fromOffset * (1f - eased);

            Vector2 scale = Vector2.LerpUnclamped(_fromScale, Vector2.one, eased);
            _target.localScale = new Vector3(_baseScale.x * scale.x, _baseScale.y * scale.y, _baseScale.z);

            if (_group != null)
                _group.alpha = Mathf.Clamp01(t * 1.6f);

            return t >= 1f;
        }
    }

    // 순서대로 재생할 등장 연출 목록
    [SerializeField] private IntroStep[] _steps = Array.Empty<IntroStep>();

    // 연출이 끝나기 전에는 버튼을 누를 수 없게 막을 캔버스 그룹(비워 두면 막지 않는다)
    [SerializeField] private CanvasGroup _inputBlocker;

    // 원래 배치를 이미 기억해 두었는지 여부
    private bool _isCached;

    // 지금 연출이 재생 중인지 여부
    private bool _isPlaying;

    // 연출이 시작된 뒤 지난 시간(초)
    private float _elapsed;

    private void OnEnable()
    {
        Play();
    }

    private void Update()
    {
        HandleAdvance();
    }

    // 모든 요소를 처음 상태로 돌려놓고 등장 연출을 처음부터 재생한다.
    public void Play()
    {
        if (!_isCached)
        {
            foreach (IntroStep step in _steps)
                step.Cache();
            _isCached = true;
        }

        _elapsed = 0f;
        _isPlaying = true;
        HandleApplyAll();

        if (_inputBlocker != null)
            _inputBlocker.interactable = false;
    }

    // 재생 중이면 시간을 흘려 모든 요소를 옮기고, 전부 제자리로 돌아오면 입력을 풀어 준다.
    private void HandleAdvance()
    {
        if (!_isPlaying)
            return;

        _elapsed += Time.unscaledDeltaTime;
        if (!HandleApplyAll())
            return;

        _isPlaying = false;
        if (_inputBlocker != null)
            _inputBlocker.interactable = true;
    }

    // 지금 시간에 맞춰 모든 칸을 적용하고, 모든 칸이 끝났는지 돌려준다.
    private bool HandleApplyAll()
    {
        bool isAllDone = true;
        foreach (IntroStep step in _steps)
            isAllDone &= step.Apply(_elapsed);
        return isAllDone;
    }
}
