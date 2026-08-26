using System;
using UnityEngine;
using UnityEngine.UI;

// 강조 대상만 남기고 화면을 덮는 튜토리얼 강조 UI (피그마 spotlight-dim-mask · highlight-ring · glowing-arrow).
// UIManager로 열리므로 그 시점에 열려 있는 다른 UI보다 뒤늦게 캔버스에 붙어 그 위에 그려지고, 매 프레임 대상을 다시 물어 위치를 따라간다.
// 어떤 이미지도 레이캐스트를 받지 않아, 강조된 대상을 그대로 클릭할 수 있다.
public class TutorialHighlightUI : OpenableUIBase<TutorialHighlightData>
{
    // 화면 전체를 덮는 자기 자신의 RectTransform
    [SerializeField] private RectTransform _rootRect;

    // 가림막과 구멍 마스크를 한 번에 켜고 끄기 위한 묶음
    [SerializeField] private GameObject _dimGroup;

    // 강조 원 바깥을 덮는 네 장의 가림막 (왼쪽·오른쪽·위·아래)
    [SerializeField] private RectTransform _dimLeft;
    [SerializeField] private RectTransform _dimRight;
    [SerializeField] private RectTransform _dimTop;
    [SerializeField] private RectTransform _dimBottom;

    // 강조 원 자리에 놓여 가운데만 뚫어 주는 마스크
    [SerializeField] private RectTransform _hole;

    // 강조 대상을 감싸는 청록 링
    [SerializeField] private RectTransform _ring;

    // 링 안쪽의 호박색 점선 링
    [SerializeField] private RectTransform _dashedRing;

    // 링 위에 떠서 대상을 가리키는 화살표
    [SerializeField] private RectTransform _arrow;

    // 그려야 할 시각 요소를 한 번에 켜고 끄기 위한 묶음
    [SerializeField] private GameObject _visuals;

    // 매 프레임 강조 대상을 다시 받아 오는 제공자
    private Func<TutorialHighlightRequest?> _provider;

    // 항상 이 오브젝트보다 아래에 그려지도록 형제 순서를 맞출 대상
    private Transform _keepBelow;

    // 월드 좌표를 화면 좌표로 바꿀 카메라
    private Camera _worldCamera;

    // 화면 좌표를 캔버스 좌표로 바꿀 때 쓰는 카메라. 오버레이 캔버스에서는 null이어야 한다.
    private Camera _canvasCamera;

    // UI 요소의 모서리를 받아 올 때 매 프레임 새로 할당하지 않도록 재사용하는 버퍼
    private readonly Vector3[] _cornerBuffer = new Vector3[4];

    // 코드로 만든 강조 UI 템플릿을 부모 아래에 세워 돌려준다. 이 템플릿 자체는 꺼진 채로 두고, UIManager가 복제해서 연다.
    public static TutorialHighlightUI CreateTemplate(Transform parent)
    {
        RectTransform root = TutorialUIBuilder.CreateRect("TutorialHighlightUI", parent);
        TutorialUIBuilder.Stretch(root);

        TutorialHighlightUI ui = root.gameObject.AddComponent<TutorialHighlightUI>();
        ui._rootRect = root;

        RectTransform visuals = TutorialUIBuilder.CreateRect("Visuals", root);
        TutorialUIBuilder.Stretch(visuals);
        ui._visuals = visuals.gameObject;

        RectTransform dimGroup = TutorialUIBuilder.CreateRect("Dim", visuals);
        TutorialUIBuilder.Stretch(dimGroup);
        ui._dimGroup = dimGroup.gameObject;

        ui._dimLeft = CreateDim("DimLeft", dimGroup);
        ui._dimRight = CreateDim("DimRight", dimGroup);
        ui._dimTop = CreateDim("DimTop", dimGroup);
        ui._dimBottom = CreateDim("DimBottom", dimGroup);

        ui._hole = CreateCentered("SpotlightHole", dimGroup, TutorialSpriteLibrary.SpotlightHole, TutorialTheme.SpotlightDim);
        ui._dashedRing = CreateCentered("FocusRingDashed", visuals, TutorialSpriteLibrary.FocusRingDashed, TutorialTheme.Amber);
        ui._ring = CreateCentered("FocusRing", visuals, TutorialSpriteLibrary.FocusRing, TutorialTheme.Accent);
        ui._arrow = CreateCentered("Arrow", visuals, TutorialSpriteLibrary.ArrowDown, TutorialTheme.Accent);

        root.gameObject.SetActive(false);
        return ui;
    }

    private void Awake()
    {
        _worldCamera = Camera.main;
        ResolveCanvasCamera();
    }

