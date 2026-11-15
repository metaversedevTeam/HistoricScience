using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 시민 앞의 상호작용 대상 오른쪽에 그 대상과 할 수 있는 명령 이름을 사각형 줄로 띄우는 캔버스 UI.
// 대상 메시 중심에서 연결선이 뻗어 나오고, 테두리가 위아래로 펼쳐진 뒤 줄들이 차례로 지층 와이프 연출로 나타난다.
// 떠 있는 동안 마우스 휠을 올리면 이전 명령, 내리면 다음 명령을 선택한다.
public class InteractionCommandListUI : MonoBehaviour
{
    // 지금 떠 있는 명령 목록 수. 하나라도 떠 있으면 휠 입력을 명령 선택에 쓰도록 카메라 확대·축소가 이 값을 보고 물러선다.
    private static int s_visibleCount;

    // 명령 목록이 하나라도 떠 있어 마우스 휠을 명령 선택에 쓰고 있는지 여부
    public static bool IsWheelCaptured => s_visibleCount > 0;

    // 도메인 리로드 없이 플레이 모드에 들어가도 이전 플레이의 떠 있는 수가 남지 않게 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetVisibleCount()
    {
        s_visibleCount = 0;
    }

    // 명령 한 줄 프리팹(이 UI 프리팹 안의 비활성 줄을 원본으로 써도 된다)
    [SerializeField] private InteractionCommandRowUI _rowPrefab;

    // 줄들이 쌓일 부모
    [SerializeField] private RectTransform _rowContainer;

    // 대상과 목록을 잇는 연결선(피벗이 오른쪽이어야 한다)
    [SerializeField] private RectTransform _connector;

    // 목록을 감싸는 테두리(피벗이 위쪽이어야 한다)
    [SerializeField] private RectTransform _frame;

    // 목록 위의 머리글 묶음
    [SerializeField] private CanvasGroup _header;

    // 전체 투명도
    [SerializeField] private CanvasGroup _group;

    // 대상의 화면 오른쪽 끝에서 목록까지 띄울 거리(연결선 길이)
    [SerializeField] private float _gap = 36f;

    // 연결선이 다 뻗는 데 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _connectorDuration = 0.18f;

    // 테두리가 펼쳐지기 시작하는 시각과 걸리는 시간(초)
    [SerializeField] private float _frameDelay = 0.08f;
    [SerializeField, Min(0.01f)] private float _frameDuration = 0.22f;

    // 첫 줄이 나타나기 시작하는 시각과 줄 사이 간격(초)
    [SerializeField] private float _rowDelay = 0.14f;
    [SerializeField] private float _rowStagger = 0.06f;

    // 사라질 때 투명해지는 속도(클수록 빠르다)
    [SerializeField, Min(0.1f)] private float _fadeOutResponse = 18f;

    // 사라질 때 오른쪽으로 밀려나는 거리
    [SerializeField] private float _fadeOutShift = 12f;

    // 만들어 둔 줄들. 명령 수가 줄면 남는 줄은 꺼 두고 다시 쓴다.
    private readonly List<InteractionCommandRowUI> _rows = new();

    // 지금 표시 중인 명령 이름들. 같은 목록이 다시 들어오면 연출을 다시 틀지 않는다.
    private readonly List<string> _shownNames = new();

    private RectTransform _rect;
    private RectTransform _parentRect;

    // 대상 위치를 화면 좌표로 바꿀 카메라
    private Camera _worldCamera;

    // 화면 좌표를 캔버스 로컬 좌표로 바꿀 카메라. 오버레이 캔버스에서는 null이어야 한다.
    private Camera _canvasCamera;

    // 목록을 붙일 대상의 충돌체. 대상 메시를 찾는 출발점이자, 메시가 없을 때 범위를 대신한다.
    private Collider _target;

    // 대상의 메시 렌더러들(건물 바닥 제외). 이들을 합친 범위의 중심을 기준으로 목록 위치를 잡는다.
    private readonly List<Renderer> _targetRenderers = new();

    // 대상 렌더러를 모을 때 재사용하는 임시 목록
    private static readonly List<Renderer> s_rendererBuffer = new();
    private static readonly List<BottomMaterialController> s_bottomBuffer = new();

