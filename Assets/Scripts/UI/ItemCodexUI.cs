using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 인류 문명 도감 UI — 아이템 목록을 시대 필터·이름 검색으로 추려 보여주고, 전체 수집 달성도를 표시하는 관리형 UI.
// 획득 여부는 씬에 있는 ItemCodex에서 읽으며, ItemCodex가 없으면 전부 미획득으로 표시한다.
// 힌트 비용을 치를 인벤토리를 페이로드로 받아, 미획득 카드의 힌트 버튼으로 조합법 힌트 팝업을 연다.
// 이미 획득한 카드의 "수집 완료" 버튼은 같은 팝업을 전체 공개 모드로 열어 조합법 전체를 보여준다.
public class ItemCodexUI : OpenableUIBase<ResourceInventory>
{
    [Header("데이터")]
    [SerializeField] private ItemDataList _itemDataList;
    // 아직 Age 열거형에 없어 잠금 상태로만 노출할 시대 탭 라벨 (예: "철기 시대 (잠금)")
    [SerializeField] private string[] _lockedTabLabels = { "철기 시대 (잠금)" };

    [Header("탭")]
    [SerializeField] private CodexAgeTabUI _tabPrefab;
    [SerializeField] private RectTransform _tabParent;

    [Header("목록")]
    [SerializeField] private ItemCodexEntryUI _entryPrefab;
    [SerializeField] private RectTransform _entryParent;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private TextMeshProUGUI _emptyText;

    [Header("힌트")]
    [SerializeField] private CraftingHintPopupUI _hintPopupPrefab;

    [Header("검색")]
    [SerializeField] private TMP_InputField _searchInput;

    [Header("닫기")]
    [SerializeField] private Button _closeButton;

    [Header("달성도")]
    [SerializeField] private RectTransform _progressFill;
    [SerializeField] private TextMeshProUGUI _progressText;

    // 전체 탭을 나타내는 값. 특정 시대가 선택되면 그 시대가 들어간다.
    private Age? _selectedAge;

    private ItemCodex _codex;

    // 힌트 비용을 차감할 인벤토리. 도감을 연 쪽이 넘겨준다.
    private ResourceInventory _inventory;

    // 이 도감이 열어 둔 힌트 팝업. 닫히면 다시 null이 되어 창이 겹쳐 쌓이지 않게 한다.
    private CraftingHintPopupUI _openHintPopup;

    private readonly List<CodexAgeTabUI> _tabs = new();
    private readonly List<Age?> _tabAges = new();
    private readonly List<ItemCodexEntryUI> _entries = new();

    private void Awake()
    {
        BuildTabs();
        _searchInput.onValueChanged.AddListener(HandleSearchChanged);
        _closeButton.onClick.AddListener(HandleCloseButtonClick);
    }

