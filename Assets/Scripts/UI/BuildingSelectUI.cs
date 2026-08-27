using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 건축할 건물 선택 UI — 건물 목록을 카드 격자로 나열하고, 카드를 고르면 오른쪽 상세 패널에 설명·건설 비용을 보여준다.
// 상세 패널의 건축 시작 버튼을 눌러야 실제 선택이 확정되며, 건물 선택·닫기를 콜백으로 알린다.
public class BuildingSelectUI : OpenableUIBase<BuildingSelectData>
{
    // 건축 시작 버튼을 눌러 건물 선택이 확정됐을 때 선택된 건물을 알리는 콜백
    public event Action<IBuildable> OnBuildingSelected;

    // 건물을 하나도 확정하지 않은 채 UI가 닫혔을 때 알리는 콜백
    public event Action OnClosedWithoutSelection;

    [Header("목록")]
    [SerializeField] private BuildingCardUI _buildingCardPrefab;
    [SerializeField] private RectTransform _content;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private TextMeshProUGUI _countText;

    [Header("상세 패널")]
    // 건물을 고르기 전에 대신 보여줄 안내 오브젝트
    [SerializeField] private GameObject _detailPlaceholder;
    // 건물을 고른 뒤에만 켜지는 상세 내용 묶음
    [SerializeField] private GameObject _detailBody;
    [SerializeField] private Image _detailThumbnail;
    [SerializeField] private TextMeshProUGUI _detailNameText;
    [SerializeField] private TextMeshProUGUI _detailDescriptionText;
    [SerializeField] private BuildCostChipUI _costChipPrefab;
    [SerializeField] private RectTransform _detailCostChipParent;
    [SerializeField] private Button _buildStartButton;

    [Header("닫기")]
    [SerializeField] private Button _closeButton;

    // 생성해 둔 건물 카드들. 다시 채우거나 정리할 때 쓴다.
    private readonly List<BuildingCardUI> _cards = new();

    // 상세 패널에 생성해 둔 비용 칩들. 선택이 바뀔 때마다 지우고 다시 만든다.
    private readonly List<BuildCostChipUI> _detailCostChips = new();

    // 상세 패널에 표시 중인 건물. 아직 아무것도 고르지 않았으면 null이다.
    private IBuildable _selectedBuildable;

    // 건설 비용의 보유량을 조회할 인벤토리. 이번 열림 동안 전달받은 것을 들고 있는다.
    private ResourceInventory _inventory;

    // 이번 열림 동안 건축 시작으로 건물을 확정했는지 여부
    private bool _hasConfirmedBuilding;

    private void Awake()
    {
        _closeButton.onClick.AddListener(HandleCloseButtonClick);
        _buildStartButton.onClick.AddListener(HandleBuildStartButtonClick);
    }

    // 전달받은 건물 목록으로 카드 격자를 채우고 상세 패널을 선택 전 상태로 되돌린다.
    protected override void ApplyData(BuildingSelectData data)
    {
        _hasConfirmedBuilding = false;
        _inventory = data.Inventory;
        PopulateCards(data.Buildables);
        SelectBuildable(null);
    }

    // 풀 반납 전 생성된 카드와 스크롤 위치를 정리하고, 건물을 확정하지 않고 닫힌 경우 콜백을 발행한다.
    protected override void OnReturnToPool()
    {
        ClearCards();
        ClearDetailCostChips();
        _selectedBuildable = null;
        _inventory = null;

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;

        if (!_hasConfirmedBuilding)
            OnClosedWithoutSelection?.Invoke();
    }

    // 닫기 버튼 클릭 시 UI를 닫는다.
    private void HandleCloseButtonClick() => Close();

    // 건축 시작 버튼 클릭 시 선택을 확정하고 선택 콜백을 발행한다.
    private void HandleBuildStartButtonClick()
    {
        if (_selectedBuildable == null) return;

        AudioManager.PlayConfirm();
        _hasConfirmedBuilding = true;
        OnBuildingSelected?.Invoke(_selectedBuildable);
    }