    private void LateUpdate()
    {
        HandleKeepOnTop();
        HandleFollowTarget();
    }

    // 강조 대상 제공자와 형제 순서 기준을 받아 둔다.
    protected override void ApplyData(TutorialHighlightData data)
    {
        _provider = data.Provider;
        _keepBelow = data.KeepBelow;

        if (_worldCamera == null)
            _worldCamera = Camera.main;

        ResolveCanvasCamera();
        HandleFollowTarget();
    }

    // 풀로 돌아가기 전에 대상 참조를 비워, 다음에 열릴 때 이전 단계의 대상을 따라가지 않게 한다.
    protected override void OnReturnToPool()
    {
        _provider = null;
        _keepBelow = null;
    }

    // 가림막 한 장을 만든다. 강조 원 바깥을 덮는 단색 사각형이다.
    private static RectTransform CreateDim(string name, Transform parent)
    {
        Image image = TutorialUIBuilder.CreateImage(name, parent, TutorialSpriteLibrary.Solid, TutorialTheme.SpotlightDim);
        TutorialUIBuilder.Anchor(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return image.rectTransform;
    }

    // 강조 원을 중심으로 배치되는 이미지 한 장을 만든다.
    private static RectTransform CreateCentered(string name, Transform parent, Sprite sprite, Color color)
    {
        Image image = TutorialUIBuilder.CreateImage(name, parent, sprite, color);
        TutorialUIBuilder.Anchor(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return image.rectTransform;
    }

    // 캔버스 좌표 변환에 쓸 카메라를 캐싱한다. 오버레이 캔버스에서는 카메라를 쓰지 않는다.
    private void ResolveCanvasCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            _canvasCamera = null;
            return;
        }

        canvas = canvas.rootCanvas;
        _canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    // 다른 UI 위에 그려지도록 형제 순서를 맨 뒤로 옮긴다. 대화창이 있으면 그 바로 아래 자리를 지킨다.
    private void HandleKeepOnTop()
    {
        Transform parent = transform.parent;
        if (parent == null) return;

        int desired = parent.childCount - 1;
        if (_keepBelow != null && _keepBelow.parent == parent && _keepBelow.gameObject.activeInHierarchy)
            desired = Mathf.Max(0, desired - 1);

        if (transform.GetSiblingIndex() != desired)
            transform.SetSiblingIndex(desired);
    }

    // 제공자에게 이번 프레임의 강조 대상을 물어 화면 위 원을 구하고, 가림막·링·화살표를 그 자리에 맞춘다.
    private void HandleFollowTarget()
    {
        TutorialHighlightRequest? request = _provider?.Invoke();

        if (request == null || !request.Value.IsValid || !TryResolveCircle(request.Value, out Vector2 center, out float radius))
        {
            if (_visuals.activeSelf) _visuals.SetActive(false);
            return;
        }

        if (!_visuals.activeSelf) _visuals.SetActive(true);

        ApplyDim(center, radius, request.Value.ShowDim);
        ApplyRing(center, radius);
        ApplyArrow(center, radius, request.Value.ShowArrow);
    }

    // 강조 대상이 화면에서 차지하는 원의 중심과 반지름을 캔버스 좌표로 구한다. 화면 밖이면 false를 반환한다.
    private bool TryResolveCircle(in TutorialHighlightRequest request, out Vector2 center, out float radius)
    {
        return request.UiTarget != null
            ? TryResolveUiCircle(request.UiTarget, out center, out radius)
            : TryResolveWorldCircle(request.WorldTarget, request.WorldRadius, out center, out radius);
    }

    // UI 요소의 네 모서리를 캔버스 좌표로 옮겨 그 사각형을 감싸는 원을 구한다.
    private bool TryResolveUiCircle(RectTransform target, out Vector2 center, out float radius)
    {
        center = default;
        radius = 0f;

        if (!target.gameObject.activeInHierarchy) return false;

        target.GetWorldCorners(_cornerBuffer);

        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;

        foreach (Vector3 corner in _cornerBuffer)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(_canvasCamera, corner);
            if (!TryScreenToLocal(screenPoint, out Vector2 local)) return false;

            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        center = (min + max) * 0.5f;
        radius = ClampRadius(Mathf.Max(max.x - min.x, max.y - min.y) * 0.5f);
        return true;
    }

    // 월드 오브젝트의 위치와 반지름을 캔버스 좌표의 원으로 옮긴다. 카메라 뒤에 있으면 false를 반환한다.
    private bool TryResolveWorldCircle(Transform target, float worldRadius, out Vector2 center, out float radius)
    {
        center = default;
        radius = 0f;

        if (target == null || _worldCamera == null) return false;

        Vector3 screenCenter = _worldCamera.WorldToScreenPoint(target.position);
        if (screenCenter.z <= 0f) return false;

        Vector3 screenEdge = _worldCamera.WorldToScreenPoint(target.position + _worldCamera.transform.right * worldRadius);

        if (!TryScreenToLocal(screenCenter, out center)) return false;
        if (!TryScreenToLocal(screenEdge, out Vector2 edge)) return false;

        radius = ClampRadius(Vector2.Distance(center, edge));
        return true;
    }

    // 화면 좌표를 이 UI의 캔버스 좌표로 바꾼다.
    private bool TryScreenToLocal(Vector2 screenPoint, out Vector2 local)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, screenPoint, _canvasCamera, out local);
    }

    // 대상 크기에서 구한 반지름에 여유를 더하고, 너무 작거나 화면을 다 덮지 않도록 범위를 고정한다.
    private static float ClampRadius(float rawRadius)
    {
        return Mathf.Clamp(rawRadius + TutorialTheme.SpotlightPadding, TutorialTheme.SpotlightMinRadius, TutorialTheme.SpotlightMaxRadius);
    }

    // 강조 원을 뺀 나머지를 네 장의 가림막으로 덮고, 원 자리에는 가운데가 뚫린 마스크를 놓는다.
    // 화면을 넓게 봐야 하는 단계에서는 가림막 자체를 꺼서 링과 화살표만 남긴다.
    private void ApplyDim(Vector2 center, float radius, bool showDim)
    {
        if (_dimGroup.activeSelf != showDim)
            _dimGroup.SetActive(showDim);

        if (!showDim) return;

        Vector2 size = _rootRect.rect.size;
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;

        float left = center.x - radius;
        float right = center.x + radius;
        float bottom = center.y - radius;
        float top = center.y + radius;

        SetBox(_dimLeft, -halfWidth, left, -halfHeight, halfHeight);
        SetBox(_dimRight, right, halfWidth, -halfHeight, halfHeight);
        SetBox(_dimTop, left, right, top, halfHeight);
        SetBox(_dimBottom, left, right, -halfHeight, bottom);

        _hole.anchoredPosition = center;
        _hole.sizeDelta = new Vector2(radius * 2f, radius * 2f);
    }

    // 가림막 한 장을 캔버스 좌표의 사각형 범위에 맞춘다. 범위가 뒤집히면 폭이 0인 사각형이 된다.
    private static void SetBox(RectTransform rect, float minX, float maxX, float minY, float maxY)
    {
        float width = Mathf.Max(0f, maxX - minX);
        float height = Mathf.Max(0f, maxY - minY);

        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(minX + width * 0.5f, minY + height * 0.5f);
    }

    // 강조 링과 점선 링을 원 자리에 맞추고, 링이 천천히 커졌다 작아지도록 맥동시킨다.
    private void ApplyRing(Vector2 center, float radius)
    {
        float pulse = 1f + Mathf.Sin(Time.time * 3f) * TutorialTheme.FocusRingPulse;
        float ringSize = radius * 2f * TutorialTheme.FocusRingScale;

        _ring.anchoredPosition = center;
        _ring.sizeDelta = new Vector2(ringSize * pulse, ringSize * pulse);

        _dashedRing.anchoredPosition = center;
        _dashedRing.sizeDelta = new Vector2(ringSize, ringSize);
    }

    // 지시 화살표를 링 위에 띄우고 위아래로 흔든다. 위쪽 공간이 모자라면 아래쪽으로 옮겨 뒤집는다.
    private void ApplyArrow(Vector2 center, float radius, bool showArrow)
    {
        if (_arrow.gameObject.activeSelf != showArrow)
            _arrow.gameObject.SetActive(showArrow);

        if (!showArrow) return;

        float bob = Mathf.Sin(Time.time * 4f) * TutorialTheme.ArrowBob;
        float ringRadius = radius * TutorialTheme.FocusRingScale;
        float distance = ringRadius + TutorialTheme.ArrowGap + TutorialTheme.ArrowSize * 0.5f;
        float halfHeight = _rootRect.rect.size.y * 0.5f;

        bool placeAbove = center.y + distance + TutorialTheme.ArrowSize * 0.5f <= halfHeight;
        float offset = placeAbove ? distance + bob : -(distance + bob);

        _arrow.anchoredPosition = new Vector2(center.x, center.y + offset);
        _arrow.sizeDelta = new Vector2(TutorialTheme.ArrowSize, TutorialTheme.ArrowSize);
        _arrow.localRotation = Quaternion.Euler(0f, 0f, placeAbove ? 0f : 180f);
    }
}
