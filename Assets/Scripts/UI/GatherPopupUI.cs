using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 채집 성공 시 채집한 월드 위치에 아이템 아이콘과 획득 개수를 잠깐 띄우고, 지속 시간 동안 위로 올라가며 투명해진 뒤 스스로 사라지는 캔버스 UI
public class GatherPopupUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _countText;
    [SerializeField] private CanvasGroup _canvasGroup;

    // 표시 지속 시간(초). 이 시간이 지나면 스스로 파괴된다.
    [SerializeField, Min(0.01f)] private float _duration = 1f;

    // 지속 시간 동안 캔버스 좌표 기준으로 올라갈 거리
    [SerializeField] private float _riseDistance = 80f;

    // 진행도(0~1)에 대한 알파 곡선. 기본값은 완전 불투명에서 투명으로 선형 감소한다.
    [SerializeField] private AnimationCurve _alphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    private RectTransform _rect;
    private RectTransform _parentRect;

    // 기준 월드 위치를 화면 좌표로 바꿀 카메라
    private Camera _worldCamera;

    // 화면 좌표를 캔버스 로컬 좌표로 바꿀 때 쓰는 카메라. 오버레이 캔버스에서는 null이어야 한다.
    private Camera _canvasCamera;

    private Vector3 _worldAnchor;
    private float _elapsed;

    // 기준 위치가 카메라 뒤로 넘어가 화면에 그릴 수 없는 상태인지 여부
    private bool _isBehindCamera;

    // 지속 시간에 대한 현재 진행도(0~1)
    private float Progress => Mathf.Clamp01(_elapsed / _duration);

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _parentRect = transform.parent as RectTransform;
        CacheCameras();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        HandleFollowAnchor();
        HandleFade();
        HandleLifetime();
    }

    // 아이콘과 획득 개수를 채우고, 지정한 월드 위치를 기준으로 떠오르기 시작한다.
    public void Show(ItemData item, int count, Vector3 worldAnchor)
    {
        _worldAnchor = worldAnchor;
        _elapsed = 0f;

        _icon.sprite = item != null ? item.IconSprite : null;
        _icon.enabled = _icon.sprite != null;
        _countText.text = $"+{count}";

        // 생성 직후 한 프레임 동안 엉뚱한 위치에 보이지 않도록 즉시 배치·갱신한다.
        HandleFollowAnchor();
        HandleFade();
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

    // 기준 월드 위치를 캔버스 좌표로 변환하고, 진행도만큼 위로 띄운 자리에 배치한다.
    private void HandleFollowAnchor()
    {
        if (_worldCamera == null || _parentRect == null) return;

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(_worldAnchor);
        _isBehindCamera = screenPoint.z < 0f;
        if (_isBehindCamera) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, screenPoint, _canvasCamera, out Vector2 localPoint))
            return;

        localPoint.y += _riseDistance * Progress;
        _rect.anchoredPosition = localPoint;
    }

    // 진행도에 따라 알파를 낮춘다. 기준 위치가 카메라 뒤에 있으면 보이지 않게 한다.
    private void HandleFade()
    {
        _canvasGroup.alpha = _isBehindCamera ? 0f : _alphaCurve.Evaluate(Progress);
    }

    // 지속 시간이 끝나면 스스로 파괴한다. (풀링을 쓰지 않으므로 인스턴스를 재사용하지 않는다)
    private void HandleLifetime()
    {
        if (_elapsed < _duration) return;

        Destroy(gameObject);
    }
}
