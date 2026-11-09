using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 3인칭 카메라 리그의 루트. 플레이어 시민의 위치를 따라가고, 휠 확대 배율을 스케일로 들고 있으며, 카메라 위치를 저장한다.
// 실제 카메라 배치는 자식 카메라의 ThirdPersonCamera가 이 오브젝트를 기준으로 정한다.
public class CameraController : MonoBehaviour, ISavable
{
    // 따라갈 시민이 없을 때 다시 찾아볼 간격(초)
    private const float k_TargetSearchInterval = 0.5f;

    [SerializeField] private PlayerManager _playerManager;

    [Tooltip("자식 카메라의 3인칭 배치 컴포넌트. 따라가는 플레이어를 장애물 판정에서 빼도록 알려 준다. 비워 두면 자식에서 찾는다")]
    [SerializeField] private ThirdPersonCamera _thirdPersonCamera;

    [Header("휠 확대/축소")]
    [Tooltip("휠로 줄일 수 있는 최소 크기. 가장 확대된 상태이다")]
    [SerializeField] private float _minZoomScale = 0.5f;

    [Tooltip("휠로 키울 수 있는 최대 크기. 가장 축소된 상태이며, 런타임에 MaxZoomScale로 바꿀 수 있다")]
    [SerializeField] private float _maxZoomScale = 2f;

    [Tooltip("휠을 한 번 굴릴 때 변하는 크기")]
    [SerializeField] private float _zoomStep = 0.1f;

    [Tooltip("최대 축소 크기를 늘려 더 멀리까지 보게 해 주는 연구 보너스. 비워 두면 위의 최대 크기를 그대로 쓴다")]
    [SerializeField] private ResearchBonusData _maxZoomBonus;

    // 연구 보너스를 곱하기 전의 최대 축소 크기. 보너스가 바뀔 때마다 이 값에서 다시 계산한다.
    private float _baseMaxZoomScale;

    // 지금 따라가는 플레이어 시민
    private Transform _followTarget;

    // 따라갈 시민을 다음에 다시 찾아볼 시각
    private float _nextTargetSearchTime;

    private void Awake()
    {
        if (_thirdPersonCamera == null)
            _thirdPersonCamera = GetComponentInChildren<ThirdPersonCamera>();

        _baseMaxZoomScale = _maxZoomScale;
        ApplyZoomScale(transform.localScale.x);
    }

    private void OnEnable()
    {
        _playerManager.OnSelected += OnUnitSelected;

        HandleSubscribeMaxZoomBonus();
    }

    private void OnDisable()
    {
        _playerManager.OnSelected -= OnUnitSelected;

        HandleUnsubscribeMaxZoomBonus();
    }

    // 최대 축소 보너스가 지정되어 있으면 갱신 이벤트를 구독하고 현재 합계를 곧바로 반영한다.
    private void HandleSubscribeMaxZoomBonus()
    {
        if (_maxZoomBonus == null) return;

        ResearchManager.Instance.OnBonusesChanged += ApplyMaxZoomBonus;
        ApplyMaxZoomBonus();
    }

    // 구독해 둔 연구 보너스 갱신 이벤트를 해제한다.
    private void HandleUnsubscribeMaxZoomBonus()
    {
        if (_maxZoomBonus == null) return;

        ResearchManager.Instance.OnBonusesChanged -= ApplyMaxZoomBonus;
    }

    // 합산된 최대 축소 보너스를 기본 최대 크기에 곱해 반영한다. 현재 배율을 범위 안으로 맞추는 일은 MaxZoomScale 세터가 처리한다.
    private void ApplyMaxZoomBonus()
    {
        MaxZoomScale = _baseMaxZoomScale * ResearchManager.Instance.GetMultiplier(_maxZoomBonus);
    }

    private void LateUpdate()
    {
        HandleZoom();
        HandleFollowPlayer();
    }

