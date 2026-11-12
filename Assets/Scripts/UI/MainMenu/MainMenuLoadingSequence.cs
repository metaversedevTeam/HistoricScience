using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// 메인 메뉴에서 인게임 씬으로 넘어가는 로딩 연출. 씬을 비동기로 불러오는 동안 왼쪽의 조작 UI를 흐리며 왼쪽으로 밀어내고
// 오른쪽 장식을 화면 가운데로 옮긴 뒤, 메뉴 캔버스를 씬 전환 너머로 넘겨 인게임 씬의 로딩 화면으로 그대로 이어 쓴다.
// 인게임 씬이 로딩을 마치면(ISceneLoadingScreen.Finish) 서서히 사라지고 스스로 파괴된다.
[RequireComponent(typeof(Canvas))]
public class MainMenuLoadingSequence : MonoBehaviour, ISceneLoadingScreen
{
    [Header("퇴장하는 조작 UI")]
    // 흐려지며 왼쪽으로 사라질 조작 UI 묶음들(꺼져 있는 것은 건너뛴다)
    [SerializeField] private RectTransform[] _exitTargets = Array.Empty<RectTransform>();

    // 조작 UI가 왼쪽으로 밀려나는 거리
    [SerializeField] private float _exitDistance = 360f;

    // 조작 UI 하나가 다 사라지기까지 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _exitDuration = 0.55f;

    // 조작 UI 묶음끼리 퇴장 시작 시점의 간격(초)
    [SerializeField, Min(0f)] private float _exitStagger = 0.06f;

    [Header("가운데로 오는 장식")]
    // 화면 가운데로 옮길 장식 묶음. 이 묶음의 위치가 도착 위치에 오도록 이동 거리를 정한다.
    // 조각 목록이 비어 있으면 묶음 자체를 옮기고, 조각이 있으면 묶음은 그대로 두고 조각들만 옮긴다.
    [SerializeField] private RectTransform _centerTarget;

    // 따로따로 옮길 장식 조각들. 중심 조각부터 바깥 조각 순서로 넣으면 그 순서대로 출발한다.
    [SerializeField] private RectTransform[] _centerParts = Array.Empty<RectTransform>();

    // 장식 묶음이 도착할 위치(캔버스 가운데 기준)
    [SerializeField] private Vector2 _centerPosition = Vector2.zero;

    // 연출 시작 뒤 첫 조각이 움직이기 시작하기까지의 시간(초)
    [SerializeField, Min(0f)] private float _centerDelay = 0.1f;

    // 조각 하나가 가운데에 도착하기까지 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _centerDuration = 0.9f;

    // 조각끼리 출발 시점의 간격(초)
    [SerializeField, Min(0f)] private float _centerStagger = 0.08f;

    [Header("진행도 표시")]
    // 장식 아래에 나타나는 진행도 묶음. 평소에는 투명하다.
    [SerializeField] private CanvasGroup _progressGroup;

    // 진행도만큼 채워지는 막대. 트랙(부모) 기준 오른쪽 앵커를 옮겨 비율을 표현한다.
    [SerializeField] private RectTransform _progressFill;

    // 진행도를 퍼센트로 보여 주는 문구
    [SerializeField] private TMP_Text _progressPercentText;

    // 연출 시작 뒤 진행도 묶음이 나타나기 시작하기까지의 시간(초)
    [SerializeField, Min(0f)] private float _progressFadeDelay = 0.5f;

    // 진행도 묶음이 다 나타나기까지 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _progressFadeDuration = 0.4f;

    // 표시 중인 진행도가 목표 진행도를 따라가는 속도(초당 비율). 0이면 즉시 따라간다.
    [SerializeField, Min(0f)] private float _progressFillSpeed = 1.5f;

    // 전체 진행도 중 씬 불러오기가 차지하는 몫. 나머지는 인게임 씬이 알려 주는 진행도에 쓴다.
    [SerializeField, Range(0f, 1f)] private float _sceneLoadShare = 0.3f;

    [Header("씬 전환")]
    // 씬 전환 너머로 넘어간 뒤 인게임 UI보다 위에 그려지도록 바꿀 캔버스 정렬 순서
    [SerializeField] private int _handoffSortingOrder = 1000;

    // 인게임 씬 로딩이 끝난 뒤 화면이 완전히 사라지기까지 걸리는 시간(초)
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.5f;

    // 이 연출이 붙은 메뉴 캔버스
    private Canvas _canvas;

    // 캔버스 전체의 투명도와 입력을 다룰 그룹
    private CanvasGroup _rootGroup;

    // 퇴장 대상마다 붙여 둔 투명도 그룹(대상과 같은 순서)
    private CanvasGroup[] _exitGroups;

    // 퇴장 대상들의 원래 위치(대상과 같은 순서)
    private Vector2[] _exitBasePositions;

    // 실제로 옮길 장식 조각들(조각 목록이 비어 있으면 묶음 하나)
    private RectTransform[] _movingParts;

