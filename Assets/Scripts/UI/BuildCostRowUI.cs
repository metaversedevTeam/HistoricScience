using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 건설 비용 UI에서 자원 하나의 아이콘과 보유/필요 수량을 표시하는 행
public class BuildCostRowUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _countText;
    [SerializeField] private Color _sufficientColor = Color.white;
    [SerializeField] private Color _insufficientColor = new Color(1f, 0.3f, 0.3f);

    // 자원 아이콘과 보유/필요 수량을 행에 반영하고, 부족하면 강조 색으로 표시한다.
    public void Setup(ResourceData resource, int needed, int owned)
    {
        _icon.sprite = resource.IconSprite;
        _icon.color = resource.IconSprite != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);

        _countText.text = $"{owned} / {needed}";
        _countText.color = owned >= needed ? _sufficientColor : _insufficientColor;
    }
}
