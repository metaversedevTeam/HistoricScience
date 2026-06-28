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
            TryFireClick(OnMouseLeftClick);

        if (Mouse.current.rightButton.wasPressedThisFrame)
            TryFireClick(OnMouseRightClick);
    }

    // 레이캐스트로 ClickableObject와 Ground 충돌 위치를 감지해 콜백 호출
    private void TryFireClick(Action<Vector2, ClickableObject> callback)
    {
        if (callback == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        ClickableObject clickable = null;
        if (Physics.Raycast(ray, out RaycastHit objHit, Mathf.Infinity))
            clickable = objHit.collider.GetComponentInParent<ClickableObject>();

        if (Physics.Raycast(ray, out RaycastHit groundHit, Mathf.Infinity, _groundLayer))
            callback.Invoke(new Vector2(groundHit.point.x, groundHit.point.z), clickable);
    }
}
