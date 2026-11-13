using UnityEngine;
using UnityEngine.InputSystem;

// ESC 입력을 감지해 일시정지 화면(PauseMenuUI)을 UIManager로 연다. 인게임 씬에 배치해 두고 프리팹만 연결하면 된다.
public class PauseMenuOpener : MonoBehaviour
{
    [SerializeField] private PauseMenuUI _pauseMenuPrefab;

    private void Update()
    {
        HandleEscapeInput();
    }

    // 조작을 막는 관리형 UI가 하나도 열려 있지 않을 때만 ESC로 일시정지 화면을 연다. (열려 있는 UI의 ESC 닫기는 UIManager가 이미 처리한다)
    // 건물 배치 중에는 ESC가 배치 취소로 쓰이므로 열지 않는다. 건설 비용처럼 보여 주기만 하는 UI는 막지 않는다.
    private void HandleEscapeInput()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (UIManager.Instance.HasBlockingUI) return;
        if (BuildingPlacementController.ActiveCount > 0) return;

        UIManager.Instance.OpenUI(_pauseMenuPrefab);
    }
}
