using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 커맨드 버튼 프리팹의 뷰 컴포넌트. 텍스트 표시와 클릭 콜백 연결을 담당한다.
public class CommandButtonView : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _label;

    // CommandData 하나를 버튼에 표시하고 클릭 시 실행될 콜백을 연결한다.
    public void Bind(CommandData cmd)
    {
        _label.text = cmd.Name;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => cmd.OnExecute?.Invoke());
    }
}