    // 건물 카드를 눌렀을 때 효과음을 내고 상세 패널의 대상을 그 건물로 바꾼다.
    private void HandleCardClick(IBuildable buildable)
    {
        AudioManager.PlayButtonClick();
        SelectBuildable(buildable);
    }

    // 기존 카드를 제거하고 건물 목록마다 카드를 새로 생성하며, 목록 개수 표시도 갱신한다.
    private void PopulateCards(IReadOnlyList<IBuildable> buildables)
    {
        ClearCards();

        foreach (var buildable in buildables)
        {
            BuildingCardUI card = Instantiate(_buildingCardPrefab, _content);
            card.Setup(buildable, _inventory);
            card.Button.onClick.AddListener(() => HandleCardClick(buildable));
            _cards.Add(card);
        }

        _countText.text = $"{buildables.Count} 건물";
    }

    // 생성해 둔 건물 카드를 모두 제거한다.
    private void ClearCards()
    {
        foreach (BuildingCardUI card in _cards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        _cards.Clear();
    }

    // 상세 패널에 표시할 건물을 바꾸고 카드 강조와 패널 내용을 함께 갱신한다. null이면 선택 전 상태로 되돌린다.
    private void SelectBuildable(IBuildable buildable)
    {
        _selectedBuildable = buildable;

        foreach (BuildingCardUI card in _cards)
            card.SetSelected(card.Buildable == buildable);

        RefreshDetail();
    }

    // 현재 선택에 맞춰 상세 패널의 안내/내용 표시와 건축 시작 버튼 활성화를 갱신한다.
    private void RefreshDetail()
    {
        bool hasSelection = _selectedBuildable != null;

        _detailPlaceholder.SetActive(!hasSelection);
        _detailBody.SetActive(hasSelection);
        _buildStartButton.interactable = hasSelection;

        if (!hasSelection)
        {
            ClearDetailCostChips();
            return;
        }

        _detailThumbnail.sprite = _selectedBuildable.Icon;
        _detailThumbnail.color = _selectedBuildable.Icon != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);
        _detailNameText.text = _selectedBuildable.BuildingName;
        _detailDescriptionText.text = _selectedBuildable.Description;

        PopulateDetailCostChips(_selectedBuildable.BuildCost);
    }

    // 상세 패널의 기존 비용 칩을 지우고 자원별로 보유/필요 수량 칩을 새로 만든다.
    private void PopulateDetailCostChips(IReadOnlyDictionary<ResourceData, int> buildCost)
    {
        ClearDetailCostChips();

        foreach (var cost in buildCost)
        {
            BuildCostChipUI chip = Instantiate(_costChipPrefab, _detailCostChipParent);
            chip.SetupWithOwned(cost.Key, cost.Value, _inventory != null ? _inventory.Get(cost.Key) : 0);
            _detailCostChips.Add(chip);
        }
    }

    // 상세 패널에 생성해 둔 비용 칩을 모두 제거한다.
    private void ClearDetailCostChips()
    {
        foreach (BuildCostChipUI chip in _detailCostChips)
        {
            if (chip != null)
                Destroy(chip.gameObject);
        }
        _detailCostChips.Clear();
    }
}

// 건물 선택 UI에 전달되는 페이로드 — 나열할 건물 목록과 건설 비용의 보유량을 비교할 인벤토리
public readonly struct BuildingSelectData
{
    public readonly IReadOnlyList<IBuildable> Buildables;
    public readonly ResourceInventory Inventory;

    // 건물 목록과 비교할 인벤토리로 페이로드를 구성한다.
    public BuildingSelectData(IReadOnlyList<IBuildable> buildables, ResourceInventory inventory)
    {
        Buildables = buildables;
        Inventory = inventory;
    }
}
