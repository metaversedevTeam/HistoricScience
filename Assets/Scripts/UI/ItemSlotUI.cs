using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _countText;

    public ItemData Item { get; private set; }

    private Canvas _rootCanvas;
    private ScrollRect _parentScrollRect;
    private RectTransform _dragImageRect;

    private void Awake()
    {
        _parentScrollRect = GetComponentInParent<ScrollRect>();
    }

    // 아이템 데이터와 보유 개수를 슬롯에 반영한다.
    public void Setup(ItemData item, int count)
    {
        Item = item;
        _icon.sprite = item.IconSprite;
        _icon.color = item.IconSprite != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);
        _countText.text = count.ToString();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Item == null)
        {
            // 아이템이 없으면 드래그를 ScrollRect에 위임한다.
            _parentScrollRect?.OnBeginDrag(eventData);
            return;
        }

        _rootCanvas = GetComponentInParent<Canvas>().rootCanvas;

        var go = new GameObject("DragImage");
        go.transform.SetParent(_rootCanvas.transform, false);
        go.transform.SetAsLastSibling();

        _dragImageRect = go.AddComponent<RectTransform>();
        _dragImageRect.sizeDelta = new Vector2(60f, 60f);

        var img = go.AddComponent<Image>();
        img.sprite = Item.IconSprite;
        img.color = Item.IconSprite != null ? Color.white : new Color(0.8f, 0.8f, 0.8f, 0.9f);
        img.raycastTarget = false;

        MoveDragImage(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragImageRect == null)
        {
            _parentScrollRect?.OnDrag(eventData);
            return;
        }
        MoveDragImage(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragImageRect == null)
        {
            _parentScrollRect?.OnEndDrag(eventData);
            return;
        }
        Destroy(_dragImageRect.gameObject);
        _dragImageRect = null;
    }

    // 드래그 이미지를 마우스 위치로 이동한다.
    private void MoveDragImage(PointerEventData eventData)
    {
        var camera = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _rootCanvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform,
            eventData.position,
            camera,
            out var localPoint);

        _dragImageRect.anchoredPosition = localPoint;
    }
}