    // 표시 중인지 여부
    private bool _isVisible;

    // 등장 연출이 시작된 뒤 지난 시간(초)
    private float _elapsed;

    // 사라지는 정도(0이면 다 보임, 1이면 다 사라짐)
    private float _fadeOut = 1f;

    // 대상이 카메라 뒤에 있어 그릴 수 없는 상태인지 여부
    private bool _isBehindCamera;

    // 지금 선택된 명령 줄 번호(0부터)
    private int _selectedIndex;

    // 지금 선택된 명령 줄 번호(0부터). 표시 중인 명령이 없으면 -1이다.
    public int SelectedIndex => _shownNames.Count > 0 ? _selectedIndex : -1;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _parentRect = transform.parent as RectTransform;
        CacheCameras();

        if (_rowPrefab != null && _rowPrefab.transform.IsChildOf(transform))
            _rowPrefab.gameObject.SetActive(false);

        _group.alpha = 0f;
    }

    // 꺼지거나 파괴될 때 떠 있던 수에서 빠져, 휠 입력을 카메라에 돌려준다.
    private void OnDisable()
    {
        SetVisible(false);
    }

    private void Update()
    {
        HandleWheelSelect();
    }

    private void LateUpdate()
    {
        HandleFollowTarget();
        HandleIntro();
        HandleFade();
    }

    // 대상 오른쪽에 명령 이름 목록을 띄운다. 대상이나 목록이 바뀌었을 때만 등장 연출과 선택을 처음부터 다시 시작한다.
    public void Show(Collider target, IReadOnlyList<string> commandNames)
    {
        bool isSame = _isVisible && target == _target && IsSameNames(commandNames);
        if (target != _target)
            CollectTargetRenderers(target);
        _target = target;
        SetVisible(true);
        if (isSame) return;

        RebuildRows(commandNames);
        Select(0);
        _elapsed = 0f;
        _fadeOut = 0f;
        HandleFollowTarget();
        HandleIntro();
        HandleFade();
    }

    // 목록을 서서히 감춘다.
    public void Hide()
    {
        SetVisible(false);
    }

    // 표시 여부를 바꾸고, 그에 맞춰 떠 있는 목록 수를 늘리거나 줄인다.
    private void SetVisible(bool isVisible)
    {
        if (_isVisible == isVisible) return;

        _isVisible = isVisible;
        s_visibleCount += isVisible ? 1 : -1;
    }

    // 떠 있는 동안 휠을 올리면 이전 명령, 내리면 다음 명령을 고른다. 끝에서 더 굴리면 반대쪽 끝으로 돌아간다.
    private void HandleWheelSelect()
    {
        if (!_isVisible || _shownNames.Count == 0) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        int step = scroll > 0f ? -1 : 1;
        int count = _shownNames.Count;
        Select((_selectedIndex + step + count) % count);
    }

    // 지정한 번호의 줄만 선택 상태로 만든다.
    private void Select(int index)
    {
        _selectedIndex = index;
        for (int i = 0; i < _rows.Count; i++)
            _rows[i].SetSelected(i == index);
    }

    // 지금 표시 중인 이름 목록과 새 목록이 같은지 확인한다.
    private bool IsSameNames(IReadOnlyList<string> commandNames)
    {
        if (commandNames.Count != _shownNames.Count) return false;

        for (int i = 0; i < commandNames.Count; i++)
        {
            if (commandNames[i] != _shownNames[i]) return false;
        }
        return true;
    }

    // 명령 이름 수만큼 줄을 켜고 채운다. 모자라면 새로 만들고, 남으면 끈다.
    private void RebuildRows(IReadOnlyList<string> commandNames)
    {
        _shownNames.Clear();
        _shownNames.AddRange(commandNames);

        while (_rows.Count < commandNames.Count)
        {
            var row = Instantiate(_rowPrefab, _rowContainer);
            _rows.Add(row);
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            bool isUsed = i < commandNames.Count;
            _rows[i].gameObject.SetActive(isUsed);
            if (isUsed)
                _rows[i].Bind(i, commandNames[i], _rowDelay + _rowStagger * i);
        }
    }

    // 월드 좌표 변환용 메인 카메라와 캔버스 좌표 변환용 카메라를 캐싱한다.
    private void CacheCameras()
    {
        _worldCamera = Camera.main;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        canvas = canvas.rootCanvas;
        _canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    // 대상 메시 범위의 중심이 화면에 비친 점에서 연결선 길이만큼 오른쪽으로 띄운 자리에 목록을 둔다.
    private void HandleFollowTarget()
    {
        if (_target == null || _worldCamera == null || _parentRect == null) return;

        if (!TryGetScreenAnchor(GetTargetBounds(), out Vector2 screenAnchor))
        {
            _isBehindCamera = true;
            return;
        }
        _isBehindCamera = false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, screenAnchor, _canvasCamera, out Vector2 localPoint))
            return;

        localPoint.x += _gap + _fadeOutShift * _fadeOut;
        _rect.anchoredPosition = localPoint;
    }

    // 대상의 상호작용 주인(IGatherable/ICommandable) 아래 메시 렌더러를 모은다. 건물 바닥 렌더러는 뺀다.
    private void CollectTargetRenderers(Collider target)
    {
        _targetRenderers.Clear();
        if (target == null) return;

        Transform root = GetInteractableRoot(target);
        root.GetComponentsInChildren(true, s_rendererBuffer);
        foreach (Renderer renderer in s_rendererBuffer)
        {
            if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                _targetRenderers.Add(renderer);
        }
        s_rendererBuffer.Clear();

        root.GetComponentsInChildren(true, s_bottomBuffer);
        foreach (BottomMaterialController bottom in s_bottomBuffer)
        {
            if (bottom.BottomRenderer != null)
                _targetRenderers.Remove(bottom.BottomRenderer);
        }
        s_bottomBuffer.Clear();
    }

    // 충돌체 위쪽에서 IGatherable이나 ICommandable을 가진 오브젝트를 찾고, 없으면 충돌체 자신을 돌려준다.
    private static Transform GetInteractableRoot(Collider target)
    {
        if (target.GetComponentInParent<IGatherable>() is Component gatherable)
            return gatherable.transform;
        if (target.GetComponentInParent<ICommandable>() is Component commandable)
            return commandable.transform;
        return target.transform;
    }

    // 켜져 있는 대상 메시들을 합친 범위를 구한다. 그런 메시가 없으면 충돌체 범위를 쓴다.
    private Bounds GetTargetBounds()
    {
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Renderer renderer in _targetRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

            if (hasBounds)
                bounds.Encapsulate(renderer.bounds);
            else
                bounds = renderer.bounds;
            hasBounds = true;
        }
        return hasBounds ? bounds : _target.bounds;
    }

    // 범위 상자의 중심을 화면에 비춘 점을 구한다. 중심이 카메라 뒤로 넘어가면 실패로 본다.
    private bool TryGetScreenAnchor(Bounds bounds, out Vector2 screenAnchor)
    {
        Vector3 screen = _worldCamera.WorldToScreenPoint(bounds.center);
        screenAnchor = screen;
        return screen.z >= 0f;
    }

    // 등장 연출 시간을 흘려 연결선·테두리·머리글·줄에 반영한다.
    private void HandleIntro()
    {
        _elapsed += Time.unscaledDeltaTime;

        float line = EaseOut(Mathf.Clamp01(_elapsed / _connectorDuration));
        _connector.sizeDelta = new Vector2(_gap * line, _connector.sizeDelta.y);

        float frame = EaseOut(Mathf.Clamp01((_elapsed - _frameDelay) / _frameDuration));
        _frame.localScale = new Vector3(1f, frame, 1f);
        _header.alpha = frame;

        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].gameObject.activeSelf)
                _rows[i].Apply(_elapsed);
        }
    }

    // 표시 여부에 따라 전체 투명도를 부드럽게 바꾼다. 대상이 카메라 뒤에 있으면 즉시 감춘다.
    private void HandleFade()
    {
        if (!_isVisible)
        {
            float k = 1f - Mathf.Exp(-_fadeOutResponse * Time.unscaledDeltaTime);
            _fadeOut = Mathf.Lerp(_fadeOut, 1f, k);
        }

        _group.alpha = _isBehindCamera || _target == null ? 0f : 1f - _fadeOut;
    }

    // 끝으로 갈수록 느려지는 곡선
    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
}
