using UnityEngine;
using UnityEngine.InputSystem;

// 따라갈 대상(리그 루트) 뒤쪽 일정 거리에 카메라를 두는 3인칭 카메라. 커서를 화면 중앙에 잠그고 마우스 이동으로 방향을 돌리며(Alt를 누르면 커서 해제),
// 벽이나 지형에 가리면 카메라를 앞으로 당긴다. 리그 루트의 스케일을 확대 배율로 보고 거리에 곱한다.
// 리그 루트(CameraController)가 시민 위치로 옮겨진 뒤에 배치하도록 그보다 늦게 실행한다.
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(110)]
public class ThirdPersonCamera : MonoBehaviour
{
    // 카메라가 장애물에 너무 붙지 않도록 남겨 두는 여유 거리(m)
    private const float k_CollisionPadding = 0.1f;

    // 장애물 감지에 한 번에 받을 최대 충돌 수
    private const int k_MaxCollisionHits = 16;

    [Tooltip("카메라가 따라갈 기준점. 보통 플레이어 위치를 따라가는 CameraController 오브젝트이며, 비워 두면 부모를 쓴다")]
    [SerializeField] private Transform _target;

    [Tooltip("기준점에서 회전축(어깨)까지의 높이(m)")]
    [SerializeField] private float _pivotHeight = 1.5f;

    [Tooltip("회전축에서 카메라까지의 기본 거리(m). 기준점 스케일(휠 확대 배율)이 곱해진다")]
    [SerializeField] private float _distance = 4f;

    [Tooltip("회전축 기준 카메라를 옆·위로 옮길 거리(m). x를 키우면 어깨 너머 시점이 된다")]
    [SerializeField] private Vector2 _shoulderOffset = new Vector2(0.5f, 0.3f);

    [Header("시점")]
    [Tooltip("시작할 때 카메라가 내려다보는 각도(도). 0이면 수평이다")]
    [SerializeField] private float _pitch = 20f;

    [Tooltip("시작할 때 카메라가 바라보는 월드 방위각(도). 0이면 +Z 방향을 본다")]
    [SerializeField] private float _yaw;

    [Tooltip("마우스 이동 1픽셀당 회전 각도(도). 설정 화면의 감도로 정해진 축별 배율이 곱해진다")]
    [SerializeField] private float _lookSensitivity = 0.1f;

    [Tooltip("위아래로 돌릴 수 있는 최대 각도(도)")]
    [SerializeField, Range(10f, 89f)] private float _maxPitch = 85f;

    [Tooltip("마우스로 정한 목표 방향을 카메라가 따라잡는 데 걸리는 시간(초). 클수록 부드럽고 느리게 따라가며, 0이면 즉시 따라간다")]
    [SerializeField, Min(0f)] private float _lookSmoothTime = 0.08f;

    [Header("장애물")]

    [Tooltip("카메라를 가리는 장애물로 볼 레이어")]
    [SerializeField] private LayerMask _collisionLayers = ~(1 << 2);

    [Tooltip("장애물 감지에 쓰는 구의 반경(m). 카메라 근평면이 벽을 뚫고 보이지 않을 만큼 잡는다")]
    [SerializeField] private float _collisionRadius = 0.25f;

    [Tooltip("장애물이 사라진 뒤 카메라가 원래 거리로 돌아가는 속도(m/s)")]
    [SerializeField] private float _returnSpeed = 8f;

    [Tooltip("장애물 판정에서 제외할 대상. CameraController가 따라가는 플레이어를 넣어 준다")]
    [SerializeField] private Transform _ignoreRoot;

    // 지금 적용 중인 회전축-카메라 거리. 장애물에 막히면 즉시 줄고, 풀리면 천천히 늘어난다.
    private float _currentDistance;

    [Header("고정 시점")]
    [Tooltip("작업대처럼 외부에서 지정한 고정 시점으로 옮겨 가거나 돌아오는 데 걸리는 시간(초)")]
    [SerializeField, Min(0.01f)] private float _viewOverrideBlendTime = 0.6f;

