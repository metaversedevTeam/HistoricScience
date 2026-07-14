using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 작업대 조합 UI — 인벤토리를 페이로드로 받아 열리는 관리형 UI (슬롯 목록 표시, 3x3 격자 조합)
public class WorkbenchUI : OpenableUIBase<ResourceInventory>
{
    [SerializeField] private GameObject _slotPrefab;
    [SerializeField] private RectTransform _slotsContent;
    [SerializeField] private RectTransform _craftingGrid;
    [SerializeField] private Button _craftButton;
    [SerializeField] private TextMeshProUGUI _warningText;

    private ResourceInventory _inventory;
    private CraftingSlotUI[] _craftingSlots;
    private Coroutine _warningCoroutine;

    private void Awake()
    {
        InitializeCraftingSlots();
        _craftButton.onClick.AddListener(OnCraftButtonClick);
        _warningText.gameObject.SetActive(false);
    }

    // 조합 격자의 각 자식에 CraftingSlotUI를 등록한다.
    private void InitializeCraftingSlots()
    {
        _craftingSlots = new CraftingSlotUI[9];
        for (int i = 0; i < _craftingGrid.childCount && i < 9; i++)
        {
            var child = _craftingGrid.GetChild(i);
            _craftingSlots[i] = child.GetComponent<CraftingSlotUI>()
                               ?? child.gameObject.AddComponent<CraftingSlotUI>();
        }
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
        var result = FindMatchingRecipe();
        if (result == null)
        {
            ShowWarning("조합법이 없습니다.");
            return;
        }

        var needed = CountIngredients(result);
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

    // 조합 격자 배치와 일치하는 조합법의 결과 아이템을 반환한다. 없으면 null.
    private ItemData FindMatchingRecipe()
    {
        foreach (var item in _inventory.ItemDataList.Items)
        {
            if (MatchesRecipe(item))
                return item;
        }
        return null;
    }

    // 아이템의 Ingredient 종류·수량이 격자에 놓인 아이템과 일치하는지 확인한다.
    private bool MatchesRecipe(ItemData item)
    {
        var ingredients = item.Ingredient;
        if (ingredients == null || ingredients.Count == 0) return false;

        var required = CountIngredients(item);
        var placed = CountPlacedItems();

        if (required.Count != placed.Count) return false;
        foreach (var (resource, count) in required)
        {
            if (!placed.TryGetValue(resource, out var placedCount) || placedCount != count)
                return false;
        }
        return true;
    }

    // 현재 격자에 놓인 아이템별 수량을 집계한다.
    private Dictionary<ResourceData, int> CountPlacedItems()
    {
        var counts = new Dictionary<ResourceData, int>();
        foreach (var slot in _craftingSlots)
        {
            if (slot.Item == null) continue;
            counts.TryGetValue(slot.Item, out var current);
            counts[slot.Item] = current + 1;
        }
        return counts;
    }

    // 조합에 필요한 재료별 소비 수량을 집계한다.
    private Dictionary<ResourceData, int> CountIngredients(ItemData result)
    {
        var counts = new Dictionary<ResourceData, int>();
        foreach (var ingredient in result.Ingredient)
        {
            if (ingredient == null) continue;
            counts.TryGetValue(ingredient, out var current);
            counts[ingredient] = current + 1;
        }
        return counts;
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
