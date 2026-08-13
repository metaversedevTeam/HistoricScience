using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 작업대 창고 격자의 한 칸. 보유 아이템을 아이콘·수량으로 보여주고, 조합 격자로 드래그해 재료를 옮긴다.
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

    // 격자가 다시 그려지며 드래그 도중 슬롯이 꺼지면 OnEndDrag가 오지 않으므로 드래그 이미지를 여기서 정리한다.
    private void OnDisable()
    {
        if (_dragImageRect == null) return;

        Destroy(_dragImageRect.gameObject);
        _dragImageRect = null;
    }

    // 아이템 데이터와 보유 개수를 슬롯에 반영한다.
    public void Setup(ItemData item, int count)
    {
        Item = item;
        _icon.gameObject.SetActive(true);
        _icon.sprite = item.IconSprite;
        _icon.color = item.IconSprite != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);

        _countText.gameObject.SetActive(true);
        _countText.text = count.ToString();
    }

    // 아이템 없이 빈 칸으로 표시한다. 창고 격자의 남은 공간을 그대로 보여주기 위해 쓴다.
    public void SetupEmpty()
    {
        Item = null;
        _icon.gameObject.SetActive(false);
        _icon.sprite = null;
        _countText.gameObject.SetActive(false);
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
