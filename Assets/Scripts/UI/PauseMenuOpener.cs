using UnityEngine;
using UnityEngine.InputSystem;

// ESC 입력을 감지해 일시정지 화면(PauseMenuUI)을 UIManager로 연다. 인게임 씬에 배치해 두고 프리팹만 연결하면 된다.
public class PauseMenuOpener : MonoBehaviour
{
    [SerializeField] private PauseMenuUI _pauseMenuPrefab;

    // 마지막으로 연 일시정지 화면. 열려 있거나 닫히는 중이면 ESC로 다시 열지 않는 데 쓴다.
    private PauseMenuUI _openedPauseMenu;

    private void Update()
    {
        HandleEscapeInput();
    }

    // 조작을 막는 관리형 UI가 하나도 열려 있지 않을 때만 ESC로 일시정지 화면을 연다. (열려 있는 UI의 ESC 닫기는 UIManager가 이미 처리한다)
    // 건물 배치 중에는 ESC가 배치 취소로 쓰이므로 열지 않는다. 건설 비용처럼 보여 주기만 하는 UI는 막지 않는다.
    // 일시정지 화면이 열려 있거나 닫히는 중이면 그 ESC는 UIManager의 닫기에 쓰인 것이므로 다시 열지 않는다.
    // 다른 UI를 닫는 데 쓰인 ESC도 마찬가지로 일시정지 화면을 열지 않는다.
    // (UIManager는 닫기가 시작되는 순간 활성 목록에서 빼므로 HasBlockingUI만으로는 같은 프레임의 재열기를 막지 못한다)
    private void HandleEscapeInput()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (_openedPauseMenu != null && _openedPauseMenu.State != UIState.Closed) return;
        if (UIManager.Instance.IsEscapeConsumedThisFrame) return;
        if (UIManager.Instance.HasBlockingUI) return;
        if (BuildingPlacementController.ActiveCount > 0) return;

        _openedPauseMenu = UIManager.Instance.OpenUI(_pauseMenuPrefab);
    }
}
