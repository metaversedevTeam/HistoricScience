using System;
using UnityEngine;

// 플레이어의 마우스 클릭·이동 입력과 자원 인벤토리를 다른 컴포넌트에 전달하는 매니저
public class PlayerManager : MonoBehaviour
{
    // 씬에 하나만 두는 인스턴스. 런타임에 소환되는 시민은 씬 참조를 직렬화해 둘 수 없어, 선택되지 않은 상태에서도 이동 입력을 읽으려면 전역 접근 경로가 필요하다.
    public static PlayerManager Instance { get; private set; }

    public event Action<Vector2, ClickableObject> OnMouseLeftClick;
    public event Action<Vector2, ClickableObject> OnMouseRightClick;

    // 우클릭을 누르고 있는 동안 갱신된 지점을 반복해서 알리는 이벤트
    public event Action<Vector2, ClickableObject> OnMouseRightHold;

    public ResourceInventory ResourceInventory => _resourceInventory;

    // 플레이어가 조종하는 유닛을 직접 걷게 할 월드 XZ 방향. WASD 입력을 카메라 기준으로 바꾼 값이며, 입력이 없으면 0이다.
    public Vector2 MoveDirection => _inputManager.MoveDirection;

    // 플레이어가 달리기 키(Shift)를 누르고 있는지. 직접 걷는 동안 이 값이 true면 달린다.
    public bool IsRunning => _inputManager.IsRunning;

    // 이번 프레임에 상호작용 키(F)를 눌렀는지. 시민이 앞 대상의 명령 목록에서 선택된 명령을 실행할 때 쓴다.
    public bool IsInteractPressed => _inputManager.IsInteractPressed;

    [SerializeField] private InputManager _inputManager;

    [SerializeField] private ResourceInventory _resourceInventory;

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

    // 좌클릭한 오브젝트에 클릭을 알리고 좌클릭 이벤트를 재발행
    private void OnLeftClick(Vector2 pos, ClickableObject clickable)
    {
        if (clickable != null)
            clickable.HandleClick(this);

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
}
