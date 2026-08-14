using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 일시정지 화면(Figma의 pause-menu-ui). UIManager로 여닫는 관리형 UI이며, 열려 있는 동안 Time.timeScale을 0으로 고정해 게임을 멈춘다.
public class PauseMenuUI : OpenableUIBase
{
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _saveAndExitButton;
    [SerializeField] private Button _optionButton;
    [SerializeField] private Button _quitToMenuButton;
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    private IngameSceneManager _sceneManager;

    private void Awake()
    {
        _resumeButton.onClick.AddListener(HandleResumeClick);
        _saveAndExitButton.onClick.AddListener(HandleSaveAndExitClick);
        _quitToMenuButton.onClick.AddListener(HandleQuitToMenuClick);

        // OPTION은 아직 기능이 없어 잠금 상태로만 보여 준다. (MainMenuUI의 OPTION과 동일한 규약)
        _optionButton.interactable = false;
    }

    // 열리는 즉시 게임을 멈춘다. (연출 없음)
    protected override void PlayOpenTransition()
    {
        _sceneManager = FindFirstObjectByType<IngameSceneManager>();
        Time.timeScale = 0f;
        FinishOpen();
    }

    // 닫히는 즉시 게임을 다시 진행시킨다. (연출 없음)
    protected override void PlayCloseTransition()
    {
        Time.timeScale = 1f;
        FinishClose();
    }

    private void HandleResumeClick() => Close();

    // 현재 맵을 저장한 뒤 메인 메뉴로 나간다.
    private void HandleSaveAndExitClick()
    {
        if (_sceneManager != null)
            _sceneManager.SaveMap(_sceneManager.CurrentSlot);

        HandleQuitToMenuClick();
    }

    // 시간 배속을 되돌리고 메인 메뉴 씬으로 나간다.
    private void HandleQuitToMenuClick()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(_mainMenuSceneName);
    }
}