    // 조각마다 붙어 있는 떠다니기 모션(없으면 null, 조각과 같은 순서)
    private UIRectFloatMotion[] _movingFloats;

    // 조각들의 출발 위치(조각과 같은 순서)
    private Vector2[] _movingStartPositions;

    // 모든 조각이 함께 옮겨질 거리
    private Vector2 _centerDelta;

    // 로딩 연출이 이미 시작되었는지 여부
    private bool _isRunning;

    // 목표 진행도(0~1)
    private float _targetProgress;

    // 지금 화면에 표시 중인 진행도(0~1)
    private float _displayedProgress;

    // 지금 사라지는 중인지 여부
    private bool _isFadingOut;

    // 사라지기 시작한 뒤 지난 시간(초)
    private float _fadeElapsed;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _rootGroup = GetComponent<CanvasGroup>();
        if (_rootGroup == null)
            _rootGroup = gameObject.AddComponent<CanvasGroup>();

        if (_progressGroup != null)
            _progressGroup.alpha = 0f;

        HandleApplyProgress();
    }

    private void Update()
    {
        HandleFillProgress();
        HandleFadeOut();
    }

    // 지정한 씬을 비동기로 불러오며 로딩 연출을 시작한다. 이미 시작했으면 무시한다.
    public void Begin(string sceneName)
    {
        if (_isRunning)
            return;

        _isRunning = true;
        StartCoroutine(HandleLoadRoutine(sceneName));
    }

    // 인게임 씬 쪽 진행도(0~1)를 씬 불러오기 몫 뒤의 남은 구간에 대응시켜 목표 진행도로 삼는다.
    public void SetProgress(float progress)
    {
        HandleSetTargetProgress(_sceneLoadShare + Mathf.Clamp01(progress) * (1f - _sceneLoadShare));
    }

    // 진행도를 끝까지 채운 상태로 서서히 사라지기 시작한다. 다 사라지면 스스로 파괴된다.
    public void Finish()
    {
        if (_isFadingOut)
            return;

        _targetProgress = 1f;
        _displayedProgress = 1f;
        HandleApplyProgress();

        _isFadingOut = true;
        _fadeElapsed = 0f;

        // 사라지는 동안에는 아래쪽 인게임 UI를 바로 조작할 수 있도록 입력 차단만 먼저 풀어 준다.
        _rootGroup.blocksRaycasts = false;
    }

    // 씬을 불러오면서 퇴장 연출을 재생하고, 둘 다 끝나면 캔버스를 다음 씬으로 넘긴 뒤 씬을 활성화한다.
    private IEnumerator HandleLoadRoutine(string sceneName)
    {
        HandlePrepare();

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"MainMenuLoadingSequence: '{sceneName}' 씬을 불러오지 못했습니다. 빌드 설정에 등록되어 있는지 확인하세요.", this);
            _rootGroup.interactable = true;
            _isRunning = false;
            yield break;
        }

        operation.allowSceneActivation = false;

        float elapsed = 0f;
        float motionEnd = HandleGetMotionEndTime();

        // allowSceneActivation이 꺼져 있으면 진행도는 0.9에서 멈추므로, 0.9를 씬 불러오기 완료로 본다.
        while (elapsed < motionEnd || operation.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;
            HandleApplyMotion(elapsed);
            HandleSetTargetProgress(Mathf.Clamp01(operation.progress / 0.9f) * _sceneLoadShare);
            yield return null;
        }

        HandleApplyMotion(motionEnd);
        HandleHandOff();
        operation.allowSceneActivation = true;

        while (!operation.isDone)
            yield return null;

        // 인게임 씬의 Start가 화면을 꺼내 갈 시간을 준 뒤에도 아무도 가져가지 않았으면 스스로 걷는다.
        yield return null;
        yield return null;
        if (SceneLoadingHandoff.TryRevoke(this))
            Finish();
    }

    // 버튼 입력을 막고, 퇴장·이동 대상들의 원래 배치를 기억해 둔다.
    private void HandlePrepare()
    {
        _rootGroup.interactable = false;
        _rootGroup.blocksRaycasts = true;

        _exitGroups = new CanvasGroup[_exitTargets.Length];
        _exitBasePositions = new Vector2[_exitTargets.Length];
        for (int i = 0; i < _exitTargets.Length; i++)
        {
            RectTransform target = _exitTargets[i];
            if (target == null)
                continue;

            _exitBasePositions[i] = target.anchoredPosition;
            _exitGroups[i] = target.GetComponent<CanvasGroup>();
            if (_exitGroups[i] == null)
                _exitGroups[i] = target.gameObject.AddComponent<CanvasGroup>();
        }

        HandlePrepareCenterParts();
    }

    // 가운데로 옮길 조각들과 각자의 출발 위치, 함께 옮겨질 거리를 정해 둔다.
    private void HandlePrepareCenterParts()
    {
        if (_centerTarget == null)
        {
            _movingParts = Array.Empty<RectTransform>();
            _movingFloats = Array.Empty<UIRectFloatMotion>();
            _movingStartPositions = Array.Empty<Vector2>();
            return;
        }

        _centerDelta = _centerPosition - _centerTarget.anchoredPosition;
        _movingParts = _centerParts.Length > 0 ? _centerParts : new[] { _centerTarget };
        _movingFloats = new UIRectFloatMotion[_movingParts.Length];
        _movingStartPositions = new Vector2[_movingParts.Length];

        for (int i = 0; i < _movingParts.Length; i++)
        {
            if (_movingParts[i] == null)
                continue;

            // 떠다니는 조각은 매 프레임 기준 위치로 되돌아가므로, 위치 대신 떠다니는 기준 위치를 옮긴다.
            _movingFloats[i] = _movingParts[i].GetComponent<UIRectFloatMotion>();
            _movingStartPositions[i] = _movingFloats[i] != null ? _movingFloats[i].BasePosition : _movingParts[i].anchoredPosition;
        }
    }

    // 퇴장·이동·진행도 등장 연출이 모두 끝나는 시각(연출 시작 기준, 초)을 구한다.
    private float HandleGetMotionEndTime()
    {
        float exitEnd = _exitTargets.Length > 0 ? _exitStagger * (_exitTargets.Length - 1) + _exitDuration : 0f;
        int partCount = _movingParts != null ? _movingParts.Length : 0;
        float centerEnd = _centerDelay + _centerStagger * Mathf.Max(0, partCount - 1) + _centerDuration;
        float progressEnd = _progressFadeDelay + _progressFadeDuration;
        return Mathf.Max(exitEnd, Mathf.Max(centerEnd, progressEnd));
    }

    // 연출 시작부터 지난 시간에 맞춰 조작 UI 퇴장, 장식 이동, 진행도 등장을 적용한다.
    private void HandleApplyMotion(float elapsed)
    {
        HandleApplyExit(elapsed);
        HandleApplyCenter(elapsed);
        HandleApplyProgressFade(elapsed);
    }

    // 조작 UI 묶음들을 차례로 흐리며 왼쪽으로 가속해 밀어낸다.
    private void HandleApplyExit(float elapsed)
    {
        for (int i = 0; i < _exitTargets.Length; i++)
        {
            RectTransform target = _exitTargets[i];
            if (target == null)
                continue;

            float t = Mathf.Clamp01((elapsed - _exitStagger * i) / _exitDuration);
            float eased = t * t * t;

            target.anchoredPosition = _exitBasePositions[i] + new Vector2(-_exitDistance * eased, 0f);
            _exitGroups[i].alpha = 1f - Mathf.Clamp01(t * 1.3f);
        }
    }

    // 장식 조각들을 중심 조각부터 차례로 출발시켜 가운데로 부드럽게 옮긴다.
    private void HandleApplyCenter(float elapsed)
    {
        for (int i = 0; i < _movingParts.Length; i++)
        {
            if (_movingParts[i] == null)
                continue;

            float t = Mathf.Clamp01((elapsed - _centerDelay - _centerStagger * i) / _centerDuration);
            float eased = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
            Vector2 position = _movingStartPositions[i] + _centerDelta * eased;

            if (_movingFloats[i] != null)
                _movingFloats[i].BasePosition = position;
            else
                _movingParts[i].anchoredPosition = position;
        }
    }

    // 진행도 묶음을 서서히 드러낸다.
    private void HandleApplyProgressFade(float elapsed)
    {
        if (_progressGroup == null)
            return;

        _progressGroup.alpha = Mathf.Clamp01((elapsed - _progressFadeDelay) / _progressFadeDuration);
    }

    // 캔버스를 씬 전환에도 파괴되지 않게 하고, 인게임 UI 위에 그려지게 한 뒤 다음 씬이 꺼내 가도록 맡긴다.
    private void HandleHandOff()
    {
        _canvas.sortingOrder = _handoffSortingOrder;
        DontDestroyOnLoad(gameObject);
        SceneLoadingHandoff.Hand(this);
    }

    // 목표 진행도를 정한다. 표시가 뒤로 가지 않도록 지금 목표보다 작은 값은 무시한다.
    private void HandleSetTargetProgress(float progress)
    {
        _targetProgress = Mathf.Max(_targetProgress, Mathf.Clamp01(progress));
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
        if (_progressFill != null)
            _progressFill.anchorMax = new Vector2(_displayedProgress, 1f);

        if (_progressPercentText != null)
            _progressPercentText.text = $"{Mathf.RoundToInt(_displayedProgress * 100f)}%";
    }

    // 사라지는 중이면 경과 시간만큼 캔버스 전체를 흐리게 하고, 다 사라지면 스스로 파괴된다.
    private void HandleFadeOut()
    {
        if (!_isFadingOut)
            return;

        _fadeElapsed += Time.unscaledDeltaTime;
        _rootGroup.alpha = _fadeOutDuration <= 0f ? 0f : Mathf.Clamp01(1f - _fadeElapsed / _fadeOutDuration);

        if (_fadeElapsed >= _fadeOutDuration)
            Destroy(gameObject);
    }
}
