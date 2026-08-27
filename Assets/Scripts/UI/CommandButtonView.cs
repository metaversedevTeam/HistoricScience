using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 커맨드 버튼 프리팹의 뷰 컴포넌트. 아이콘/텍스트 표시와 클릭 콜백 연결을 담당한다.
public class CommandButtonView : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private Image _icon;

    // CommandData 하나를 버튼에 표시하고 클릭 시 실행될 콜백을 연결한다.
    public void Bind(CommandData cmd)
    {
        ApplyContent(cmd);

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => HandleClick(cmd));
    }

    // 명령 버튼을 눌렀을 때 효과음을 내고 연결된 명령을 실행한다.
    private void HandleClick(CommandData cmd)
    {
        AudioManager.PlayButtonClick();
        cmd.OnExecute?.Invoke();
    }

    // 아이콘이 있으면 아이콘만, 없으면 이름 텍스트만 표시한다.
    private void ApplyContent(CommandData cmd)
    {
        bool hasIcon = cmd.Icon != null;

        if (_icon != null)
        {
            _icon.sprite = cmd.Icon;
            _icon.gameObject.SetActive(hasIcon);
        }

        _label.text = cmd.Name;
        _label.gameObject.SetActive(!hasIcon);
    }
}
