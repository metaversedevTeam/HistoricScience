using UnityEngine;
using UnityEngine.UI;

// 건물 선택 UI에서 건물 하나를 표시하는 아이콘 버튼
public class BuildingIconButtonUI : MonoBehaviour
{
    [SerializeField] private Image _icon;

    public IBuildable Buildable { get; private set; }
    public Button Button { get; private set; }

    private void Awake()
    {
        Button = GetComponent<Button>();
    }

    // 건물 데이터를 아이콘 버튼에 반영한다.
    public void Setup(IBuildable buildable)
    {
        Buildable = buildable;
        _icon.sprite = buildable.Icon;
        _icon.color = buildable.Icon != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);
    }
}
