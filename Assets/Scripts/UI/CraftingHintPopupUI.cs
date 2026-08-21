using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 조합법 힌트 팝업 UI — 아이템 하나의 조합법을 물음표 격자로 보여주고, 비용을 치를 때마다 한 칸씩 공개한다.
// 어떤 칸이 공개되는지는 아이템 ID로 정해진 순서(CraftingHintOrder)를 따르고, 몇 번 공개했는지는 ItemCodex가 맵 저장 파일에 기록한다.
public class CraftingHintPopupUI : OpenableUIBase<CraftingHintData>
{
    [Header("비용")]
    // 소모할 자원과 개수는 아이템마다 ItemData(HintCostResource·HintCost)에서 읽어 온다.
    [SerializeField] private Image _costIcon;
    [SerializeField] private TextMeshProUGUI _costText;

    [Header("헤더")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private Button _closeButton;

    [Header("격자")]
    [SerializeField] private CraftingHintCellUI _cellPrefab;
    [SerializeField] private RectTransform _gridParent;
    [SerializeField] private GridLayoutGroup _gridLayout;
    // 격자가 차지할 수 있는 최대 칸 크기. 조합법이 커지면 이 값보다 작게 줄여 영역 안에 맞춘다.
    [SerializeField, Min(1f)] private float _maxCellSize = 72f;

    [Header("힌트 버튼")]
    [SerializeField] private Button _hintButton;
    [SerializeField] private TextMeshProUGUI _hintButtonText;

    private ItemData _item;
    private ResourceInventory _inventory;
    private ItemCodex _codex;
    private readonly List<CraftingHintCellUI> _cells = new();

    private void Awake()
    {
        _closeButton.onClick.AddListener(HandleCloseButtonClick);
        _hintButton.onClick.AddListener(HandleHintButtonClick);
    }

    // 힌트를 볼 아이템과 비용을 치를 인벤토리, 공개 횟수를 기록할 도감을 주입받고 화면을 그린다.
    protected override void ApplyData(CraftingHintData data)
    {
        _item = data.Item;
        _inventory = data.Inventory;
        _codex = data.Codex;

        if (_codex != null)
            _codex.OnHintRevealed += HandleHintRevealed;

        if (_inventory != null)
        {
            _inventory.OnAddItem += HandleInventoryChanged;
            _inventory.OnRemoveItem += HandleInventoryChanged;
        }

        Refresh();
    }

    // 구독을 해제하고 격자를 비워 다음에 열릴 때 이전 아이템이 남지 않게 한다.
    protected override void OnReturnToPool()
    {
        if (_codex != null)
            _codex.OnHintRevealed -= HandleHintRevealed;

        if (_inventory != null)
        {
            _inventory.OnAddItem -= HandleInventoryChanged;
            _inventory.OnRemoveItem -= HandleInventoryChanged;
        }

        foreach (CraftingHintCellUI cell in _cells)
            cell.gameObject.SetActive(false);

        _item = null;
        _inventory = null;
        _codex = null;
    }

    // 닫기 버튼을 누르면 UI를 닫는다. (ESC로 닫는 UIManager 경로와 동일한 동작)
    private void HandleCloseButtonClick() => Close();

    // 비용을 차감하고 힌트를 한 칸 공개한다. 남은 칸이 없거나 자원이 모자라면 아무것도 하지 않는다.
    private void HandleHintButtonClick()
    {
        if (_item == null || _codex == null) return;
        if (!_codex.CanRevealHint(_item)) return;
        if (!HandlePayHintCost()) return;

        _codex.TryRevealHint(_item);
    }

    // 이번 힌트의 비용을 인벤토리에서 차감한다. 아이템에 비용 자원이 지정되지 않았거나 비용이 0이면 그냥 통과한다.
    private bool HandlePayHintCost()
    {
        if (!HasCost) return true;
        if (_inventory == null) return false;

        return _inventory.Remove(_item.HintCostResource, _item.HintCost);
    }

    // 이 아이템의 힌트에 실제로 치를 비용이 있는지 여부.
    private bool HasCost => _item != null && _item.HintCostResource != null && _item.HintCost > 0;

    // 이 팝업이 보고 있는 아이템의 힌트가 공개되면 화면을 다시 그린다.
    private void HandleHintRevealed(ItemData item)
    {
        if (item == _item) Refresh();
    }

    // 비용 자원 보유량이 바뀌면 버튼을 누를 수 있는지 다시 판정한다.
    private void HandleInventoryChanged(ItemData item, int newCount)
    {
        if (HasCost && item == _item.HintCostResource) RefreshHintButton();
    }

    // 제목·격자·비용·버튼을 현재 공개 상태에 맞춰 모두 다시 그린다.
    private void Refresh()
    {
        _titleText.text = _item != null ? $"{_item.Nmae} 조합법 힌트" : "조합법 힌트";

        RefreshGrid();
        RefreshCost();
        RefreshHintButton();
    }

    // 조합법의 크기에 맞춘 격자를 만들고, 공개된 칸만 재료를 드러낸다.
    private void RefreshGrid()
    {
        CraftingPattern pattern = _item != null ? _item.ToPattern() : null;
        Vector2Int size = pattern != null ? pattern.Size : Vector2Int.zero;
        if (size.x <= 0 || size.y <= 0)
        {
            HideCellsFrom(0);
            return;
        }

        ApplyGridLayout(size);

        HashSet<Vector2Int> revealed = _codex != null ? _codex.GetRevealedCoords(_item) : new HashSet<Vector2Int>();
        // 재료 칸을 모두 공개했다면 더 숨길 것이 없으므로, 남은 칸은 물음표 대신 빈 칸으로 보여준다.
        bool allRevealed = revealed.Count >= pattern.Cells.Count;

        int index = 0;
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                var coord = new Vector2Int(x, y);
                CraftingHintCellUI cell = GetOrCreateCell(index);
                index++;

                if (revealed.Contains(coord))
                    cell.ShowRevealed(pattern.Cells[coord]);
                else if (allRevealed)
                    cell.ShowEmpty();
                else
                    cell.ShowHidden();
            }
        }

