using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 창고 UI — 인벤토리를 페이로드로 받아 보유 아이템을 시대별 카테고리 필터·이름 검색으로 추려 격자에 보여주고,
// 선택한 아이템의 상세 정보를 표시하는 관리형 UI.
// 카테고리 탭은 도감의 알약형 탭 컴포넌트(CodexAgeTabUI)를 그대로 재사용한다.
public class WarehouseUI : OpenableUIBase<ResourceInventory>
{
    [Header("탭")]
    [SerializeField] private CodexAgeTabUI _tabPrefab;
    [SerializeField] private RectTransform _tabParent;

    [Header("격자")]
    [SerializeField] private WarehouseSlotUI _slotPrefab;
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private ScrollRect _scrollRect;
    // 격자가 비어 보이지 않도록 빈 칸을 채워 항상 유지하는 최소 슬롯 개수.
    [SerializeField, Min(1)] private int _minSlotCount = 30;

    [Header("검색")]
    [SerializeField] private TMP_InputField _searchInput;

    [Header("닫기")]
    [SerializeField] private Button _closeButton;

    [Header("상세")]
    [SerializeField] private Image _detailIcon;
    [SerializeField] private Image _detailPlaceholderIcon;
    [SerializeField] private RectTransform _detailInfo;
    [SerializeField] private TextMeshProUGUI _detailNameText;
    [SerializeField] private TextMeshProUGUI _detailDescriptionText;
    [SerializeField] private RectTransform _detailQuantity;
    [SerializeField] private TextMeshProUGUI _detailQuantityText;

    // 전체 탭을 나타내는 값. 특정 시대가 선택되면 그 시대가 들어간다.
    private Age? _selectedAge;
    private ItemData _selectedItem;

    private ResourceInventory _inventory;
    private readonly List<CodexAgeTabUI> _tabs = new();
    private readonly List<Age?> _tabAges = new();
    private readonly List<WarehouseSlotUI> _slots = new();

    private void Awake()
    {
        BuildTabs();
        _searchInput.onValueChanged.AddListener(HandleSearchChanged);
        _closeButton.onClick.AddListener(HandleCloseButtonClick);
    }

    // 주입받은 인벤토리를 구독하고 격자·상세를 채운다.
    protected override void ApplyData(ResourceInventory data)
    {
        _inventory = data;
        _inventory.OnAddItem += HandleInventoryChanged;
        _inventory.OnRemoveItem += HandleInventoryChanged;

        Refresh();
    }

    // 풀로 돌아가기 전에 구독·검색어·선택 상태·스크롤 위치를 초기 상태로 되돌린다.
    protected override void OnReturnToPool()
    {
        if (_inventory != null)
        {
            _inventory.OnAddItem -= HandleInventoryChanged;
            _inventory.OnRemoveItem -= HandleInventoryChanged;
            _inventory = null;
        }

        _searchInput.SetTextWithoutNotify(string.Empty);
        _selectedItem = null;
        SelectAge(null, refresh: false);

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }

    // 전체 탭과 시대별 탭을 순서대로 생성한다. 창고는 채집한 자연 자원도 걸러 볼 수 있어야 하므로 도감과 달리 자연 탭도 만든다.
    private void BuildTabs()
    {
        AddTab("전체", null);

        foreach (Age age in Enum.GetValues(typeof(Age)))
            AddTab(age.ToShortName(), age);

        SelectAge(null, refresh: false);
    }

    // 탭 하나를 생성해 목록에 등록한다.
    private void AddTab(string label, Age? age)
    {
        CodexAgeTabUI tab = Instantiate(_tabPrefab, _tabParent);
        tab.Setup(label, locked: false, () => SelectAge(age, refresh: true));

        _tabs.Add(tab);
        _tabAges.Add(age);
    }

    // 선택된 시대 필터를 바꾸고 탭 색상을 갱신한다.
    private void SelectAge(Age? age, bool refresh)
    {
        _selectedAge = age;

        for (int i = 0; i < _tabs.Count; i++)
            _tabs[i].SetSelected(_tabAges[i].Equals(age));

        if (refresh)
            Refresh();
    }

    // 닫기 버튼을 누르면 UI를 닫는다. (ESC로 닫는 UIManager 경로와 동일한 동작)
    private void HandleCloseButtonClick() => Close();

