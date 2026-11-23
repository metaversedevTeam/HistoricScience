using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 건축할 건물 선택 UI — 메인 메뉴와 같은 사각형 모션그래픽 디자인으로 건물 목록을 가로 띠 카드로 나열하고,
// 카드를 고르면 오른쪽 상세 패널에 설명·건설 비용을 보여준다. 건축 시작 버튼으로 선택이 확정되며,
// 건물 선택·닫기를 콜백으로 알린다. 열고 닫을 때의 등장·퇴장 연출은 MainMenuIntroSequence가 맡는다.
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

    // UI가 열린 뒤 첫 카드가 등장하기까지의 지연(초)
    [SerializeField, Min(0f)] private float _cardIntroDelay = 0.25f;

    // 카드끼리 등장 시점의 간격(초)
    [SerializeField, Min(0f)] private float _cardIntroStagger = 0.05f;

    [Header("상세 패널")]
    // 건물을 고르기 전에 대신 보여줄 안내 오브젝트
    [SerializeField] private GameObject _detailPlaceholder;
    // 건물을 고른 뒤에만 켜지는 상세 내용 묶음
    [SerializeField] private GameObject _detailBody;
    // 선택이 바뀔 때마다 옆에서 미끄러져 들어올 상세 내용 위치
    [SerializeField] private RectTransform _detailBodyRect;
    // 상세 내용이 미끄러져 들어올 때 투명도를 다룰 캔버스 그룹
    [SerializeField] private CanvasGroup _detailBodyGroup;
    // 패널 왼쪽 위 "SELECTED · 01" 꼬리표 글자
    [SerializeField] private TextMeshProUGUI _detailTagText;
    [SerializeField] private Image _detailThumbnail;
    [SerializeField] private TextMeshProUGUI _detailNameText;
    [SerializeField] private TextMeshProUGUI _detailDescriptionText;
    [SerializeField] private BuildCostCellUI _costCellPrefab;
    [SerializeField] private RectTransform _detailCostCellParent;
    [SerializeField] private Button _buildStartButton;

    // 상세 내용이 미끄러져 들어오는 데 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _detailSlideDuration = 0.38f;

    // 상세 내용이 미끄러져 들어오기 시작하는 거리
    [SerializeField] private float _detailSlideOffset = 36f;

    [Header("닫기")]
    [SerializeField] private Button _closeButton;

    [Header("연출")]
    // 열릴 때 요소들을 차례로 들여오고, 닫힐 때 거꾸로 내보내는 연출
    [SerializeField] private MainMenuIntroSequence _introSequence;

    // 생성해 둔 건물 카드들. 다시 채우거나 정리할 때 쓴다.
    private readonly List<BuildingCardUI> _cards = new();

    // 상세 패널에 생성해 둔 비용 칸들. 선택이 바뀔 때마다 지우고 다시 만든다.
    private readonly List<BuildCostCellUI> _detailCostCells = new();

    // 상세 패널에 표시 중인 건물. 아직 아무것도 고르지 않았으면 null이다.
    private IBuildable _selectedBuildable;

    // 건설 비용의 보유량을 조회할 인벤토리. 이번 열림 동안 전달받은 것을 들고 있는다.
    private ResourceInventory _inventory;

    // 이번 열림 동안 나열한 건물 목록. 지금 지을 수 있는 건물 수를 다시 셀 때 쓴다.
    private IReadOnlyList<IBuildable> _buildables;

    // 이번 열림 동안 건축 시작으로 건물을 확정했는지 여부
    private bool _hasConfirmedBuilding;

    // 상세 내용의 원래 위치
    private Vector2 _detailBodyBase;

    // 상세 내용 미끄러짐 연출이 시작된 뒤 지난 시간(초). 연출 시간을 넘기면 멈춘 상태다.
    private float _detailSlideElapsed = float.MaxValue;

    private void Awake()
    {
        _closeButton.onClick.AddListener(HandleCloseButtonClick);
        _buildStartButton.onClick.AddListener(HandleBuildStartButtonClick);
        _detailBodyBase = _detailBodyRect.anchoredPosition;
    }

    private void Update()
    {
        HandleDetailSlide();
    }

    // 전달받은 건물 목록으로 카드 목록을 채우고 상세 패널을 선택 전 상태로 되돌린다.
    protected override void ApplyData(BuildingSelectData data)
    {
        _hasConfirmedBuilding = false;
        _inventory = data.Inventory;
        _buildables = data.Buildables;
        SubscribeToInventory();
        PopulateCards(data.Buildables);
        RefreshAvailableCount();
        SelectBuildable(null);
    }

    // 등장 연출은 활성화될 때 MainMenuIntroSequence가 스스로 재생하므로 바로 열림 상태로 넘어간다.
    protected override void PlayOpenTransition()
    {
        FinishOpen();
    }

    // 등장 연출을 거꾸로 재생해 요소들을 내보낸 뒤 닫기를 마무리한다.
    protected override void PlayCloseTransition()
    {
        if (_introSequence == null || !_introSequence.isActiveAndEnabled)
        {
            FinishClose();
            return;
        }

        _introSequence.PlayReverse(FinishClose);
    }

    // 풀 반납 전 생성된 카드와 스크롤 위치를 정리하고, 건물을 확정하지 않고 닫힌 경우 콜백을 발행한다.
    protected override void OnReturnToPool()
    {
        ClearCards();
        ClearDetailCostCells();
        UnsubscribeFromInventory();
        _selectedBuildable = null;
        _inventory = null;
        _buildables = null;

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;

        if (!_hasConfirmedBuilding)
            OnClosedWithoutSelection?.Invoke();
    }

    // 닫기 버튼 클릭 시 UI를 닫는다.
    private void HandleCloseButtonClick() => Close();

    // 건축 시작 버튼 클릭 시 선택을 확정한다.
    private void HandleBuildStartButtonClick() => ConfirmSelection();

    // 건물 카드를 눌렀을 때 효과음을 내고 상세 패널의 대상을 그 건물로 바꾼다.
    private void HandleCardSelect(BuildingCardUI card)
    {
        AudioManager.PlayButtonClick();
        SelectBuildable(card.Buildable);
    }

    // 고른 건물이 있고 건축 시작이 가능하면 선택을 확정하고 선택 콜백을 발행한다.
    private void ConfirmSelection()
    {
        if (_selectedBuildable == null || !_buildStartButton.interactable) return;
        if (_hasConfirmedBuilding) return;

        AudioManager.PlayConfirm();
        _hasConfirmedBuilding = true;
        OnBuildingSelected?.Invoke(_selectedBuildable);
    }

    // 기존 카드를 제거하고 건물 목록마다 카드를 새로 생성하며, 목록 개수 표시도 갱신한다.
    private void PopulateCards(IReadOnlyList<IBuildable> buildables)
    {
        ClearCards();

        for (int i = 0; i < buildables.Count; i++)
        {
            BuildingCardUI card = Instantiate(_buildingCardPrefab, _content);
            card.Setup(i, buildables[i], _inventory, _cardIntroDelay + _cardIntroStagger * i, HandleCardSelect);
            _cards.Add(card);
        }
    }

    // 지금 가진 자원으로 바로 지을 수 있는 건물 수를 세어 AVAILABLE 숫자에 표시한다.
    private void RefreshAvailableCount()
    {
        int count = 0;
        if (_buildables != null)
        {
            foreach (IBuildable buildable in _buildables)
            {
                if (CanAfford(buildable))
                    count++;
            }
        }

        _countText.text = count.ToString("00");
    }

    // 지정한 건물의 건설 비용을 인벤토리가 모두 감당할 수 있는지 확인한다.
    private bool CanAfford(IBuildable buildable)
    {
        if (_inventory == null) return false;

        foreach (var cost in buildable.BuildCost)
        {
            if (!_inventory.Has(cost.Key, cost.Value))
                return false;
        }
        return true;
    }

    // 열려 있는 동안 자원이 늘거나 줄면 AVAILABLE 숫자를 다시 세도록 인벤토리 변화를 구독한다.
    private void SubscribeToInventory()
    {
        if (_inventory == null) return;

        _inventory.OnAddItem += HandleInventoryChanged;
        _inventory.OnRemoveItem += HandleInventoryChanged;
    }

    // 인벤토리 변화 구독을 해제한다.
    private void UnsubscribeFromInventory()
    {
        if (_inventory == null) return;

        _inventory.OnAddItem -= HandleInventoryChanged;
        _inventory.OnRemoveItem -= HandleInventoryChanged;
    }

    // 자원이 바뀌면 지을 수 있는 건물 수를 다시 세고, 카드의 비용 칩과 상세 패널의 비용 칸을 지금 보유량으로 다시 칠한다.
    private void HandleInventoryChanged(ItemData item, int amount)
    {
        RefreshAvailableCount();

        foreach (BuildingCardUI card in _cards)
            card.RefreshCostChips(_inventory);

        RefreshDetailCostCells();
    }

    // 상세 패널의 비용 칸을 지금 보유량으로 다시 칠한다. 막대는 연출 없이 바로 새 길이로 맞춘다.
    private void RefreshDetailCostCells()
    {
        if (_selectedBuildable == null) return;

        int index = 0;
        foreach (var cost in _selectedBuildable.BuildCost)
        {
            if (index >= _detailCostCells.Count) break;

            _detailCostCells[index].Setup(cost.Key, cost.Value, _inventory != null ? _inventory.Get(cost.Key) : 0, false);
            index++;
        }
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

        for (int i = 0; i < _cards.Count; i++)
        {
            bool selected = _cards[i].Buildable == buildable;
            _cards[i].SetSelected(selected);
            if (selected)
                _detailTagText.text = $"SELECTED  ·  {i + 1:00}";
        }

        RefreshDetail();
    }

    // 현재 선택에 맞춰 상세 패널의 안내/내용 표시와 건축 시작 버튼 활성화를 갱신한다.
    private void RefreshDetail()
    {
        bool hasSelection = _selectedBuildable != null;

        _detailPlaceholder.SetActive(!hasSelection);
        _detailBody.SetActive(hasSelection);

        if (!hasSelection)
        {
            _buildStartButton.interactable = false;
            ClearDetailCostCells();
            return;
        }

        _detailThumbnail.sprite = _selectedBuildable.Icon;
        _detailThumbnail.color = _selectedBuildable.Icon != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);
        _detailNameText.text = _selectedBuildable.BuildingName;
        _detailDescriptionText.text = _selectedBuildable.Description;

        PopulateDetailCostCells(_selectedBuildable.BuildCost);
        _buildStartButton.interactable = true;

        _detailSlideElapsed = 0f;
        HandleApplyDetailSlide(0f);
    }

    // 상세 패널의 기존 비용 칸을 지우고 자원별로 보유/필요 수량 칸을 새로 만든다.
    private void PopulateDetailCostCells(IReadOnlyDictionary<ResourceData, int> buildCost)
    {
        ClearDetailCostCells();

        foreach (var cost in buildCost)
        {
            BuildCostCellUI cell = Instantiate(_costCellPrefab, _detailCostCellParent);
            cell.Setup(cost.Key, cost.Value, _inventory != null ? _inventory.Get(cost.Key) : 0);
            _detailCostCells.Add(cell);
        }
    }

    // 상세 패널에 생성해 둔 비용 칸을 모두 제거한다.
    private void ClearDetailCostCells()
    {
        foreach (BuildCostCellUI cell in _detailCostCells)
        {
            if (cell != null)
                Destroy(cell.gameObject);
        }
        _detailCostCells.Clear();
    }

    // 선택이 바뀐 직후 상세 내용을 옆에서 미끄러져 들어오게 한다.
    private void HandleDetailSlide()
    {
        if (_detailSlideElapsed >= _detailSlideDuration)
            return;

        _detailSlideElapsed += Time.unscaledDeltaTime;
        HandleApplyDetailSlide(Mathf.Clamp01(_detailSlideElapsed / _detailSlideDuration));
    }

    // 미끄러짐 진행도(0~1)를 상세 내용의 위치와 투명도에 반영한다.
    private void HandleApplyDetailSlide(float t)
    {
        float eased = 1f - Mathf.Pow(1f - t, 4f);
        _detailBodyRect.anchoredPosition = _detailBodyBase + new Vector2(_detailSlideOffset * (1f - eased), 0f);
        _detailBodyGroup.alpha = Mathf.Clamp01(t * 1.6f);
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