        HideCellsFrom(index);
    }

    // 조합법 크기에 맞춰 격자의 열 수와 칸 크기를 정한다. 칸이 영역을 넘지 않도록 큰 조합법일수록 칸을 줄인다.
    private void ApplyGridLayout(Vector2Int size)
    {
        Vector2 area = _gridParent.rect.size;
        float spacing = _gridLayout.spacing.x;
        RectOffset padding = _gridLayout.padding;

        float width = (area.x - padding.left - padding.right - spacing * (size.x - 1)) / size.x;
        float height = (area.y - padding.top - padding.bottom - _gridLayout.spacing.y * (size.y - 1)) / size.y;
        float cellSize = Mathf.Max(1f, Mathf.Min(_maxCellSize, Mathf.Min(width, height)));

        _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _gridLayout.constraintCount = size.x;
        _gridLayout.cellSize = new Vector2(cellSize, cellSize);
    }

    // 아이템에 설정된 비용 자원의 아이콘과 개수를 표시한다. ("힌트 비용:" 문구 자체는 프리팹에 고정돼 있다)
    private void RefreshCost()
    {
        bool showIcon = HasCost && _item.HintCostResource.IconSprite != null;

        _costIcon.gameObject.SetActive(showIcon);
        if (showIcon)
            _costIcon.sprite = _item.HintCostResource.IconSprite;

        _costText.text = HasCost ? _item.HintCost.ToString() : "0";
    }

    // 남은 힌트와 보유 자원에 따라 버튼의 문구와 누를 수 있는지 여부를 갱신한다.
    private void RefreshHintButton()
    {
        if (_item == null || _codex == null)
        {
            _hintButton.interactable = false;
            return;
        }

        if (!_codex.CanRevealHint(_item))
        {
            _hintButton.interactable = false;
            _hintButtonText.text = _item.HasRecipe ? "모두 공개됨" : "조합법 없음";
            return;
        }

        bool canPay = !HasCost ||
                      (_inventory != null && _inventory.Has(_item.HintCostResource, _item.HintCost));

        _hintButton.interactable = canPay;
        _hintButtonText.text = canPay
            ? $"힌트 받기 ({_codex.GetHintCount(_item)}/{_codex.GetHintTotal(_item)})"
            : $"{_item.HintCostResource.Nmae} 부족";
    }

    // index번째 칸을 켜서 반환하고, 아직 없으면 새로 만들어 재사용 목록에 넣는다.
    private CraftingHintCellUI GetOrCreateCell(int index)
    {
        while (_cells.Count <= index)
            _cells.Add(Instantiate(_cellPrefab, _gridParent));

        _cells[index].transform.SetSiblingIndex(index);
        _cells[index].gameObject.SetActive(true);
        return _cells[index];
    }

    // 조합법이 작아져 남는 칸들을 격자에서 감춘다.
    private void HideCellsFrom(int index)
    {
        for (int i = index; i < _cells.Count; i++)
            _cells[i].gameObject.SetActive(false);
    }
}

// 힌트 팝업에 전달되는 페이로드 — 힌트를 볼 아이템, 비용을 치를 인벤토리, 공개 횟수를 기록할 도감
public readonly struct CraftingHintData
{
    public readonly ItemData Item;
    public readonly ResourceInventory Inventory;
    public readonly ItemCodex Codex;

    // 아이템·인벤토리·도감으로 페이로드를 구성한다.
    public CraftingHintData(ItemData item, ResourceInventory inventory, ItemCodex codex)
    {
        Item = item;
        Inventory = inventory;
        Codex = codex;
    }
}
