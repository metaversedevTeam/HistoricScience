using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 인게임에서 메인 메뉴로 나갈 때의 전환 연출. 메인 메뉴→인게임 로딩 연출(MainMenuLoadingSequence)을 거꾸로 재생한다.
// 화면을 다 덮을 만큼 큰 중심 사각형이 줄어들며 테두리 바깥에 로딩 화면을 채워 게임을 가리고, 정렬돼 있던 장식 사각형들이
// 다시 떠다니기 시작하면 진행도를 비우면서 메인 메뉴 씬을 불러온다. 메인 메뉴는 이어서 로딩 연출의 앞부분을 거꾸로 재생한다.
[RequireComponent(typeof(Canvas))]
public class IngameExitSequence : MonoBehaviour
{
    [Header("중심 사각형")]
    // 줄어들며 로딩 화면을 채워 나갈 중심 사각형(로딩 연출에서 커지며 화면을 지우던 사각형)
    [SerializeField] private RectTransform _revealCore;

    // 떠다니기를 다시 시작할 장식 사각형들의 묶음
    [SerializeField] private RectTransform _settleRoot;

    // 중심 사각형이 화면 크기에서 원래 크기로 줄어드는 데 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _closeDuration = 0.9f;

    // 줄어드는 중심 사각형에서 지워지지 않고 남는 테두리 두께(캔버스 단위)
    [SerializeField, Min(0f)] private float _revealBorder = 12f;

    // 줄어드는 동안 중심 사각형(남는 테두리)의 투명도. 다 줄어들면 원래 투명도로 돌아간다.
    [SerializeField, Range(0f, 1f)] private float _revealCoreAlpha = 0.9f;

    // 장식 사각형들이 정렬된 자세에서 원래 움직임으로 돌아오기까지 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _unsettleDuration = 0.45f;

    [Header("진행도 표시")]
    // 장식 아래의 진행도 묶음
    [SerializeField] private CanvasGroup _progressGroup;

    // 진행도만큼 채워진 막대. 트랙(부모) 기준 오른쪽 앵커를 옮겨 비율을 표현한다.
    [SerializeField] private RectTransform _progressFill;

    // 진행도를 퍼센트로 보여 주는 문구
    [SerializeField] private TMP_Text _progressPercentText;

    // 진행도 막대가 100%에서 0%까지 비워지는 데 걸리는 가장 짧은 시간(초). 씬 불러오기가 더 오래 걸리면 그에 맞춰 늦춰진다.
    [SerializeField, Min(0.01f)] private float _drainDuration = 0.6f;

    // 캔버스 전체의 입력을 다룰 그룹
    private CanvasGroup _rootGroup;

    // 연출이 이미 시작되었는지 여부
    private bool _isRunning;

    private void Awake()
    {
        _rootGroup = GetComponent<CanvasGroup>();
        if (_rootGroup == null)
            _rootGroup = gameObject.AddComponent<CanvasGroup>();

        if (_progressGroup != null)
            _progressGroup.alpha = 0f;

        HandleApplyProgress(1f);
    }

    // 전환 연출을 재생한 뒤 지정한 씬(메인 메뉴)으로 넘어간다. 이미 시작했으면 무시한다.
    public void Play(string sceneName)
    {
        if (_isRunning)
            return;

        _isRunning = true;
        StartCoroutine(HandleExitRoutine(sceneName));
    }

    // 화면 덮기 → 장식 다시 움직이기 → 진행도 비우며 씬 불러오기 순서로 진행하고, 메인 메뉴에 역재생을 맡긴 뒤 씬을 넘긴다.
    private IEnumerator HandleExitRoutine(string sceneName)
    {
        _rootGroup.blocksRaycasts = true;
        _rootGroup.interactable = false;

        UIRectFloatMotion[] floats = _settleRoot.GetComponentsInChildren<UIRectFloatMotion>();
        foreach (UIRectFloatMotion motion in floats)
            motion.BeginUnsettle();

        yield return HandleCloseRoutine();
        yield return HandleUnsettleRoutine(floats);
        yield return HandleLoadRoutine(sceneName);
    }

    // 중심 사각형을 화면을 다 덮는 크기에서 원래 크기까지 감속하며 줄이고, 테두리 바깥의 로딩 화면이 게임을 덮어 나가게 한다.
    private IEnumerator HandleCloseRoutine()
    {
        RectTransform canvasRect = (RectTransform)transform;
        RectTransform hole = HandleCreateCutoutHole();

        // 중심 사각형 한 단위가 캔버스에서 차지하는 크기. 크기 계산은 캔버스 단위로 하고 사각형에는 자기 단위로 적용한다.
        float unitScale = _revealCore.lossyScale.x / canvasRect.lossyScale.x;
        Vector2 center = canvasRect.InverseTransformPoint(_revealCore.position);
        Rect canvasArea = canvasRect.rect;

        float endSize = _revealCore.rect.width * unitScale;
        float halfCover = Mathf.Max(Mathf.Abs(center.x) + canvasArea.width * 0.5f, Mathf.Abs(center.y) + canvasArea.height * 0.5f);
        float coverSize = halfCover * 2f + _revealBorder * 2f + 4f;

        hole.localPosition = center;
        hole.localRotation = _revealCore.localRotation;

        Graphic coreGraphic = _revealCore.GetComponent<Graphic>();
        float coreEndAlpha = coreGraphic != null ? coreGraphic.color.a : 1f;

        for (float elapsed = 0f; elapsed < _closeDuration; elapsed += Time.unscaledDeltaTime)
        {
            // 로딩 연출의 지우기를 시간만 거꾸로 돌린다. (그쪽은 점점 빨라지며 커지므로, 이쪽은 빠르게 줄다가 감속한다)
            float reversed = 1f - elapsed / _closeDuration;
            HandleApplyClose(hole, unitScale, Mathf.Lerp(endSize, coverSize, reversed * reversed * reversed));
            HandleApplyCoreAlpha(coreGraphic, Mathf.Lerp(coreEndAlpha, _revealCoreAlpha, Mathf.Clamp01(reversed * 4f)));
            yield return null;
        }

        HandleApplyClose(hole, unitScale, endSize);
        HandleApplyCoreAlpha(coreGraphic, coreEndAlpha);
        HandleRemoveCutout(hole);
    }

