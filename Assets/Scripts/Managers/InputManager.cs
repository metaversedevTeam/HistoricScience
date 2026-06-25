using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public event Action<Vector2, ClickableObject> OnMouseLeftClick;
    public event Action<Vector2, ClickableObject> OnMouseRightClick;

    [SerializeField] private LayerMask _groundLayer;

    private Camera _cam;

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

    private void Update()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryFireClick(OnMouseLeftClick);

        if (Mouse.current.rightButton.wasPressedThisFrame)
            TryFireClick(OnMouseRightClick);
    }

    private void TryFireClick(Action<Vector2, ClickableObject> callback)
    {
        if (callback == null) return;

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        // ClickableObject 감지 (레이어 무관)
        ClickableObject clickable = null;
        if (Physics.Raycast(ray, out RaycastHit objHit, Mathf.Infinity))
            clickable = objHit.collider.GetComponentInParent<ClickableObject>();

        // Ground 레이어 클릭 위치
        if (Physics.Raycast(ray, out RaycastHit groundHit, Mathf.Infinity, _groundLayer))
            callback.Invoke(new Vector2(groundHit.point.x, groundHit.point.z), clickable);
    }
}
