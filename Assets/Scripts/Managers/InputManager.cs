using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 마우스 입력을 감지해 클릭 이벤트를 발행하는 매니저
public class InputManager : MonoBehaviour
{
    public event Action<Vector2, ClickableObject> OnMouseLeftClick;
    public event Action<Vector2, ClickableObject> OnMouseRightClick;

    // 우클릭을 누르고 있는 동안 갱신된 지점을 반복해서 알리는 이벤트
    public event Action<Vector2, ClickableObject> OnMouseRightHold;

    [SerializeField] private LayerMask _groundLayer;

    [Header("우클릭 홀드")]
    // 우클릭을 누른 뒤 홀드 갱신이 시작되기까지 기다리는 시간(초). 짧은 클릭이 홀드로 처리되지 않게 한다.
    [SerializeField] private float _holdStartDelay = 0.15f;
    // 홀드 중 갱신 이벤트를 발행하는 간격(초). 지점이 움직였는지와 무관하게 이 간격으로 계속 발행하므로,
    // 같은 지점의 반복 발행을 걸러내는 것은 이벤트를 받는 쪽의 몫이다.
    [SerializeField] private float _holdRepeatInterval = 0.1f;

    [Header("클릭 기즈모")]
    [SerializeField] private Color _leftClickColor = Color.green;
    [SerializeField] private Color _rightClickColor = Color.red;
    [SerializeField] private Color _objectClickColor = Color.yellow;
    [SerializeField] private Color _otherClickColor = Color.gray;
    [SerializeField] private float _gizmoDuration = 0.5f;
    [SerializeField] private float _maxGizmoDistance = 100f;

    private Camera _cam;

    // 다음 홀드 갱신을 발행할 시각
    private float _nextHoldFireTime;

    // Ground 레이어가 미설정된 경우 자동으로 찾아 할당
    private void Awake()
    {
        _cam = Camera.main;

        if (_groundLayer.value == 0)
        {
            int idx = LayerMask.NameToLayer("Ground");
            if (idx >= 0)
                _groundLayer = 1 << idx;
            else
                Debug.LogWarning("[InputManager] 'Ground' 레이어를 찾을 수 없습니다. Inspector에서 직접 설정해주세요.");
        }
    }

    // 매 프레임 마우스 버튼 입력과 우클릭 홀드를 감지해 콜백을 발행
    private void Update()
    {
        if (Mouse.current == null) return;

        HandleClickInput();
        HandleRightHoldInput();
    }

    // 버튼이 눌린 프레임에 좌·우클릭 콜백을 발행한다.
    private void HandleClickInput()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryFireClick(OnMouseLeftClick, _leftClickColor);

        if (Mouse.current.rightButton.wasPressedThisFrame)
            HandleRightPress();
    }

    // 우클릭 순간의 명령을 발행하고, 홀드 갱신이 시작될 시각을 유예 시간만큼 미뤄 둔다.
    private void HandleRightPress()
    {
        _nextHoldFireTime = Time.time + _holdStartDelay;

        TryFireClick(OnMouseRightClick, _rightClickColor);
    }

    // 우클릭을 누르고 있는 동안 일정 간격으로 현재 지점을 발행한다. 지점이 움직였는지는 보지 않으므로,
    // 같은 지점의 반복 발행을 걸러내는 일은 이벤트를 받는 쪽에서 각자의 기준으로 처리한다.
    private void HandleRightHoldInput()
    {
        if (!Mouse.current.rightButton.isPressed) return;
        if (OnMouseRightHold == null) return;

        if (Time.time < _nextHoldFireTime) return;
        _nextHoldFireTime = Time.time + _holdRepeatInterval;

        TryFireClick(OnMouseRightHold, _rightClickColor);
    }

    // 포인터가 UI 위에 올라가 있는지 반환한다.
    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    // 레이캐스트로 ClickableObject와 Ground 충돌 위치를 감지해 콜백 호출
    private void TryFireClick(Action<Vector2, ClickableObject> callback, Color gizmoColor)
    {
        if (callback == null) return;
        if (IsPointerOverUI()) return;

        if (TryResolveClickTarget(gizmoColor, out Vector2 pos, out ClickableObject clickable))
            callback.Invoke(pos, clickable);
    }

    // 현재 마우스 위치로 레이캐스트해 클릭 대상과 지면 좌표를 구하고 기즈모를 표시한다. 지면에 닿지 않으면 false를 반환한다.
    private bool TryResolveClickTarget(Color gizmoColor, out Vector2 groundPos, out ClickableObject clickable)
    {
        groundPos = default;
        clickable = null;

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            clickable = hit.collider.GetComponentInParent<ClickableObject>();
            DrawClickGizmo(hit, clickable, gizmoColor);
        }
        else
        {
            // 아무것도 감지되지 않은 경우 레이 방향으로 회색 선 표시
            Debug.DrawLine(_cam.transform.position, ray.GetPoint(_maxGizmoDistance), _otherClickColor, _gizmoDuration);
        }

        if (!Physics.Raycast(ray, out RaycastHit groundHit, Mathf.Infinity, _groundLayer))
            return false;

        groundPos = new Vector2(groundHit.point.x, groundHit.point.z);
        return true;
    }

    // 클릭 대상(Ground / ClickableObject / 그 외)에 따라 색을 달리해 선을 잠깐 표시
    private void DrawClickGizmo(RaycastHit hit, ClickableObject clickable, Color groundColor)
    {
        Color color;
        if (IsInGroundLayer(hit.collider.gameObject.layer))
            color = groundColor;
        else if (clickable != null)
            color = _objectClickColor;
        else
            color = _otherClickColor;

        Debug.DrawLine(_cam.transform.position, hit.point, color, _gizmoDuration);
    }

    // 지정한 레이어가 Ground 레이어 마스크에 포함되는지 반환
    private bool IsInGroundLayer(int layer)
    {
        return (_groundLayer.value & (1 << layer)) != 0;
    }
}
