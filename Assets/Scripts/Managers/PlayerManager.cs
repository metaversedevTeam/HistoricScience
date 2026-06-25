using System;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public event Action<SelectableObject> OnSelected;
    public event Action OnDeselected;

    [SerializeField] private InputManager _inputManager;

    private SelectableObject _currentSelection;

    // InputManager 클릭 이벤트 구독
    private void OnEnable()
    {
        _inputManager.OnMouseLeftClick  += OnLeftClick;
        _inputManager.OnMouseRightClick += OnRightClick;
    }

    // InputManager 클릭 이벤트 구독 해제
    private void OnDisable()
    {
        _inputManager.OnMouseLeftClick  -= OnLeftClick;
        _inputManager.OnMouseRightClick -= OnRightClick;
    }

    // 좌클릭 시 오브젝트 클릭 처리 및 선택 가능 여부에 따라 선택/해제
    private void OnLeftClick(Vector2 pos, ClickableObject clickable)
    {
        if (clickable == null)
        {
            Deselect();
            return;
        }

        clickable.HandleClick(this);
        var selectable = clickable.GetComponent<SelectableObject>();
        if (selectable != null)
            Select(selectable);
        else
            Deselect();
    }

    // 우클릭 시 선택된 유닛을 오브젝트 추적 또는 지정 위치로 이동
    private void OnRightClick(Vector2 pos, ClickableObject clickable)
    {
        if (_currentSelection == null) return;

        var mover = _currentSelection.GetComponent<IMover>();
        if (mover == null) return;

        if (clickable != null)
            mover.Move(clickable.transform);
        else
            mover.Move(pos);
    }

    // 대상을 현재 선택으로 설정하고 OnSelected 이벤트 발행
    private void Select(SelectableObject target)
    {
        if (_currentSelection == target) return;

        Deselect();
        _currentSelection = target;
        _currentSelection.HandleSelect(this);
        OnSelected?.Invoke(_currentSelection);
    }

    // 현재 선택을 해제하고 OnDeselected 이벤트 발행
    private void Deselect()
    {
        if (_currentSelection == null) return;

        _currentSelection = null;
        OnDeselected?.Invoke();
    }
}
