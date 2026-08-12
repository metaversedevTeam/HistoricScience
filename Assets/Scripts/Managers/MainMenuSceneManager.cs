using UnityEngine;

// 메인 메뉴 씬의 진입점. 메인 메뉴 화면과 맵 관리 화면 중 하나만 켜 두며 둘 사이의 전환을 담당한다.
public class MainMenuSceneManager : MonoBehaviour
{
    [SerializeField] private MainMenuUI _mainMenuUI;
    [SerializeField] private MapManagementUI _mapManagementUI;

    private void Awake()
    {
        _mainMenuUI.PlayRequested += HandlePlayRequested;
        _mapManagementUI.BackRequested += HandleBackRequested;
    }

    private void Start()
    {
        HandleShowMainMenu();
    }

    private void OnDestroy()
    {
        _mainMenuUI.PlayRequested -= HandlePlayRequested;
        _mapManagementUI.BackRequested -= HandleBackRequested;
    }

    // PLAY를 누르면 맵 관리 화면으로 넘어간다.
    private void HandlePlayRequested()
    {
        HandleShowMapManagement();
    }

    // 뒤로가기를 누르면 메인 메뉴로 돌아온다.
    private void HandleBackRequested()
    {
        HandleShowMainMenu();
    }

    // 메인 메뉴 화면만 켠다.
    private void HandleShowMainMenu()
    {
        _mapManagementUI.gameObject.SetActive(false);
        _mainMenuUI.gameObject.SetActive(true);
    }

    // 맵 관리 화면만 켜고 저장된 맵 목록을 다시 읽는다.
    private void HandleShowMapManagement()
    {
        _mainMenuUI.gameObject.SetActive(false);
        _mapManagementUI.gameObject.SetActive(true);
        _mapManagementUI.Refresh();
    }
}