    // 외부에서 지정한 고정 시점을 쓰는 중인지 여부. 풀리면 비중이 0으로 내려가며 3인칭 시점으로 돌아온다.
    private bool _hasViewOverride;

    // 고정 시점의 월드 위치
    private Vector3 _overridePosition;

    // 고정 시점의 월드 회전
    private Quaternion _overrideRotation = Quaternion.identity;

    // 고정 시점이 3인칭 시점을 덮는 비중(0~1). 실제 시간으로 오르내린다.
    private float _viewOverrideWeight;

    // 장애물 감지 결과를 담아 둘 재사용 버퍼
    private readonly RaycastHit[] _hits = new RaycastHit[k_MaxCollisionHits];

    // 직전 프레임에 커서가 풀려 있었는지 여부. 커서가 다시 잠기는 프레임의 마우스 이동량은 튀므로 버린다.
    private bool _wasCursorFree = true;

    // 마우스 입력이 쌓이는 목표 방위각(도). 실제 방위각(_yaw)이 이 값을 보간해 따라간다.
    private float _targetYaw;

    // 마우스 입력이 쌓이는 목표 내려다보기 각도(도). 실제 각도(_pitch)가 이 값을 보간해 따라간다.
    private float _targetPitch;

    // 카메라가 지금 바라보는(보간된) 월드 방위각(도). 플레이어가 카메라와 같은 쪽을 보게 할 때 쓴다.
    public float Yaw => _yaw;

    // 장애물 판정에서 제외할 대상. 따라가는 플레이어가 바뀔 때 CameraController가 갱신한다.
    public Transform IgnoreRoot
    {
        get => _ignoreRoot;
        set => _ignoreRoot = value;
    }

    private void Awake()
    {
        if (_target == null)
            _target = transform.parent;

        _currentDistance = GetZoomedDistance();
        _pitch = Mathf.Clamp(_pitch, -_maxPitch, _maxPitch);
        _targetYaw = _yaw;
        _targetPitch = _pitch;
    }

    // 컴포넌트가 꺼지면 커서를 원래대로 풀어 메뉴 화면 등에서 커서가 사라진 채 남지 않게 한다.
    private void OnDisable()
    {
        ApplyCursorState(true);
    }

    private void LateUpdate()
    {
        HandleLook();
        HandleSmoothLook();
        HandleFollowTarget();
        HandleViewOverride();
    }

    // 카메라를 지정한 위치·회전의 고정 시점으로 부드럽게 옮긴다. ClearViewOverride를 부를 때까지 유지된다.
    public void SetViewOverride(Vector3 position, Quaternion rotation)
    {
        _hasViewOverride = true;
        _overridePosition = position;
        _overrideRotation = rotation;
    }

    // 고정 시점을 풀어 3인칭 시점으로 부드럽게 되돌린다.
    public void ClearViewOverride()
    {
        _hasViewOverride = false;
    }

    // 고정 시점 비중을 실제 시간으로 올리거나 내리고, 3인칭 배치와 고정 시점 사이를 그 비중으로 섞는다.
    private void HandleViewOverride()
    {
        float step = Time.unscaledDeltaTime / _viewOverrideBlendTime;
        _viewOverrideWeight = Mathf.MoveTowards(_viewOverrideWeight, _hasViewOverride ? 1f : 0f, step);
        if (_viewOverrideWeight <= 0f) return;

        float t = Mathf.SmoothStep(0f, 1f, _viewOverrideWeight);
        Vector3 position = Vector3.Lerp(transform.position, _overridePosition, t);
        Quaternion rotation = Quaternion.Slerp(transform.rotation, _overrideRotation, t);
        transform.SetPositionAndRotation(position, rotation);
    }