    // 검색어가 바뀌면 격자를 다시 추린다.
    private void HandleSearchChanged(string _) => Refresh();

    // 인벤토리 수량이 바뀌면 격자·상세를 다시 그린다.
    private void HandleInventoryChanged(ItemData item, int newCount) => Refresh();

    // 슬롯을 클릭하면 그 아이템을 상세 패널의 대상으로 삼는다.
    private void HandleSlotClick(WarehouseSlotUI slot)
    {
        _selectedItem = slot.Item;

        RefreshSelection();
        RefreshDetail();
    }

    // 격자·선택 강조·상세를 모두 다시 그린다.
    private void Refresh()
    {
        RefreshSlots();
        RefreshSelection();
        RefreshDetail();
    }

    // 필터를 통과한 아이템으로 슬롯을 채우고, 남는 슬롯은 빈 칸으로 되돌린다.
    private void RefreshSlots()
    {
        IReadOnlyList<ItemData> items = _inventory.ItemDataList.Items;
        string keyword = _searchInput.text.Trim();
        int filledCount = 0;

        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (!Matches(item, keyword)) continue;

            GetOrCreateSlot(filledCount).Setup(item, _inventory.Get(item));
            filledCount++;
        }

        // 보유 종류가 적어도 빈 칸을 그려 격자 모양을 유지한다.
        int slotCount = Mathf.Max(_minSlotCount, filledCount);
        for (int i = filledCount; i < slotCount; i++)
            GetOrCreateSlot(i).SetupEmpty();

        // 필터가 좁아져 슬롯이 남으면 격자에서 감춘다.
        for (int i = slotCount; i < _slots.Count; i++)
            _slots[i].gameObject.SetActive(false);
    }

    // 아이템이 보유 중이고 현재 시대 필터와 검색어를 모두 만족하는지 판정한다.
    private bool Matches(ItemData item, string keyword)
    {
        if (_inventory.Get(item) <= 0)
            return false;

        if (_selectedAge.HasValue && item.Age != _selectedAge.Value)
            return false;

        if (string.IsNullOrEmpty(keyword))
            return true;

        return item.Nmae != null &&
               item.Nmae.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // index번째 슬롯을 켜서 반환하고, 아직 없으면 새로 만들어 재사용 목록에 넣는다.
    private WarehouseSlotUI GetOrCreateSlot(int index)
    {
        while (_slots.Count <= index)
        {
            WarehouseSlotUI slot = Instantiate(_slotPrefab, _slotParent);
            slot.SetClickHandler(HandleSlotClick);
            _slots.Add(slot);
        }

        _slots[index].gameObject.SetActive(true);
        _slots[index].transform.SetSiblingIndex(index);
        return _slots[index];
    }

    // 선택한 아이템이 격자에서 사라졌으면 선택을 해제하고, 남아 있으면 그 슬롯만 강조한다.
    private void RefreshSelection()
    {
        bool stillVisible = false;

        for (int i = 0; i < _slots.Count; i++)
        {
            bool selected = _selectedItem != null && _slots[i].gameObject.activeSelf && _slots[i].Item == _selectedItem;
            _slots[i].SetSelected(selected);
            stillVisible |= selected;
        }

        if (!stillVisible)
            _selectedItem = null;
    }

    // 선택한 아이템의 아이콘·이름·설명·수량을 상세 패널에 반영한다. 선택이 없으면 자리표시자만 남긴다.
    private void RefreshDetail()
    {
        bool hasSelection = _selectedItem != null;
        bool hasIcon = hasSelection && _selectedItem.IconSprite != null;

        _detailIcon.gameObject.SetActive(hasIcon);
        _detailIcon.sprite = hasSelection ? _selectedItem.IconSprite : null;
        _detailPlaceholderIcon.gameObject.SetActive(!hasIcon);

        _detailInfo.gameObject.SetActive(hasSelection);
        _detailQuantity.gameObject.SetActive(hasSelection);

        if (!hasSelection) return;

        _detailNameText.text = _selectedItem.Nmae;
        _detailDescriptionText.text = _selectedItem.Description;
        _detailQuantityText.text = $"x{_inventory.Get(_selectedItem)}";
    }
}