    private void OnEnable()
    {
        _codex = FindFirstObjectByType<ItemCodex>();
        if (_codex != null)
        {
            _codex.OnDiscover += HandleDiscover;
            _codex.OnHintRevealed += HandleHintRevealed;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (_codex != null)
        {
            _codex.OnDiscover -= HandleDiscover;
            _codex.OnHintRevealed -= HandleHintRevealed;
        }

        _codex = null;
    }

    // 힌트 비용을 치를 인벤토리를 주입받고 목록을 다시 그린다.
    protected override void ApplyData(ResourceInventory data)
    {
        _inventory = data;
        Refresh();
    }

    // 풀로 돌아가기 전에 검색어·선택 탭·스크롤 위치를 초기 상태로 되돌리고, 열어 둔 힌트 팝업을 정리한다.
    protected override void OnReturnToPool()
    {
        // 도감이 닫히면 도감이 띄운 힌트 팝업도 함께 정리한다. (닫히면서 구독 해제까지 이어진다)
        if (_openHintPopup != null)
            _openHintPopup.Close(immediate: true);

        UnsubscribeFromHintPopup();
        _inventory = null;
        _searchInput.SetTextWithoutNotify(string.Empty);
        SelectAge(null, refresh: false);

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }

    // 전체 탭과 Age별 탭, 그리고 아직 구현되지 않아 잠긴 탭을 순서대로 생성한다.
    private void BuildTabs()
    {
        AddTab("전체", null, locked: false);

        foreach (Age age in Enum.GetValues(typeof(Age)))
        {
            // 자연 자원은 시대 구분 대상이 아니므로 탭을 만들지 않는다 (전체 탭에서만 보인다).
            if (age == Age.nature) continue;
            AddTab(age.ToTabName(), age, locked: false);
        }

        foreach (string label in _lockedTabLabels)
            AddTab(label, null, locked: true);

        SelectAge(null, refresh: false);
    }

    // 탭 하나를 생성해 목록에 등록한다. locked면 클릭해도 필터가 바뀌지 않는다.
    private void AddTab(string label, Age? age, bool locked)
    {
        Action onClick = null;
        if (!locked) onClick = () => SelectAge(age, refresh: true);

        CodexAgeTabUI tab = Instantiate(_tabPrefab, _tabParent);
        tab.Setup(label, locked, onClick);

        _tabs.Add(tab);
        // 잠긴 탭은 선택 대상이 아니므로, 탭을 만들지 않는 Age.nature를 넣어 어떤 선택과도 매칭되지 않게 한다
        _tabAges.Add(locked ? Age.nature : age);
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

    // 검색어가 바뀌면 목록을 다시 추린다.
    private void HandleSearchChanged(string _) => Refresh();

    // 새 아이템이 도감에 등록되면 카드와 달성도를 다시 그린다.
    private void HandleDiscover(ItemData _) => Refresh();

    // 힌트가 공개되면 카드의 힌트 버튼 상태를 다시 그린다.
    private void HandleHintRevealed(ItemData _) => RefreshEntries();

    // 해당 아이템의 조합법 팝업을 연다. revealAll이면 힌트를 사지 않고 조합법 전체를 보여준다.
    // 이미 떠 있는 팝업이 있으면 다시 열지 않는다.
    private void OpenHintPopup(ItemData item, bool revealAll)
    {
        if (_openHintPopup != null) return;

        if (_hintPopupPrefab == null)
        {
            Debug.LogWarning($"ItemCodexUI({name}): 힌트 팝업 프리팹이 설정되지 않아 조합법을 열 수 없습니다.");
            return;
        }

        _openHintPopup = UIManager.Instance.OpenUI(_hintPopupPrefab, new CraftingHintData(item, _inventory, _codex, revealAll));
        _openHintPopup.OnFinishClose += HandleHintPopupClosed;
    }

    // 힌트 팝업이 닫히면 구독을 해제해 다시 열 수 있게 한다.
    private void HandleHintPopupClosed(IManagedUI ui) => UnsubscribeFromHintPopup();

    // 열어 둔 힌트 팝업의 구독을 해제하고 참조를 비운다.
    private void UnsubscribeFromHintPopup()
    {
        if (_openHintPopup == null) return;

        _openHintPopup.OnFinishClose -= HandleHintPopupClosed;
        _openHintPopup = null;
    }

    // 현재 필터·검색어에 맞는 카드 목록과 달성도를 모두 다시 그린다.
    private void Refresh()
    {
        RefreshEntries();
        RefreshProgress();
    }

    // 필터를 통과한 아이템만큼 카드를 켜서 채우고, 남는 카드는 끈다.
    private void RefreshEntries()
    {
        IReadOnlyList<ItemData> items = _itemDataList.Items;
        string keyword = _searchInput.text.Trim();
        int codexNumber = 0;
        int visibleCount = 0;

        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            // 도감에서 감춘 아이템은 번호도 차지하지 않도록 필터보다 먼저 건너뛴다.
            if (!item.ShowInCodex) continue;

            codexNumber++;
            if (!Matches(item, keyword)) continue;

            ItemCodexEntryUI entry = GetOrCreateEntry(visibleCount);
            entry.gameObject.SetActive(true);
            entry.Setup(item, codexNumber, IsDiscovered(item), MakeHintCallback(item));
            visibleCount++;
        }

        for (int i = visibleCount; i < _entries.Count; i++)
            _entries[i].gameObject.SetActive(false);

        _emptyText.gameObject.SetActive(visibleCount == 0);
    }

    // 조합법이 있는 아이템에만 상태 바 콜백을 만들어 준다. 조합법이 없으면 null을 반환해 평범한 상태 바로 그리게 한다.
    // 이미 획득한 아이템은 전체 공개 모드로, 아직이면 힌트 모드로 팝업을 연다.
    private Action MakeHintCallback(ItemData item)
    {
        if (!item.HasRecipe) return null;

        bool revealAll = IsDiscovered(item);
        return () => OpenHintPopup(item, revealAll);
    }

    // 아이템이 현재 시대 필터와 검색어를 모두 만족하는지 판정한다.
    private bool Matches(ItemData item, string keyword)
    {
        if (_selectedAge.HasValue && item.Age != _selectedAge.Value)
            return false;

        if (string.IsNullOrEmpty(keyword))
            return true;

        return item.Nmae != null &&
               item.Nmae.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // index번째 카드를 반환하고, 아직 없으면 새로 만들어 재사용 목록에 넣는다.
    private ItemCodexEntryUI GetOrCreateEntry(int index)
    {
        while (_entries.Count <= index)
            _entries.Add(Instantiate(_entryPrefab, _entryParent));

        _entries[index].transform.SetSiblingIndex(index);
        return _entries[index];
    }

    // 도감에 표시되는 아이템 대비 획득 수를 계산해 게이지 폭과 문구를 갱신한다. (필터와 무관하게 항상 전체 기준)
    private void RefreshProgress()
    {
        IReadOnlyList<ItemData> items = _itemDataList.Items;
        int total = 0;
        int discovered = 0;

        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (!item.ShowInCodex) continue;

            total++;
            if (IsDiscovered(item)) discovered++;
        }

        float ratio = total > 0 ? (float)discovered / total : 0f;

        _progressFill.anchorMin = new Vector2(0f, 0f);
        _progressFill.anchorMax = new Vector2(ratio, 1f);
        _progressFill.offsetMin = Vector2.zero;
        _progressFill.offsetMax = Vector2.zero;

        _progressText.text = $"수집 완료: {discovered}/{total} ({Mathf.RoundToInt(ratio * 100f)}%)";
    }

    // 씬의 ItemCodex 기준으로 아이템 획득 여부를 조회한다. 도감이 없으면 미획득으로 본다.
    private bool IsDiscovered(ItemData item) => _codex != null && _codex.IsDiscovered(item);
}
