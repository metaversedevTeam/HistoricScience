using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CraftingSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    public ItemData Item { get; private set; }

    private Image _image;
    private Color _emptyColor;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _emptyColor = _image.color;
    }

    // 슬롯에 아이템을 배치하고 아이콘을 갱신한다.
    public void SetItem(ItemData item)
    {
        Item = item;
        if (item == null)
        {
            _image.sprite = null;
            _image.color = _emptyColor;
        }
        else if (item.IconSprite != null)
        {
            _image.sprite = item.IconSprite;
            _image.color = Color.white;
        }
        else
        {
            // 아이콘이 없어도 점유 상태임을 밝은 색으로 표시한다.
            _image.sprite = null;
            _image.color = new Color(0.55f, 0.55f, 0.55f, 1f);
        }
    }

    // 슬롯을 비운다.
    public void Clear() => SetItem(null);

    public void OnPointerClick(PointerEventData eventData) => Clear();

    public void OnDrop(PointerEventData eventData)
    {
        var source = eventData.pointerDrag?.GetComponent<ItemSlotUI>();
        if (source == null) return;
        SetItem(source.Item);
    }
}
