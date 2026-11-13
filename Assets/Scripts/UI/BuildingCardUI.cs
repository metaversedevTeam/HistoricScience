using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 건물 선택 UI 목록에 놓이는 가로 띠 모양의 건물 카드 한 장. 순번·썸네일·이름·필요 자원을 보여 주고,
// 누르면 선택(깜빡이는 테두리), 두 번 누르면 바로 건축 시작을 요청한다. 목록에 놓일 때 왼쪽에서 미끄러져 들어온다.
// 호버 연출은 본체에 붙은 MainMenuButtonMotion이 맡는다.
public class BuildingCardUI : MonoBehaviour, IPointerClickHandler
{
    [Header("카드")]
    // 카드 본체 버튼
    [SerializeField] private Button _button;

    // 등장할 때 미끄러질 카드 본체
    [SerializeField] private RectTransform _body;

    // 등장할 때 투명도를 다룰 캔버스 그룹(카드 맨 위 오브젝트)
    [SerializeField] private CanvasGroup _introGroup;

    // 선택됐을 때만 켜지는 테두리 묶음
    [SerializeField] private GameObject _selectionFrame;

    [Header("내용")]
    // 목록 순번 글자
    [SerializeField] private TextMeshProUGUI _indexText;
    [SerializeField] private Image _thumbnail;
    [SerializeField] private TextMeshProUGUI _nameText;

    [Header("건설 비용")]
    [SerializeField] private BuildCostChipUI _costChipPrefab;
    [SerializeField] private RectTransform _costChipParent;

    [Header("등장 연출")]
    // 등장 연출에 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _introDuration = 0.45f;

    // 등장 연출의 처음 위치가 원래 위치에서 떨어진 거리
    [SerializeField] private float _introOffset = -80f;

    // 이 카드에 생성해 둔 비용 칩들. 다시 Setup할 때 지우기 위해 들고 있는다.
    private readonly List<BuildCostChipUI> _costChips = new();

    // 눌러서 선택했을 때 부를 콜백
    private Action<BuildingCardUI> _onSelect;

    // 두 번 눌러 바로 건축을 시작하려 할 때 부를 콜백
    private Action<BuildingCardUI> _onSubmit;

    // 카드 본체의 원래 위치
    private Vector2 _bodyBase;

    // 등장 연출이 시작되기까지 남은 지연(초)
    private float _introDelay;

    // 등장 연출이 시작된 뒤 지난 시간(초)
    private float _introElapsed;

    // 등장 연출이 재생 중인지 여부
    private bool _isIntroPlaying;

    public IBuildable Buildable { get; private set; }

    private void Awake()
    {
        _bodyBase = _body.anchoredPosition;
        _button.onClick.AddListener(HandleClick);
    }

    private void Update()
    {
        HandleIntro();
    }

    // 건물 데이터와 콜백을 카드에 반영하고, introDelay초 뒤에 등장 연출을 시작한다. inventory는 필요 자원의 부족 여부를 판정하는 데 쓴다.
    public void Setup(int order, IBuildable buildable, ResourceInventory inventory, float introDelay, Action<BuildingCardUI> onSelect, Action<BuildingCardUI> onSubmit)
    {
        Buildable = buildable;
        _onSelect = onSelect;
        _onSubmit = onSubmit;

        _indexText.text = (order + 1).ToString("00");
        _thumbnail.sprite = buildable.Icon;
        _thumbnail.color = buildable.Icon != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);
        _nameText.text = buildable.BuildingName;

        PopulateCostChips(buildable.BuildCost, inventory);
        SetSelected(false);

        _introDelay = introDelay;
        _introElapsed = 0f;
        _isIntroPlaying = true;
        HandleApplyIntro(0f);
    }

    // 선택 여부에 따라 선택 테두리를 켜고 끈다.
    public void SetSelected(bool selected)
    {
        _selectionFrame.SetActive(selected);
    }

    // 두 번 누르면 바로 건축 시작을 요청한다. (첫 번째 누름은 버튼의 onClick이 선택으로 처리한다)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || eventData.clickCount != 2)
            return;

        _onSubmit?.Invoke(this);
    }

    // 카드를 눌러 선택했음을 알린다.
    private void HandleClick()
    {
        _onSelect?.Invoke(this);
    }

    // 지연이 지나면 등장 연출을 진행하고, 끝나면 멈춘다.
    private void HandleIntro()
    {
        if (!_isIntroPlaying)
            return;

        _introElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01((_introElapsed - _introDelay) / _introDuration);
        HandleApplyIntro(t);

        if (t >= 1f)
            _isIntroPlaying = false;
    }

    // 등장 진행도(0~1)를 위치와 투명도에 반영한다.
    private void HandleApplyIntro(float t)
    {
        float eased = 1f - Mathf.Pow(1f - t, 4f);
        _body.anchoredPosition = _bodyBase + new Vector2(_introOffset * (1f - eased), 0f);
        _introGroup.alpha = Mathf.Clamp01(t * 1.6f);
    }

    // 기존 비용 칩을 지우고 자원별로 칩을 새로 만든다. 카드에는 필요 수량만 적고 부족 여부는 색으로 표시한다.
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

    // 만들어 둔 비용 칩의 부족 여부 색을 지금 인벤토리 기준으로 다시 칠한다. 칩은 건설 비용과 같은 순서로 만들어져 있다.
    public void RefreshCostChips(ResourceInventory inventory)
    {
        if (Buildable == null) return;

        int index = 0;
        foreach (var cost in Buildable.BuildCost)
        {
            if (index >= _costChips.Count) break;

            _costChips[index].SetupRequiredOnly(cost.Key, cost.Value, inventory != null ? inventory.Get(cost.Key) : 0);
            index++;
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
