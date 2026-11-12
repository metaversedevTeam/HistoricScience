using System;
using UnityEngine;
using UnityEngine.UI;

// 메인 메뉴 화면. 사각형 모션그래픽 배경 위에 시작·설정·나가기 버튼을 두며,
// 설정은 아직 기능이 없어 잠금 상태(비활성)로만 보여 준다. 등장·강조 연출은 MainMenu 폴더의 모션 컴포넌트들이 맡는다.
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _optionButton;
    [SerializeField] private Button _exitButton;

    // 게임 제목과 버튼들을 묶은 메뉴 본문. 맵 선택 화면이 떠 있는 동안에는 꺼 두고 배경 모션그래픽만 남긴다.
    [SerializeField] private GameObject _menuContent;

    // 시작을 눌러 맵 선택 화면으로 넘어가야 할 때 발생한다.
    public event Action PlayRequested;

    private void Awake()
    {
        _playButton.onClick.AddListener(HandlePlayClick);
        _exitButton.onClick.AddListener(HandleExitClick);

        // OPTION은 구현 전이므로 어떤 입력도 받지 않는다. (버튼의 Disabled 색으로 잠금 상태가 보인다)
        _optionButton.interactable = false;
    }

    // 게임 제목과 버튼들(메뉴 본문)을 보이거나 숨긴다. 배경 모션그래픽은 그대로 둔다.
    public void SetMenuVisible(bool visible)
    {
        _menuContent.SetActive(visible);
    }

    // PLAY 요청을 바깥(씬 매니저)에 알린다.
    private void HandlePlayClick()
    {
        AudioManager.PlayConfirm();
        PlayRequested?.Invoke();
    }

    // 게임(에디터에서는 플레이 모드)을 종료한다.
    private void HandleExitClick()
    {
        AudioManager.PlayCancel();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