    // 휠로 키울 수 있는 최대 크기. 런타임에 바꿀 수 있으며, 값을 바꾸면 현재 크기도 즉시 범위 안으로 맞춘다
    public float MaxZoomScale
    {
        get => _maxZoomScale;
        set
        {
            _maxZoomScale = Mathf.Max(value, _minZoomScale);
            ApplyZoomScale(transform.localScale.x);
        }
    }

    // 마우스 휠 입력으로 이 오브젝트의 크기를 조절해 확대/축소한다. 자식 3인칭 카메라는 이 크기를 거리 배율로 쓴다
    private void HandleZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        // 휠을 위로 굴리면 크기를 줄여 확대하고, 아래로 굴리면 크기를 키워 축소한다
        ApplyZoomScale(transform.localScale.x - Mathf.Sign(scroll) * _zoomStep);
    }

    // 지정한 크기를 최소/최대 범위로 고정해 균등 스케일로 적용한다
    private void ApplyZoomScale(float scale)
    {
        transform.localScale = Vector3.one * Mathf.Clamp(scale, _minZoomScale, _maxZoomScale);
    }

    // 따라갈 시민이 없으면 찾아 두고, 있으면 이 리그를 그 시민 위치로 옮긴다.
    private void HandleFollowPlayer()
    {
        if (_followTarget == null)
            TryFindFollowTarget();

        if (_followTarget == null) return;

        transform.position = _followTarget.position;
    }

    // 일정 간격으로 씬의 시민을 찾아 따라갈 대상으로 정한다. 저장된 시민은 청크가 로드된 뒤에 소환되므로 처음 몇 프레임은 없을 수 있다.
    private void TryFindFollowTarget()
    {
        if (Time.unscaledTime < _nextTargetSearchTime) return;
        _nextTargetSearchTime = Time.unscaledTime + k_TargetSearchInterval;

        Citizen citizen = FindFirstObjectByType<Citizen>();
        if (citizen != null)
            SetFollowTarget(citizen.transform);
    }

    // 따라갈 대상을 바꾸고, 3인칭 카메라가 그 대상을 장애물로 보지 않도록 알려 준다.
    private void SetFollowTarget(Transform target)
    {
        _followTarget = target;

        if (_thirdPersonCamera != null)
            _thirdPersonCamera.IgnoreRoot = target;
    }

    // 시민을 선택하면 그 시민을 따라갈 대상으로 바꾼다. 시민이 아닌 오브젝트를 선택하면 지금 대상을 그대로 따라간다.
    private void OnUnitSelected(SelectableObject selected)
    {
        if (selected == null || selected.GetComponent<Citizen>() == null) return;

        SetFollowTarget(selected.transform);
    }

    // 지정한 XZ 좌표로 리그를 즉시 옮긴다. 따라갈 시민이 아직 없을 때 시작 위치를 잡는 데 쓴다
    public void MoveTo(Vector2 xzPosition)
    {
        transform.position = new Vector3(xzPosition.x, transform.position.y, xzPosition.y);
    }

    // 지정한 X, Z 좌표로 리그를 즉시 옮긴다.
    public void MoveTo(float x, float z)
    {
        MoveTo(new Vector2(x, z));
    }

    // 씬에 상주하는 객체라 프리팹 소환에 쓰이지 않는 고정 식별자
    public string PrefabId => "CameraController";

    // 현재 리그의 XZ 위치를 JSON 문자열로 캡처한다.
    public string CaptureJson()
    {
        SaveState state = new SaveState { Position = new Vector2(transform.position.x, transform.position.z) };
        return JsonUtility.ToJson(state);
    }

    // JSON 문자열에서 리그의 XZ 위치를 복원한다. 시민이 소환되면 곧바로 그 위치를 따라간다.
    public void ApplyJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        SaveState state = JsonUtility.FromJson<SaveState>(json);
        if (state == null) return;

        MoveTo(state.Position);
    }

    // 카메라 위치 저장 상태의 직렬화 래퍼
    [Serializable]
    private class SaveState
    {
        public Vector2 Position;
    }
}
