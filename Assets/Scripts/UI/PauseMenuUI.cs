using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 일시정지 화면. 메인 메뉴와 같은 사각형 모션그래픽 디자인으로 이어하기·저장 후 나가기·설정·메뉴로 나가기 버튼을 보여 준다.
// UIManager로 여닫는 관리형 UI이며, 열려 있는 동안 Time.timeScale을 0으로 고정해 게임을 멈춘다.
// 열고 닫을 때의 등장·퇴장 연출은 MainMenuIntroSequence가 맡는다. (모든 모션은 실제 경과 시간으로 돌아 일시정지 중에도 움직인다)
public class PauseMenuUI : OpenableUIBase
{
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _saveAndExitButton;
    [SerializeField] private Button _optionButton;
    [SerializeField] private Button _quitToMenuButton;
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    [Header("오른쪽 일시정지 표식")]
    // 누르면 이어하기와 같이 동작하는 표식 버튼
    [SerializeField] private Button _emblemButton;

    // 눌렀을 때 일시정지 기호에서 재생 기호로 바뀔 아이콘
    [SerializeField] private PausePlayIconGraphic _emblemIcon;

    // 눌렀을 때 왼쪽부터 하늘색으로 차오를 막대(피벗이 왼쪽이어야 한다)
    [SerializeField] private RectTransform _emblemFill;

    // 재생 기호로 다 바뀌는 데 걸리는 시간(초). 다 바뀐 뒤 창이 닫히기 시작한다.
    [SerializeField, Min(0.01f)] private float _emblemMorphDuration = 0.28f;

    [Header("연출")]
    // 열릴 때 요소들을 차례로 들여오고, 닫힐 때 거꾸로 내보내는 연출
    [SerializeField] private MainMenuIntroSequence _introSequence;

    // 메인 메뉴로 나갈 때 띄울 전환 연출(로딩 연출의 역재생). 비워 두면 연출 없이 바로 넘어간다.
    [SerializeField] private IngameExitSequence _exitSequencePrefab;

    private IngameSceneManager _sceneManager;

    // 메인 메뉴로 나가는 전환 연출이 시작되었는지 여부. 그 뒤로는 ESC로 닫히지 않는다.
    private bool _isExiting;

    // 메인 메뉴로 나가는 중에는 ESC로 닫혀 게임이 다시 흐르지 않도록 막는다.
    public override bool ClosesOnEscape => !_isExiting;

    // 표식을 눌러 재생 기호로 바뀌는 중인지 여부
    private bool _isEmblemMorphing;

    // 재생 기호로 바뀌기 시작한 뒤 지난 시간(초)
    private float _emblemMorphElapsed;

    private void Awake()
    {
        _resumeButton.onClick.AddListener(HandleResumeClick);
        _emblemButton.onClick.AddListener(HandleEmblemClick);
        _saveAndExitButton.onClick.AddListener(HandleSaveAndExitClick);
        _quitToMenuButton.onClick.AddListener(HandleQuitToMenuClick);

        // OPTION은 아직 기능이 없어 잠금 상태로만 보여 준다. (MainMenuUI의 OPTION과 동일한 규약)
        _optionButton.interactable = false;
    }

    private void Update()
    {
        HandleEmblemMorph();
    }

    // 열리는 즉시 게임을 멈춘다. 등장 연출은 활성화될 때 MainMenuIntroSequence가 스스로 재생한다.
    protected override void PlayOpenTransition()
    {
        _sceneManager = FindFirstObjectByType<IngameSceneManager>();
        Time.timeScale = 0f;
        FinishOpen();
    }

    // 등장 연출을 거꾸로 재생해 요소들을 내보낸 뒤 닫기를 마무리한다.
    protected override void PlayCloseTransition()
    {
        if (_introSequence == null || !_introSequence.isActiveAndEnabled)
        {
            FinishClose();
            return;
        }

        _introSequence.PlayReverse(FinishClose);
    }

    // 연출을 거쳤든 즉시 닫혔든, 닫히고 나면 게임을 다시 진행시킨다.
    // 다음에 열릴 때를 위해 표식도 일시정지 기호로 되돌린다.
    protected override void OnReturnToPool()
    {
        Time.timeScale = 1f;

        _isExiting = false;
        _isEmblemMorphing = false;
        HandleApplyEmblem(0f);
    }

    // 이어하기는 창을 닫기만 하므로 닫기 효과음(ManagedUIBase)만 울린다.
    private void HandleResumeClick() => Close();

    // 표식을 누르면 재생 기호로 바꾸기 시작한다. 다 바뀌면 이어하기와 같이 창을 닫는다.
    private void HandleEmblemClick()
    {
        if (_isEmblemMorphing || State != UIState.Open)
            return;

        _isEmblemMorphing = true;
        _emblemMorphElapsed = 0f;
    }

    // 표식을 일시정지 기호에서 재생 기호로 감속하며 바꾸고, 다 바뀌면 창을 닫는다.
    private void HandleEmblemMorph()
    {
        if (!_isEmblemMorphing)
            return;

        _emblemMorphElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_emblemMorphElapsed / _emblemMorphDuration);
        HandleApplyEmblem(1f - Mathf.Pow(1f - t, 3f));

        if (t < 1f)
            return;

        _isEmblemMorphing = false;
        Close();
    }

    // 표식 연출 진행도(0~1)를 아이콘 모양과 하늘색 채우기 길이에 반영한다.
    private void HandleApplyEmblem(float progress)
    {
        _emblemIcon.Morph = progress;
        _emblemFill.localScale = new Vector3(progress, 1f, 1f);
    }

    // 현재 맵을 저장한 뒤 메인 메뉴로 나간다.
    private void HandleSaveAndExitClick()
    {
        if (_isExiting)
            return;

        AudioManager.PlayConfirm();

        if (_sceneManager != null)
            _sceneManager.SaveMap(_sceneManager.CurrentSlot);

        LoadMainMenuScene();
    }

    // 저장하지 않고 메인 메뉴로 나간다.
    private void HandleQuitToMenuClick()
    {
        if (_isExiting)
            return;

        AudioManager.PlayCancel();
        LoadMainMenuScene();
    }

    // 로딩 연출을 거꾸로 재생하며 메인 메뉴 씬으로 넘어간다. 시간 배속은 씬이 넘어가는 순간 전환 연출이 되돌린다.
    private void LoadMainMenuScene()
    {
        if (_isExiting)
            return;

        if (_exitSequencePrefab == null)
        {
            _isExiting = true;
            Time.timeScale = 1f;
            SceneManager.LoadSceneAsync(_mainMenuSceneName);
            return;
        }

        _isExiting = true;
        Instantiate(_exitSequencePrefab).Play(_mainMenuSceneName);
    }
}
