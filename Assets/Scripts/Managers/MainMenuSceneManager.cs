using UnityEngine;

// 메인 메뉴 씬의 진입점. 메인 메뉴 본문과 맵 선택 화면 중 하나만 보여 주며,
// 둘 사이를 왼쪽에서 나오는 사각형 전환 연출(MenuWipeTransition)로 넘긴다. 배경 모션그래픽은 두 화면이 함께 쓴다.
public class MainMenuSceneManager : MonoBehaviour
{
    [SerializeField] private MainMenuUI _mainMenuUI;
    [SerializeField] private MapSelectUI _mapSelectUI;

    // 화면을 바꿀 때 쓰는 사각형 전환 연출
    [SerializeField] private MenuWipeTransition _transition;

    // 맵 선택 화면으로 넘어갈 때 전환 사각형에 띄울 글자
    [SerializeField] private string _mapSelectCaption = "SELECT MAP";

    // 메인 메뉴로 돌아갈 때 전환 사각형에 띄울 글자
    [SerializeField] private string _mainMenuCaption = "MAIN MENU";

    private void Awake()
    {
        _mainMenuUI.PlayRequested += HandlePlayRequested;
        _mapSelectUI.BackRequested += HandleBackRequested;
    }

    private void Start()
    {
        HandleShowMainMenu();
    }

    private void OnDestroy()
    {
        _mainMenuUI.PlayRequested -= HandlePlayRequested;
        _mapSelectUI.BackRequested -= HandleBackRequested;
    }

    // 시작을 누르면 사각형이 메뉴를 감싼 뒤 맵 선택 화면을 드러낸다.
    private void HandlePlayRequested()
    {
        _transition.Play(_mapSelectCaption, HandleShowMapSelect);
    }

    // 뒤로가기를 누르면 사각형이 맵 선택 화면을 감싼 뒤 메인 메뉴를 드러낸다.
    private void HandleBackRequested()
    {
        _transition.Play(_mainMenuCaption, HandleShowMainMenu);
    }

    // 맵 선택 화면을 끄고 메인 메뉴 본문을 켠다.
    private void HandleShowMainMenu()
    {
        _mapSelectUI.gameObject.SetActive(false);
        _mainMenuUI.gameObject.SetActive(true);
        _mainMenuUI.SetMenuVisible(true);
    }

    // 메인 메뉴 본문을 숨기고 맵 선택 화면을 켠 뒤 저장된 맵 목록을 다시 읽는다.
    private void HandleShowMapSelect()
    {
        _mainMenuUI.SetMenuVisible(false);
        _mapSelectUI.gameObject.SetActive(true);
        _mapSelectUI.Refresh();
    }
}
