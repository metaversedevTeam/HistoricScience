using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 조합법 힌트 격자의 한 칸. 아직 공개되지 않았으면 물음표만 보이고, 공개되면 그 칸에 놓이는 재료를 강조해 보여준다.
public class CraftingHintCellUI : MonoBehaviour
{
    [SerializeField] private Image _fill;
    [SerializeField] private Image _outline;
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _questionText;

    [Header("가려진 칸")]
    [SerializeField] private Color _hiddenFill = new Color32(0x08, 0x06, 0x0F, 0xFF);
    [SerializeField] private Color _hiddenOutline = new Color32(0x23, 0x2B, 0x3E, 0xFF);

    [Header("공개된 칸")]
    [SerializeField] private Color _revealedFill = new Color32(0x2A, 0x1F, 0x18, 0xFF);
    [SerializeField] private Color _revealedOutline = new Color32(0xF9, 0x73, 0x16, 0xFF);

    [Header("빈 칸")]
    [SerializeField] private Color _emptyFill = new Color32(0x0D, 0x0B, 0x14, 0xFF);
    [SerializeField] private Color _emptyOutline = new Color32(0x1A, 0x16, 0x28, 0xFF);

    // 공개된 칸으로 그린다. 재료 아이콘이 없으면 점유 상태만 알 수 있게 회색으로 표시한다.
    public void ShowRevealed(ResourceData item)
    {
        Apply(_revealedFill, _revealedOutline);

        _questionText.gameObject.SetActive(false);
        _icon.gameObject.SetActive(true);
        _icon.sprite = item != null ? item.IconSprite : null;
        _icon.color = item != null && item.IconSprite != null
            ? Color.white
            : new Color(0.55f, 0.55f, 0.55f, 1f);
    }

    // 아직 공개되지 않은 칸으로 그린다. 재료가 놓이는 칸인지조차 알 수 없도록 물음표만 보여준다.
    public void ShowHidden()
    {
        Apply(_hiddenFill, _hiddenOutline);

        _icon.gameObject.SetActive(false);
        _questionText.gameObject.SetActive(true);
    }

    // 재료가 놓이지 않는 빈 칸으로 그린다. (숨길 재료가 더 이상 남지 않았을 때만 쓴다)
    public void ShowEmpty()
    {
        Apply(_emptyFill, _emptyOutline);

        _icon.gameObject.SetActive(false);
        _questionText.gameObject.SetActive(false);
    }

    // 배경과 테두리 색을 한 번에 반영한다.
    private void Apply(Color fill, Color outline)
    {
        _fill.color = fill;
        _outline.color = outline;
    }
}