    // Alt를 누르고 있지 않고 조작을 막는 UI(창고·건물 선택 등)도 열려 있지 않으면 커서를 잠그고, 잠긴 동안 마우스 이동량으로 목표 좌우·위아래 방향을 돌린다.
    private void HandleLook()
    {
        bool isCursorFree = IsAltPressed() || UIManager.Instance.HasBlockingUI;
        bool justLocked = _wasCursorFree && !isCursorFree;
        _wasCursorFree = isCursorFree;

        ApplyCursorState(isCursorFree);
        if (isCursorFree || justLocked) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // 기본 감도에 설정 화면의 감도로 정해진 실제 배율을 축별로 곱한다. (수직은 수평의 절반)
        Vector2 delta = mouse.delta.ReadValue() * _lookSensitivity;
        _targetYaw += delta.x * GameSettings.AppliedHorizontalLookSensitivity;
        _targetPitch = Mathf.Clamp(_targetPitch - delta.y * GameSettings.AppliedVerticalLookSensitivity, -_maxPitch, _maxPitch);
    }

    // 실제 방향을 목표 방향 쪽으로 프레임 속도와 무관하게 지수 보간한다. 커서가 풀려도 남은 거리는 마저 따라간다.
    private void HandleSmoothLook()
    {
        if (_lookSmoothTime <= 0f)
        {
            _yaw = _targetYaw;
            _pitch = _targetPitch;
            return;
        }

        float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / _lookSmoothTime);
        _yaw = Mathf.Lerp(_yaw, _targetYaw, t);
        _pitch = Mathf.Lerp(_pitch, _targetPitch, t);
    }

    // 왼쪽이나 오른쪽 Alt 키를 누르고 있는지 확인한다.
    private bool IsAltPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
    }

    // 커서를 풀면 보이게, 잠그면 화면 중앙에 고정하고 숨긴다.
    private void ApplyCursorState(bool isFree)
    {
        CursorLockMode mode = isFree ? CursorLockMode.None : CursorLockMode.Locked;
        if (Cursor.lockState != mode)
            Cursor.lockState = mode;

        Cursor.visible = isFree;
    }

    // 기준점 위치와 현재 방향으로 장애물을 피한 카메라 위치를 구해 적용한다.
    private void HandleFollowTarget()
    {
        if (_target == null) return;

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 pivot = _target.position + Vector3.up * _pivotHeight;
        Vector3 shoulder = pivot + rotation * new Vector3(_shoulderOffset.x, _shoulderOffset.y, 0f);
        Vector3 back = rotation * Vector3.back;

        float maxDistance = GetZoomedDistance();
        float targetDistance = GetUnblockedDistance(shoulder, back, maxDistance);
        _currentDistance = targetDistance < _currentDistance
            ? targetDistance
            : Mathf.MoveTowards(_currentDistance, targetDistance, _returnSpeed * Time.deltaTime);

        transform.SetPositionAndRotation(shoulder + back * _currentDistance, rotation);
    }

    // 기준점의 스케일(휠 확대 배율)을 곱한 기본 거리를 반환한다.
    private float GetZoomedDistance()
    {
        float zoom = _target != null ? _target.localScale.x : 1f;
        return _distance * zoom;
    }

    // 어깨 지점에서 뒤쪽으로 구를 쏴 장애물에 막히지 않는 최대 거리를 구한다. 따라가는 플레이어 자신은 장애물로 보지 않는다.
    private float GetUnblockedDistance(Vector3 origin, Vector3 direction, float maxDistance)
    {
        int count = Physics.SphereCastNonAlloc(origin, _collisionRadius, direction, _hits, maxDistance, _collisionLayers, QueryTriggerInteraction.Ignore);

        float nearest = maxDistance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _hits[i];
            if (_ignoreRoot != null && hit.transform.IsChildOf(_ignoreRoot)) continue;
            if (hit.distance <= 0f) continue;

            nearest = Mathf.Min(nearest, hit.distance - k_CollisionPadding);
        }

        return Mathf.Max(0f, nearest);
    }
}
