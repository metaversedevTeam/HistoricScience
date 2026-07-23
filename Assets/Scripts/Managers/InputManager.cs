using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 마우스 입력을 감지해 클릭 이벤트를 발행하는 매니저
public class InputManager : MonoBehaviour
{
    public event Action<Vector2, ClickableObject> OnMouseLeftClick;
    public event Action<Vector2, ClickableObject> OnMouseRightClick;

    [SerializeField] private LayerMask _groundLayer;

    [Header("클릭 기즈모")]
    [SerializeField] private Color _leftClickColor = Color.green;
    [SerializeField] private Color _rightClickColor = Color.red;
    [SerializeField] private Color _objectClickColor = Color.yellow;
    [SerializeField] private Color _otherClickColor = Color.gray;
    [SerializeField] private float _gizmoDuration = 0.5f;
    [SerializeField] private float _maxGizmoDistance = 100f;

    private Camera _cam;

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

    // 매 프레임 마우스 버튼 입력을 감지해 클릭 콜백을 발행
    private void Update()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryFireClick(OnMouseLeftClick, _leftClickColor);

        if (Mouse.current.rightButton.wasPressedThisFrame)
            TryFireClick(OnMouseRightClick, _rightClickColor);
    }

    // 레이캐스트로 ClickableObject와 Ground 충돌 위치를 감지해 콜백 호출
    private void TryFireClick(Action<Vector2, ClickableObject> callback, Color gizmoColor)
    {
        if (callback == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        ClickableObject clickable = null;
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

        if (Physics.Raycast(ray, out RaycastHit groundHit, Mathf.Infinity, _groundLayer))
            callback.Invoke(new Vector2(groundHit.point.x, groundHit.point.z), clickable);
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
