using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 작업대 조합 UI — 인벤토리를 페이로드로 받아 열리는 관리형 UI (슬롯 목록 표시, 격자에 배치된 위치까지 감안한 조합)
public class WorkbenchUI : OpenableUIBase<ResourceInventory>
{
    [SerializeField] private GameObject _slotPrefab;
    [SerializeField] private RectTransform _slotsContent;
    [SerializeField] private RectTransform _craftingGrid;
    [SerializeField] private Button _craftButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TextMeshProUGUI _warningText;

    private ResourceInventory _inventory;
    private CraftingSlotUI[] _craftingSlots;
    private Coroutine _warningCoroutine;

    private void Awake()
    {
        InitializeCraftingSlots();
        _craftButton.onClick.AddListener(OnCraftButtonClick);
        _closeButton.onClick.AddListener(OnCloseButtonClick);
        _warningText.gameObject.SetActive(false);
    }

    // 조합 격자 하위의 모든 CraftingSlotUI를 수집한다(격자 크기·모양 무관).
    private void InitializeCraftingSlots()
    {
        _craftingSlots = _craftingGrid.GetComponentsInChildren<CraftingSlotUI>(true);
    }

    // 주입받은 인벤토리를 구독하고 슬롯 목록을 구성한다.
    protected override void ApplyData(ResourceInventory data)
    {
        _inventory = data;
        _inventory.OnAddItem += HandleInventoryChanged;
        _inventory.OnRemoveItem += HandleInventoryChanged;
        PopulateSlots();
    }

    // 인벤토리 구독을 해제하고 격자·경고 표시를 재사용 가능한 상태로 정리한다.
    protected override void OnReturnToPool()
    {
        if (_inventory != null)
        {
            _inventory.OnAddItem -= HandleInventoryChanged;
            _inventory.OnRemoveItem -= HandleInventoryChanged;
            _inventory = null;
        }

        foreach (var slot in _craftingSlots)
            slot.Clear();

        _warningText.gameObject.SetActive(false);
    }

    // 인벤토리 변경 시 슬롯 목록을 갱신한다.
    private void HandleInventoryChanged(ItemData item, int newCount) => PopulateSlots();

    // 닫기 버튼 클릭 시 UI를 닫는다.
    private void OnCloseButtonClick() => Close();

    // 기존 슬롯을 제거하고 인벤토리의 아이템마다 슬롯을 새로 생성한다.
    private void PopulateSlots()
    {
        ClearSlots();
        foreach (var item in _inventory.ItemDataList.Items)
        {
            var count = _inventory.Get(item);
            if (count == 0) continue;
            var slot = Instantiate(_slotPrefab, _slotsContent).GetComponent<ItemSlotUI>();
            slot.Setup(item, count);
        }
    }

    // Content 아래의 슬롯 오브젝트를 모두 제거한다.
    private void ClearSlots()
    {
        for (int i = _slotsContent.childCount - 1; i >= 0; i--)
            Destroy(_slotsContent.GetChild(i).gameObject);
    }

    // 격자 배치와 일치하는 조합법을 찾아 재료를 소비하고 결과 아이템을 지급한다.
    private void OnCraftButtonClick()
    {
        var placed = BuildPlacedPattern();
        var result = FindMatchingRecipe(placed);
        if (result == null)
        {
            ShowWarning("조합법이 없습니다.");
            return;
        }

        var needed = placed.CountItems();
        foreach (var (resource, count) in needed)
        {
            if (!_inventory.Has(resource, count))
            {
                ShowWarning("재료가 부족합니다.");
                return;
            }
        }

        foreach (var (resource, count) in needed)
            _inventory.Remove(resource, count);

        _inventory.Add(result, 1);

        foreach (var slot in _craftingSlots)
            slot.Clear();
    }

    // 격자의 점유 슬롯에서 현재 배치 패턴을 만든다.
    private CraftingPattern BuildPlacedPattern()
    {
        var cells = new List<(Vector2Int coord, ResourceData item)>();
        foreach (var slot in _craftingSlots)
        {
            if (slot.Item != null)
                cells.Add((slot.Coord, slot.Item));
        }
        return CraftingPattern.FromCells(cells);
    }

    // 현재 배치와 (위치까지) 일치하는 조합법의 결과 아이템을 반환한다. 없으면 null.
    private ItemData FindMatchingRecipe(CraftingPattern placed)
    {
        if (placed.IsEmpty) return null;
        foreach (var item in _inventory.ItemDataList.Items)
        {
            if (item.HasRecipe && placed.Matches(item.ToPattern()))
                return item;
        }
        return null;
    }

    // 경고 메시지를 표시하고 2초 후 자동으로 숨긴다.
    private void ShowWarning(string message)
    {
        _warningText.text = message;
        _warningText.gameObject.SetActive(true);

        if (_warningCoroutine != null) StopCoroutine(_warningCoroutine);
        _warningCoroutine = StartCoroutine(HideWarningAfterDelay(2f));
    }

    // 지정한 시간 뒤에 경고 텍스트를 숨기는 코루틴.
    private IEnumerator HideWarningAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _warningText.gameObject.SetActive(false);
    }
}
