using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _countText;

    // 아이템 데이터와 보유 개수를 슬롯에 반영한다.
    public void Setup(ItemData item, int count)
    {
        _icon.sprite = item.IconSprite;
        _icon.color = item.IconSprite != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);
        _countText.text = count.ToString();
    }
}
