using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 조합 격자의 한 칸. 인벤토리에서 드롭받아 아이템을 배치하고, 배치된 아이템을 다른 칸으로 드래그해 이동·교환할 수 있다.
public class CraftingSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ItemData Item { get; private set; }

    // 이 슬롯의 격자 좌표(x=열, y=행). 비직사각형 배치도 지원하도록 인스펙터에서 명시적으로 지정한다.
    [SerializeField] private Vector2Int _coord;
    public Vector2Int Coord => _coord;

    private Image _image;
    private Color _emptyColor;

    private Canvas _rootCanvas;
    private RectTransform _dragIconRect;

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
        var dragged = eventData.pointerDrag;
        if (dragged == null) return;

        // 인벤토리 슬롯에서 온 드롭: 아이템을 이 칸에 복사한다.
        var fromInventory = dragged.GetComponent<ItemSlotUI>();
        if (fromInventory != null)
        {
            SetItem(fromInventory.Item);
            return;
        }

        // 다른 조합 칸에서 온 드롭: 이동(대상이 비어 있음) 또는 교환(대상에 아이템이 있음).
        var fromCraft = dragged.GetComponent<CraftingSlotUI>();
        if (fromCraft != null && fromCraft != this && fromCraft.Item != null)
        {
            var previous = Item;
            SetItem(fromCraft.Item);
            fromCraft.SetItem(previous);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 빈 칸은 옮길 것이 없으므로 드래그하지 않는다.
        if (Item == null) return;

        _rootCanvas = GetComponentInParent<Canvas>().rootCanvas;

        var go = new GameObject("DragImage");
        go.transform.SetParent(_rootCanvas.transform, false);
        go.transform.SetAsLastSibling();

        _dragIconRect = go.AddComponent<RectTransform>();
        _dragIconRect.sizeDelta = new Vector2(60f, 60f);

        var img = go.AddComponent<Image>();
        img.sprite = Item.IconSprite;
        img.color = Item.IconSprite != null ? Color.white : new Color(0.8f, 0.8f, 0.8f, 0.9f);
        img.raycastTarget = false;

        MoveDragIcon(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragIconRect == null) return;
        MoveDragIcon(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragIconRect == null) return;
        Destroy(_dragIconRect.gameObject);
        _dragIconRect = null;
    }

    // 드래그 아이콘을 마우스 위치로 이동한다.
    private void MoveDragIcon(PointerEventData eventData)
    {
        var camera = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _rootCanvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform,
            eventData.position,
            camera,
            out var localPoint);

        _dragIconRect.anchoredPosition = localPoint;
    }
}
