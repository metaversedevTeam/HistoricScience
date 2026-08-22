using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 건물 선택 UI의 목록 격자에 놓이는 건물 카드 하나. 썸네일·이름·필요 자원·건설 시간을 보여주고, 선택되면 테두리로 강조된다.
public class BuildingCardUI : MonoBehaviour
{
    [Header("카드")]
    [SerializeField] private Image _cardFill;
    [SerializeField] private Image _cardOutline;

    [Header("내용")]
    [SerializeField] private Image _thumbnail;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _buildTimeText;

    [Header("건설 비용")]
    [SerializeField] private BuildCostChipUI _costChipPrefab;
    [SerializeField] private RectTransform _costChipParent;

    [Header("선택됨")]
    [SerializeField] private Color _selectedFill = new Color32(0x14, 0x1F, 0x36, 0xFF);
    [SerializeField] private Color _selectedOutline = new Color32(0xE2, 0x52, 0x1A, 0xFF);

    [Header("선택 안 됨")]
    [SerializeField] private Color _normalFill = new Color32(0x0C, 0x14, 0x24, 0xFF);
    [SerializeField] private Color _normalOutline = new Color32(0x2A, 0x35, 0x50, 0xFF);

    // 이 카드에 생성해 둔 비용 칩들. 다시 Setup할 때 지우기 위해 들고 있는다.
    private readonly List<BuildCostChipUI> _costChips = new();

    public IBuildable Buildable { get; private set; }
    public Button Button { get; private set; }

    private void Awake()
    {
        Button = GetComponent<Button>();
    }

    // 건물 데이터를 카드에 반영하고 선택되지 않은 상태로 되돌린다. inventory는 필요 자원의 부족 여부를 판정하는 데 쓴다.
    public void Setup(IBuildable buildable, ResourceInventory inventory)
    {
        Buildable = buildable;

        _thumbnail.sprite = buildable.Icon;
        _thumbnail.color = buildable.Icon != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);
        _nameText.text = buildable.BuildingName;
        _buildTimeText.text = buildable.BuildTime.ToBuildTimeText();

        PopulateCostChips(buildable.BuildCost, inventory);
        SetSelected(false);
    }

    // 선택 여부에 따라 카드 배경과 테두리 색을 갱신한다.
    public void SetSelected(bool selected)
    {
        _cardFill.color = selected ? _selectedFill : _normalFill;
        _cardOutline.color = selected ? _selectedOutline : _normalOutline;
    }

    // 기존 비용 칩을 지우고 자원별로 칩을 새로 만든다. 카드는 폭이 좁아 필요 수량만 적고 부족 여부는 색으로 표시한다.
    private void PopulateCostChips(IReadOnlyDictionary<ResourceData, int> buildCost, ResourceInventory inventory)
    {
        ClearCostChips();

        foreach (var cost in buildCost)
        {
            BuildCostChipUI chip = Instantiate(_costChipPrefab, _costChipParent);
            chip.SetupRequiredOnly(cost.Key, cost.Value, inventory != null ? inventory.Get(cost.Key) : 0);
            _costChips.Add(chip);
        }
    }

    // 생성해 둔 비용 칩을 모두 제거한다.
    private void ClearCostChips()
    {
        foreach (BuildCostChipUI chip in _costChips)
        {
            if (chip != null)
                Destroy(chip.gameObject);
        }
        _costChips.Clear();
    }
}
