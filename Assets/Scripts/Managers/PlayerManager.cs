using System;
using UnityEngine;

// 플레이어의 오브젝트 선택 및 이동 명령을 처리하는 매니저
public class PlayerManager : MonoBehaviour
{
    // 씬에 하나만 두는 인스턴스. 런타임에 소환되는 시민은 씬 참조를 직렬화해 둘 수 없어, 선택되지 않은 상태에서도 이동 입력을 읽으려면 전역 접근 경로가 필요하다.
    public static PlayerManager Instance { get; private set; }

    public event Action<SelectableObject> OnSelected;
    public event Action OnDeselected;

    public event Action<Vector2, ClickableObject> OnMouseLeftClick;
    public event Action<Vector2, ClickableObject> OnMouseRightClick;

    // 우클릭을 누르고 있는 동안 갱신된 지점을 반복해서 알리는 이벤트
    public event Action<Vector2, ClickableObject> OnMouseRightHold;

    public ResourceInventory ResourceInventory => _resourceInventory;

    // 플레이어가 조종하는 유닛을 직접 걷게 할 월드 XZ 방향. WASD 입력을 카메라 기준으로 바꾼 값이며, 입력이 없으면 0이다.
    public Vector2 MoveDirection => _inputManager.MoveDirection;

    // 플레이어가 달리기 키(Shift)를 누르고 있는지. 직접 걷는 동안 이 값이 true면 달린다.
    public bool IsRunning => _inputManager.IsRunning;

    [SerializeField] private InputManager _inputManager;

    [SerializeField] private ResourceInventory _resourceInventory;

    private SelectableObject _currentSelection;

    // 전역 접근 경로로 자신을 등록한다
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("PlayerManager: 씬에 인스턴스가 둘 이상 있어 먼저 등록된 쪽을 그대로 사용합니다.", this);
            return;
        }

        Instance = this;
    }

    // 씬을 벗어날 때 파괴된 자신이 전역 경로에 남지 않도록 등록을 해제한다
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

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
