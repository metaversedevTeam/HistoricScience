using UnityEngine;

public class WorkbenchUI : MonoBehaviour
{
    [SerializeField] private GameObject _layer;
    [SerializeField] private GameObject _slotPrefab;
    [SerializeField] private RectTransform _slotsContent;

    private ResourceInventory _inventory;

    // 인벤토리를 받아 슬롯 UI를 생성한다.
    public void Open(ResourceInventory inventory)
    {
        _inventory = inventory;
        PopulateSlots();
        _layer.SetActive(true);
    }

    //UI를 닫는다.
    public void Close()
    {
        _layer.SetActive(false);
    }

    // 기존 슬롯을 제거하고 인벤토리의 아이템마다 슬롯을 새로 생성한다.
    private void PopulateSlots()
    {
        ClearSlots();

        foreach (var item in _inventory.ItemDataList.Items)
        {
            var slot = Instantiate(_slotPrefab, _slotsContent).GetComponent<ItemSlotUI>();
            slot.Setup(item, _inventory.Get(item));
        }
    }

    // Content 아래의 슬롯 오브젝트를 모두 제거한다.
    private void ClearSlots()
    {
        for (int i = _slotsContent.childCount - 1; i >= 0; i--)
            Destroy(_slotsContent.GetChild(i).gameObject);
    }
}
