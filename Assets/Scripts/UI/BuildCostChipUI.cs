using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 건물 카드와 상세 패널에서 필요 자원 하나를 아이콘과 수량으로 나란히 보여주는 작은 칩.
// 보유량이 필요량에 못 미치면 수량 글자를 강조 색으로 바꿔 부족함을 알린다.
public class BuildCostChipUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _countText;
    [SerializeField] private Color _sufficientColor = new Color32(0xC9, 0xD2, 0xE3, 0xFF);
    [SerializeField] private Color _insufficientColor = new Color32(0xFF, 0x5A, 0x4D, 0xFF);

    // 필요 수량만 표시한다. 폭이 좁아 보유량을 함께 적을 수 없는 건물 카드에서 쓴다.
    public void SetupRequiredOnly(ResourceData resource, int needed, int owned)
    {
        Apply(resource, needed.ToString(), owned >= needed);
    }

    // 보유 수량과 필요 수량을 함께 표시한다. 상세 패널의 건설 비용에서 쓴다.
    public void SetupWithOwned(ResourceData resource, int needed, int owned)
    {
        Apply(resource, $"{owned}/{needed}", owned >= needed);
    }

    // 자원 아이콘과 수량 문구를 칩에 반영하고, 충족 여부에 따라 수량 글자색을 정한다.
    private void Apply(ResourceData resource, string countText, bool sufficient)
    {
        _icon.sprite = resource != null ? resource.IconSprite : null;
        _icon.color = _icon.sprite != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);

        _countText.text = countText;
        _countText.color = sufficient ? _sufficientColor : _insufficientColor;
    }
}
