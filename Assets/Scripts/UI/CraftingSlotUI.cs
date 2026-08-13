using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 조합 격자의 한 칸. 인벤토리에서 드롭받아 아이템을 배치하고, 배치된 아이템을 다른 칸으로 드래그해 이동·교환할 수 있다.
public class CraftingSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // 배치된 아이템이 바뀔 때마다 발행된다. 결과 미리보기처럼 격자 전체를 다시 읽어야 하는 쪽이 구독한다.
    public event Action OnItemChanged;

    public ItemData Item { get; private set; }

    // 이 슬롯의 격자 좌표(x=열, y=행). 비직사각형 배치도 지원하도록 인스펙터에서 명시적으로 지정한다.
    [SerializeField] private Vector2Int _coord;
    public Vector2Int Coord => _coord;

    // 칸의 테두리·배경과 별개로 아이템 그림만 그리는 자식 이미지
    [SerializeField] private Image _icon;

    // 창고에서 새 재료를 받아도 되는지 판정하는 규칙. 남은 보유량을 아는 작업대가 넣어 준다.
    private Func<ItemData, bool> _canPlace;

    private Canvas _rootCanvas;
    private RectTransform _dragIconRect;

    // 창고에서 재료를 받을 수 있는지 판정하는 규칙을 등록한다.
    public void SetPlacementRule(Func<ItemData, bool> canPlace) => _canPlace = canPlace;

    // 슬롯에 아이템을 배치하고 아이콘을 갱신한다.
    public void SetItem(ItemData item)
    {
        Item = item;

        if (item == null)
        {
            _icon.gameObject.SetActive(false);
            _icon.sprite = null;
        }
        else
        {
            _icon.gameObject.SetActive(true);
            _icon.sprite = item.IconSprite;
            // 아이콘이 없어도 점유 상태임을 밝은 색으로 표시한다.
            _icon.color = item.IconSprite != null ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
        }

        OnItemChanged?.Invoke();
    }

    // 슬롯을 비운다.
    public void Clear() => SetItem(null);

    public void OnPointerClick(PointerEventData eventData) => Clear();

    public void OnDrop(PointerEventData eventData)
    {
        var dragged = eventData.pointerDrag;
        if (dragged == null) return;

        // 창고 슬롯에서 온 드롭: 아이템을 이 칸에 복사한다.
        var fromInventory = dragged.GetComponent<ItemSlotUI>();
        if (fromInventory != null)
        {
            // 빈 칸을 끌어온 경우에는 놓여 있던 재료를 지우지 않고 무시한다.
            if (fromInventory.Item == null) return;

            // 같은 재료를 다시 놓는 것은 총량이 그대로라 항상 허용한다. 다른 재료라면 한 개를 새로 쓰는 셈이다.
            if (fromInventory.Item != Item && _canPlace != null && !_canPlace(fromInventory.Item)) return;

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
