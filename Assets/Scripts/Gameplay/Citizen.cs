using UnityEngine;

// 시민 유닛을 나타내며 선택된 상태에서 우클릭 시 대상 또는 위치로 이동을 명령하는 컴포넌트
public class Citizen : MonoBehaviour
{
    private SelectableObject _selectable;
    private IMover _mover;
    private PlayerManager _selectedBy;

    // SelectableObject와 IMover 컴포넌트를 캐싱
    private void Awake()
    {
        _selectable = GetComponent<SelectableObject>();
        _mover = GetComponent<IMover>();
    }

    // 자신의 선택 이벤트를 구독
    private void OnEnable()
    {
        _selectable.OnSelect += HandleSelect;
    }

    // 자신의 선택 이벤트와 PlayerManager 구독을 해제
    private void OnDisable()
    {
        _selectable.OnSelect -= HandleSelect;
        UnsubscribeFromPlayer();
    }

    // 선택되었을 때 해당 PlayerManager의 우클릭/선택해제 이벤트를 구독
    private void HandleSelect(PlayerManager playerManager)
    {
        UnsubscribeFromPlayer();

        _selectedBy = playerManager;
        _selectedBy.OnMouseRightClick += HandleRightClick;
        _selectedBy.OnDeselected += HandleDeselected;
    }

    // 선택 해제 시 PlayerManager 구독을 해제
    private void HandleDeselected()
    {
        UnsubscribeFromPlayer();
    }

    // 우클릭한 대상이 있으면 그 대상을 추적하고, 없으면 클릭한 위치로 이동
    private void HandleRightClick(Vector2 pos, ClickableObject clickable)
    {
        if (clickable != null)
            _mover.Move(clickable.transform);
        else
            _mover.Move(pos);
    }

    // 구독 중인 PlayerManager 이벤트를 해제하고 참조를 비움
    private void UnsubscribeFromPlayer()
    {
        if (_selectedBy == null) return;

        _selectedBy.OnMouseRightClick -= HandleRightClick;
        _selectedBy.OnDeselected -= HandleDeselected;
        _selectedBy = null;
    }
}
