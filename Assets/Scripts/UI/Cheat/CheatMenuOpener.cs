using UnityEngine;
using UnityEngine.InputSystem;

// 정해진 키 입력을 감지해 치트 관리 화면(CheatManagementUI)을 UIManager로 연다.
// 인게임 씬에 배치해 두고 프리팹만 연결하면 된다. (PauseMenuOpener와 같은 규약)
public class CheatMenuOpener : MonoBehaviour
{
    [SerializeField] private CheatManagementUI _cheatMenuPrefab;

    // 치트 관리 화면을 여는 키
    [SerializeField] private Key _openKey = Key.F9;

    // 비워 두면 씬에서 찾아 쓴다.
    [SerializeField] private ResourceInventory _resourceInventory;
    [SerializeField] private ItemCodex _itemCodex;

    // 지금 열려 있는 치트 관리 화면. 열려 있는 동안 같은 키를 다시 눌러 여러 개가 열리는 것을 막는다.
    private CheatManagementUI _openMenu;

    private void Update()
    {
        HandleOpenInput();
    }

    // 지정한 키를 누르면 치트 관리 화면을 연다. 이미 열려 있으면 아무것도 하지 않는다.
    private void HandleOpenInput()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current[_openKey].wasPressedThisFrame) return;
        if (_openMenu != null && _openMenu.State != UIState.Closed) return;

        if (!TryResolveTargets()) return;

        _openMenu = UIManager.Instance.OpenUI(_cheatMenuPrefab, new CheatMenuData(_resourceInventory, _itemCodex));
    }

    // 치트를 적용할 인벤토리와 도감을 씬에서 찾아 채운다. 인벤토리가 없으면 화면을 열지 않는다.
    private bool TryResolveTargets()
    {
        if (_resourceInventory == null)
            _resourceInventory = FindFirstObjectByType<ResourceInventory>();

        if (_itemCodex == null)
            _itemCodex = FindFirstObjectByType<ItemCodex>();

        if (_resourceInventory == null)
        {
            Debug.LogWarning("CheatMenuOpener: 씬에서 ResourceInventory를 찾지 못해 치트 관리 화면을 열지 않습니다.", this);
            return false;
        }

        return true;
    }
}