    // 정렬돼 멈춰 있던 장식 사각형들의 움직임 세기를 서서히 올리면서 진행도 표시를 드러낸다.
    private IEnumerator HandleUnsettleRoutine(UIRectFloatMotion[] floats)
    {
        for (float elapsed = 0f; elapsed < _unsettleDuration; elapsed += Time.unscaledDeltaTime)
        {
            HandleApplyUnsettle(floats, elapsed / _unsettleDuration);
            yield return null;
        }

        HandleApplyUnsettle(floats, 1f);
    }

    // 다시 움직이는 정도(0~1)를 가속 곡선으로 장식 사각형들의 움직임 세기와 진행도 표시의 투명도에 적용한다.
    private void HandleApplyUnsettle(UIRectFloatMotion[] floats, float t)
    {
        float eased = Mathf.Pow(Mathf.Clamp01(t), 3f);
        foreach (UIRectFloatMotion motion in floats)
            motion.MotionWeight = eased;

        if (_progressGroup != null)
            _progressGroup.alpha = eased;
    }

    // 메인 메뉴 씬을 불러오는 동안 진행도를 100%에서 0%로 비우고, 둘 다 끝나면 메인 메뉴에 역재생을 맡기고 씬을 넘긴다.
    private IEnumerator HandleLoadRoutine(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"IngameExitSequence: '{sceneName}' 씬을 불러오지 못했습니다. 빌드 설정에 등록되어 있는지 확인하세요.", this);
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
            yield break;
        }

        operation.allowSceneActivation = false;

        float displayed = 1f;
        while (displayed > 0f || operation.progress < 0.9f)
        {
            // allowSceneActivation이 꺼져 있으면 진행도는 0.9에서 멈추므로, 0.9를 씬 불러오기 완료로 본다.
            float loadRemaining = 1f - Mathf.Clamp01(operation.progress / 0.9f);
            displayed = Mathf.Max(loadRemaining, displayed - Time.unscaledDeltaTime / _drainDuration);
            HandleApplyProgress(displayed);
            yield return null;
        }

        HandleApplyProgress(0f);
        MainMenuLoadingSequence.RequestReturnSequence();
        Time.timeScale = 1f;
        operation.allowSceneActivation = true;
    }

    // 진행도(0~1)를 채움 막대의 너비와 퍼센트 문구에 적용한다.
    private void HandleApplyProgress(float progress)
    {
        if (_progressFill != null)
            _progressFill.anchorMax = new Vector2(progress, 1f);

        if (_progressPercentText != null)
            _progressPercentText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
    }

    // 중심 사각형과 구멍의 크기를 정한다. 구멍은 테두리 두께만큼 작아 사각형의 가장자리만 남는다.
    private void HandleApplyClose(RectTransform hole, float unitScale, float size)
    {
        float coreSize = size / unitScale;
        _revealCore.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, coreSize);
        _revealCore.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, coreSize);

        float holeSize = Mathf.Max(0f, size - _revealBorder * 2f);
        hole.sizeDelta = new Vector2(holeSize, holeSize);
    }

    // 중심 사각형의 투명도를 바꾼다.
    private void HandleApplyCoreAlpha(Graphic coreGraphic, float alpha)
    {
        if (coreGraphic == null)
            return;

        Color color = coreGraphic.color;
        color.a = alpha;
        coreGraphic.color = color;
    }

    // 캔버스 맨 앞에 스텐실 구멍을 만들고, 캔버스의 모든 그래픽이 그 구멍 안에서는 그려지지 않게 한다.
    private RectTransform HandleCreateCutoutHole()
    {
        var holeObject = new GameObject("Reveal-Hole", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var hole = (RectTransform)holeObject.transform;
        hole.SetParent(transform, false);
        hole.SetAsFirstSibling();
        hole.anchorMin = hole.anchorMax = new Vector2(0.5f, 0.5f);
        hole.pivot = new Vector2(0.5f, 0.5f);
        hole.sizeDelta = Vector2.zero;
        holeObject.GetComponent<Image>().raycastTarget = false;

        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.gameObject != holeObject)
                graphic.gameObject.AddComponent<UIStencilCutout>();
        }

        holeObject.AddComponent<UIStencilCutout>().Setup(true);
        return hole;
    }

    // 다 덮은 뒤에는 구멍과 지워지기용 스텐실 설정을 걷어 내 평소처럼 그리게 한다.
    private void HandleRemoveCutout(RectTransform hole)
    {
        foreach (UIStencilCutout cutout in GetComponentsInChildren<UIStencilCutout>(true))
        {
            if (cutout.gameObject != hole.gameObject)
                Destroy(cutout);
        }

        Destroy(hole.gameObject);
    }
}
