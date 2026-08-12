using System;
using UnityEngine;
using UnityEngine.UI;

// 메인 메뉴 화면(Figma의 main-menu-space). History·Science·Game 세 줄이 각각 PLAY·OPTION·EXIT 항목이 되며,
// OPTION은 아직 기능이 없어 잠금 상태(비활성)로만 보여 준다.
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _optionButton;
    [SerializeField] private Button _exitButton;

    // PLAY를 눌러 맵 관리 화면으로 넘어가야 할 때 발생한다.
    public event Action PlayRequested;

    private void Awake()
    {
        _playButton.onClick.AddListener(HandlePlayClick);
        _exitButton.onClick.AddListener(HandleExitClick);

        // OPTION은 구현 전이므로 어떤 입력도 받지 않는다. (버튼의 Disabled 색으로 잠금 상태가 보인다)
        _optionButton.interactable = false;
    }

    // PLAY 요청을 바깥(씬 매니저)에 알린다.
    private void HandlePlayClick()
    {
        PlayRequested?.Invoke();
    }

    // 게임(에디터에서는 플레이 모드)을 종료한다.
    private void HandleExitClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
