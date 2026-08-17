using System;
using UnityEngine;

// 플레이어의 오브젝트 선택 및 이동 명령을 처리하는 매니저
public class PlayerManager : MonoBehaviour
{
    public event Action<SelectableObject> OnSelected;
    public event Action OnDeselected;

    public event Action<Vector2, ClickableObject> OnMouseLeftClick;
    public event Action<Vector2, ClickableObject> OnMouseRightClick;

    // 우클릭을 누르고 있는 동안 갱신된 지점을 반복해서 알리는 이벤트
    public event Action<Vector2, ClickableObject> OnMouseRightHold;

    public ResourceInventory ResourceInventory => _resourceInventory;


    [SerializeField] private InputManager _inputManager;

    [SerializeField] private ResourceInventory _resourceInventory;

    private SelectableObject _currentSelection;


    private void OnEnable()
    {
        _inputManager.OnMouseLeftClick  += OnLeftClick;
        _inputManager.OnMouseRightClick += OnRightClick;
        _inputManager.OnMouseRightHold  += OnRightHold;
    }

    private void OnDisable()
    {
        _inputManager.OnMouseLeftClick  -= OnLeftClick;
        _inputManager.OnMouseRightClick -= OnRightClick;
        _inputManager.OnMouseRightHold  -= OnRightHold;
    }

    // 좌클릭 시 오브젝트 클릭 처리 및 선택 가능 여부에 따라 선택/해제
    private void OnLeftClick(Vector2 pos, ClickableObject clickable)
    {
        if (clickable == null)
        {
            Deselect();
        }
        else
        {
            clickable.HandleClick(this);
            var selectable = clickable.GetComponent<SelectableObject>();
            if (selectable != null)
                Select(selectable);
            else
                Deselect();
        }

        OnMouseLeftClick?.Invoke(pos, clickable);
    }

    // 우클릭 이벤트를 재발행
    private void OnRightClick(Vector2 pos, ClickableObject clickable)
    {
        OnMouseRightClick?.Invoke(pos, clickable);
    }

    // 우클릭 홀드 갱신 이벤트를 재발행
    private void OnRightHold(Vector2 pos, ClickableObject clickable)
    {
        OnMouseRightHold?.Invoke(pos, clickable);
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

    // 클릭이 아닌 코드 경로(건축 위치 지정 모드 등)에서 대상을 선택 상태로 전환한다.
    public void SelectExternally(SelectableObject target) => Select(target);

    // 클릭이 아닌 코드 경로(건축 위치 지정 모드 등)에서 현재 선택을 해제한다.
    public void DeselectExternally() => Deselect();
}
