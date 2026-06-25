using System;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public event Action<SelectableObject> OnSelected;
    public event Action OnDeselected;

    [SerializeField] private InputManager _inputManager;

    private SelectableObject _currentSelection;

    private void OnEnable()
    {
        _inputManager.OnMouseLeftClick += OnLeftClick;
    }

    private void OnDisable()
    {
        _inputManager.OnMouseLeftClick -= OnLeftClick;
    }

    private void OnLeftClick(Vector2 pos, ClickableObject clickable)
    {
        if (clickable == null)
        {
            Deselect();
            return;
        }

        clickable.HandleClick();
        var selectable = clickable.GetComponent<SelectableObject>();
        if (selectable != null)
            Select(selectable);
        else
            Deselect();
    }

    private void Select(SelectableObject target)
    {
        if (_currentSelection == target) return;

        Deselect();
        _currentSelection = target;
        _currentSelection.HandleSelect();
        OnSelected?.Invoke(_currentSelection);
    }

    private void Deselect()
    {
        if (_currentSelection == null) return;

        _currentSelection = null;
        OnDeselected?.Invoke();
    }
}
