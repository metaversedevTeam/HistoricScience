using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 작업대 조합 UI — 인벤토리와 작업대 위치를 페이로드로 받아 열리는 관리형 UI (창고 격자 표시, 격자에 배치된 위치까지 감안한 조합)
public class WorkbenchUI : OpenableUIBase<WorkbenchData>
{
    [Header("창고 격자")]
    [SerializeField] private ItemSlotUI _slotPrefab;
    [SerializeField] private RectTransform _slotsContent;
    // 보유 종류가 적어도 빈 칸을 그려 창고 격자를 채우는 최소 칸 수 (디자인 기준 6열 x 5행)
    [SerializeField, Min(0)] private int _minSlotCount = 30;

    [Header("제작대")]
    [SerializeField] private RectTransform _craftingGrid;
    [SerializeField] private Image _resultIcon;
    [SerializeField] private Button _craftButton;
    [SerializeField] private TextMeshProUGUI _warningText;

    [Header("헤더")]
    [SerializeField] private Button _closeButton;
    // 시민 할당 배지. 작업대에 일꾼 정보를 넘기는 경로가 아직 없어 항상 숨긴다.
    [SerializeField] private GameObject _workerBadge;
    // 제작 진행 게이지. 제작이 즉시 끝나므로 아직 숨긴다.
    [SerializeField] private GameObject _progressRow;

    private ResourceInventory _inventory;

    // 조합 결과를 획득한 위치로 넘길 작업대의 월드 좌표
    private Vector3 _workbenchWorldPosition;

    private CraftingSlotUI[] _craftingSlots;
    private readonly List<ItemSlotUI> _slots = new();
    private Coroutine _warningCoroutine;

    private void Awake()
    {
        InitializeCraftingSlots();
        _craftButton.onClick.AddListener(OnCraftButtonClick);
        _closeButton.onClick.AddListener(OnCloseButtonClick);
        _warningText.gameObject.SetActive(false);

        // 대응하는 데이터 소스가 생기기 전까지는 디자인 요소만 남기고 꺼 둔다.
        if (_workerBadge != null) _workerBadge.SetActive(false);
        if (_progressRow != null) _progressRow.SetActive(false);

        RefreshResultPreview();
    }

    // 조합 격자 하위의 모든 CraftingSlotUI를 수집해 배치 규칙과 변경 알림을 연결한다(격자 크기·모양 무관).
    private void InitializeCraftingSlots()
    {
        _craftingSlots = _craftingGrid.GetComponentsInChildren<CraftingSlotUI>(true);

        foreach (var slot in _craftingSlots)
        {
            // 프리팹에는 아이콘 이미지가 켜진 채 저장돼 있어 그대로 두면 빈 칸이 흰 사각형으로 보인다.
            slot.Clear();
            slot.SetPlacementRule(CanPlaceInCraftingGrid);
            slot.OnItemChanged += HandleCraftingGridChanged;
        }
    }

    // 격자 배치가 바뀌면 결과 미리보기와 창고에 남아 보이는 수량을 함께 갱신한다.
    private void HandleCraftingGridChanged()
    {
        RefreshResultPreview();

        // 열리기 전이나 풀로 돌아가는 중에는 채울 인벤토리가 없다.
        if (_inventory != null) PopulateSlots();
    }

    // 격자에 재료를 한 개 더 올릴 여유가 있는지 판정한다.
    private bool CanPlaceInCraftingGrid(ItemData item)
    {
        if (item == null || _inventory == null) return false;
        return GetRemainingCount(item) > 0;
    }

    // 창고에 남아 보이는 수량 — 보유량에서 조합 격자에 이미 올려둔 몫을 뺀 값.
    private int GetRemainingCount(ItemData item)
    {
        int placed = 0;
        foreach (var slot in _craftingSlots)
        {
            if (slot.Item == item) placed++;
        }

        return _inventory.Get(item) - placed;
    }

    // 주입받은 인벤토리를 구독하고 창고 격자를 구성한다. 작업대 위치는 조합 결과의 획득 위치로 쓴다.
    protected override void ApplyData(WorkbenchData data)
    {
        _inventory = data.Inventory;
        _workbenchWorldPosition = data.WorldPosition;
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

    // 인벤토리 변경 시 창고 격자를 갱신한다.
    private void HandleInventoryChanged(ItemData item, int newCount) => PopulateSlots();

    // 닫기 버튼 클릭 시 UI를 닫는다.
    private void OnCloseButtonClick() => Close();

    // 창고에 남아 있는 아이템으로 격자를 채우고, 남는 칸은 빈 칸으로 그린다.
    // 조합 격자에 올려둔 재료는 아직 소비되지 않았지만 창고에서는 빠진 것처럼 보여 준다.
    private void PopulateSlots()
    {
        int filledCount = 0;
        foreach (var item in _inventory.ItemDataList.Items)
        {
            var count = GetRemainingCount(item);
            if (count <= 0) continue;

            GetOrCreateSlot(filledCount).Setup(item, count);
            filledCount++;
        }

        int slotCount = Mathf.Max(_minSlotCount, filledCount);
        for (int i = filledCount; i < slotCount; i++)
            GetOrCreateSlot(i).SetupEmpty();

        // 보유 종류가 줄어 남는 슬롯은 격자에서 감춘다.
        for (int i = slotCount; i < _slots.Count; i++)
            _slots[i].gameObject.SetActive(false);
    }

    // index번째 슬롯을 켜서 반환하고, 아직 없으면 새로 만들어 재사용 목록에 넣는다.
    private ItemSlotUI GetOrCreateSlot(int index)
    {
        while (_slots.Count <= index)
            _slots.Add(Instantiate(_slotPrefab, _slotsContent));

        _slots[index].gameObject.SetActive(true);
        return _slots[index];
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

        _inventory.Add(result, 1, _workbenchWorldPosition);

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
        // 열기 전에도 격자 변경 이벤트가 올 수 있어 인벤토리가 없으면 판정을 건너뛴다.
        if (_inventory == null) return null;

        foreach (var item in _inventory.ItemDataList.Items)
        {
            if (item.HasRecipe && placed.Matches(item.ToPattern()))
                return item;
        }
        return null;
    }

    // 현재 배치로 만들어질 아이템을 결과 슬롯에 미리 보여준다. 맞는 조합법이 없으면 빈 슬롯으로 둔다.
    private void RefreshResultPreview()
    {
        ItemData result = FindMatchingRecipe(BuildPlacedPattern());
        bool hasResult = result != null;

        _resultIcon.gameObject.SetActive(hasResult);
        if (!hasResult) return;

        _resultIcon.sprite = result.IconSprite;
        _resultIcon.color = result.IconSprite != null ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
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

// 작업대 UI에 전달되는 페이로드 — 재료·결과를 주고받을 인벤토리와, 조합 결과의 획득 위치로 쓸 작업대의 월드 좌표
public readonly struct WorkbenchData
{
    public readonly ResourceInventory Inventory;
    public readonly Vector3 WorldPosition;

    // 인벤토리와 작업대 월드 좌표로 페이로드를 구성한다.
    public WorkbenchData(ResourceInventory inventory, Vector3 worldPosition)
    {
        Inventory = inventory;
        WorldPosition = worldPosition;
    }
}
