using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 고인돌의 연구 목록 UI — 연구 목록을 카드 격자로 나열하고, 카드를 누르면 상세 패널을 연다.
// 완료·시대 잠금 같은 상태는 씬의 ResearchManager에서 읽는다. 도감과 같은 모달 창 디자인과 등장·퇴장 연출을 쓴다.
public class ResearchUI : OpenableUIBase<ResearchListData>
{
    [Header("헤더")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _subtitleText;
    [SerializeField] private Button _closeButton;

    [Header("목록")]
    [SerializeField] private ResearchCardUI _cardPrefab;
    [SerializeField] private RectTransform _cardParent;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private TextMeshProUGUI _emptyText;

    [Header("진행도")]
    // "연구 완료: 3/10 (30%)" 형태로 진행도를 보여 줄 글자 (비워 두면 표시하지 않는다)
    [SerializeField] private TextMeshProUGUI _progressText;
    // 진행도만큼 가로로 차오를 막대. 앵커 최대값 x로 길이를 정하므로 앵커는 왼쪽 끝(0)에서 시작해야 한다.
    [SerializeField] private RectTransform _progressFill;

    [Header("상세 패널")]
    [SerializeField] private ResearchDetailUI _detailPrefab;

    [Header("연출")]
    // 열릴 때 창을 들여오고, 닫힐 때 거꾸로 내보내는 연출 (비워 두면 연출 없이 바로 닫힌다)
    [SerializeField] private MainMenuIntroSequence _introSequence;

    // 이번 열림 동안 표시할 연구 목록
    private ResearchDataList _researchDataList;

    // 연구 비용을 치를 인벤토리. 연구 목록을 연 쪽이 넘겨준다.
    private ResourceInventory _inventory;

    // 상태를 읽어 오는 관리자
    private ResearchManager _manager;

    // 이 목록이 열어 둔 상세 패널. 닫히면 다시 null이 되어 창이 겹쳐 쌓이지 않게 한다.
    private ResearchDetailUI _openDetail;

    // 다시 채우거나 정리할 때 쓰는, 생성해 둔 카드 목록
    private readonly List<ResearchCardUI> _cards = new();

    private void Awake()
    {
        _closeButton.onClick.AddListener(HandleCloseButtonClick);
    }

    private void OnEnable()
    {
        _manager = ResearchManager.Instance;
        _manager.OnCompleted += HandleResearchChanged;
        _manager.OnAgeChanged += HandleAgeChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (_manager == null) return;

        _manager.OnCompleted -= HandleResearchChanged;
        _manager.OnAgeChanged -= HandleAgeChanged;
        _manager = null;
    }

    // 표시할 연구 목록과 비용을 치를 인벤토리를 주입받고 목록을 다시 그린다.
    protected override void ApplyData(ResearchListData data)
    {
        _researchDataList = data.ResearchDataList;
        _inventory = data.Inventory;

        if (!string.IsNullOrEmpty(data.Title))
            _titleText.text = data.Title;

        Refresh();
    }

    // 등장 연출을 거꾸로 재생해 창을 내보낸 뒤 닫기를 마무리한다.
    protected override void PlayCloseTransition()
    {
        if (_introSequence == null || !_introSequence.isActiveAndEnabled)
        {
            FinishClose();
            return;
        }

        _introSequence.PlayReverse(FinishClose);
    }

    // 풀로 돌아가기 전에 열어 둔 상세 패널을 정리하고 스크롤 위치를 되돌린다.
    protected override void OnReturnToPool()
    {
        if (_openDetail != null)
            _openDetail.Close(immediate: true);

        UnsubscribeFromDetail();
        _inventory = null;
        _researchDataList = null;

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }

    // 닫기 버튼을 누르면 UI를 닫는다. (ESC로 닫는 UIManager 경로와 동일한 동작)
    private void HandleCloseButtonClick() => Close();

    // 연구가 끝나면 카드 상태를 다시 그린다.
    private void HandleResearchChanged(ResearchData _) => Refresh();

    // 시대가 바뀌면 시대 제한이 달라지므로 카드 상태를 다시 그린다.
    private void HandleAgeChanged(Age _) => Refresh();

    // 카드 격자와 현재 시대·진행도 표시를 모두 다시 그린다.
    private void Refresh()
    {
        RefreshCards();
        RefreshProgress();
    }

    // 연구 목록만큼 카드를 켜서 채우고, 남는 카드는 끈다.
    private void RefreshCards()
    {
        IReadOnlyList<ResearchData> researches = Researches;
        int visibleCount = 0;

        for (int i = 0; i < researches.Count; i++)
        {
            ResearchData research = researches[i];
            if (research == null) continue;

            ResearchCardUI card = GetOrCreateCard(visibleCount);
            card.gameObject.SetActive(true);
            card.Setup(research, visibleCount + 1, StateOf(research), MakeDetailCallback(research));
            visibleCount++;
        }

        for (int i = visibleCount; i < _cards.Count; i++)
            _cards[i].gameObject.SetActive(false);

        _emptyText.gameObject.SetActive(visibleCount == 0);
    }

    // 현재 시대와 연구 진행도(글자·막대)를 표시한다.
    private void RefreshProgress()
    {
        IReadOnlyList<ResearchData> researches = Researches;
        int completed = 0;
        int total = 0;

        foreach (ResearchData research in researches)
        {
            if (research == null) continue;

            total++;
            if (_manager != null && _manager.IsCompleted(research)) completed++;
        }

        Age age = _manager != null ? _manager.CurrentAge : Age.nature;
        _subtitleText.text = $"현재 {age.ToTabName()}";

        float ratio = total > 0 ? (float)completed / total : 0f;
        if (_progressText != null)
            _progressText.text = $"연구 완료: {completed}/{total} ({Mathf.RoundToInt(ratio * 100f)}%)";

        if (_progressFill != null)
            _progressFill.anchorMax = new Vector2(ratio, _progressFill.anchorMax.y);
    }

    // index번째 카드를 반환하고, 아직 없으면 새로 만들어 재사용 목록에 넣는다.
    private ResearchCardUI GetOrCreateCard(int index)
    {
        while (_cards.Count <= index)
            _cards.Add(Instantiate(_cardPrefab, _cardParent));

        _cards[index].transform.SetSiblingIndex(index);
        return _cards[index];
    }

    // 카드를 눌렀을 때 해당 연구의 상세 패널을 여는 콜백을 만든다.
    private System.Action MakeDetailCallback(ResearchData research) => () => OpenDetail(research);

    // 해당 연구의 상세 패널을 연다. 이미 떠 있는 패널이 있으면 다시 열지 않는다.
    private void OpenDetail(ResearchData research)
    {
        if (_openDetail != null) return;

        if (_detailPrefab == null)
        {
            Debug.LogWarning($"ResearchUI({name}): 상세 패널 프리팹이 설정되지 않아 연구 상세를 열 수 없습니다.");
            return;
        }

        _openDetail = UIManager.Instance.OpenUI(_detailPrefab, new ResearchDetailData(research, _inventory));
        _openDetail.OnFinishClose += HandleDetailClosed;
    }

    // 상세 패널이 닫히면 구독을 해제해 다시 열 수 있게 한다.
    private void HandleDetailClosed(IManagedUI ui) => UnsubscribeFromDetail();

    // 열어 둔 상세 패널의 구독을 해제하고 참조를 비운다.
    private void UnsubscribeFromDetail()
    {
        if (_openDetail == null) return;

        _openDetail.OnFinishClose -= HandleDetailClosed;
        _openDetail = null;
    }

    // 이번 열림에서 표시할 연구 목록. 목록이 연결되지 않았으면 빈 목록으로 본다.
    private IReadOnlyList<ResearchData> Researches =>
        _researchDataList != null ? _researchDataList.Researches : System.Array.Empty<ResearchData>();

    // 관리자에서 연구 상태를 읽는다. 관리자가 없으면 시대 잠금으로 본다.
    private ResearchState StateOf(ResearchData research) =>
        _manager != null ? _manager.GetState(research) : ResearchState.AgeLocked;
}

// 연구 목록 UI를 열 때 넘기는 페이로드 (표시할 연구 목록, 비용을 치를 인벤토리, 창 제목)
public readonly struct ResearchListData
{
    public readonly ResearchDataList ResearchDataList;
    public readonly ResourceInventory Inventory;
    public readonly string Title;

    public ResearchListData(ResearchDataList researchDataList, ResourceInventory inventory, string title)
    {
        ResearchDataList = researchDataList;
        Inventory = inventory;
        Title = title;
    }
}
