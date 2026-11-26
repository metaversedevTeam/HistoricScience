using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 치트 관리 화면(Figma의 cheat-management-ui). 자원 무한처럼 언제든 껐다 켤 수 있는 치트와
// 모든 아이템·연구 해금처럼 되돌릴 수 없는 일회성 치트, 그리고 슬라이더로 고르는 이동속도 배율·채집 속도 배율·Y오프셋·시간·시간 진행 속도를 한 목록에서 다룬다.
// 일회성 치트는 실행하고 나면 스위치가 켜진 채로 잠겨 다시 눌리지 않으며,
// 잠금 여부는 UI가 따로 기억하지 않고 도감·연구 상태에서 그때그때 다시 읽는다. (창을 다시 열거나 저장을 불러와도 그대로 유지된다)
public class CheatManagementUI : OpenableUIBase<CheatMenuData>
{
    [Header("헤더")]
    [SerializeField] private Button _closeButton;

    [Header("치트 목록")]
    [SerializeField] private CheatToggleRowUI _rowPrefab;
    [SerializeField] private RectTransform _rowContainer;
    [SerializeField] private CheatEntry[] _entries;

    [Header("이동속도 배율")]
    [SerializeField] private CheatSliderRowUI _sliderRowPrefab;
    // 슬라이더 줄들을 세로로 쌓는 오른쪽 열. 스위치 줄들(_rowContainer)과 두 열로 나란히 놓인다.
    [SerializeField] private RectTransform _sliderRowContainer;
    [SerializeField] private string _moveSpeedTitle = "이동속도 배율 (Move Speed)";
    [SerializeField, TextArea(1, 3)] private string _moveSpeedDescription = "모든 시민의 이동속도를 1배에서 10배까지 높입니다.";

    [Header("채집 속도 배율")]
    [SerializeField] private string _gatherSpeedTitle = "채집 속도 배율 (Gather Speed)";
    [SerializeField, TextArea(1, 3)] private string _gatherSpeedDescription = "모든 시민의 채집 속도를 1배에서 10배까지 높입니다.";

    [Header("Y오프셋")]
    [SerializeField] private string _heightOffsetTitle = "Y오프셋 (Height Offset)";
    [SerializeField, TextArea(1, 3)] private string _heightOffsetDescription = "시민을 땅보다 0~500m 위에 띄웁니다. 500이면 높이 Y500에 고정됩니다.";

    [Header("시간")]
    [SerializeField] private string _timeOfDayTitle = "시간 (Time of Day)";
    [SerializeField, TextArea(1, 3)] private string _timeOfDayDescription = "하늘과 햇빛의 시간을 0시부터 24시까지 바꿉니다. 바꾼 시간부터 다시 흘러갑니다.";

    [Header("시간 진행 속도")]
    [SerializeField] private string _timeSpeedTitle = "시간 진행 속도 (Time Speed)";
    [SerializeField, TextArea(1, 3)] private string _timeSpeedDescription = "하루가 흘러가는 속도를 1배에서 60배까지 높입니다.";

    // 이동속도 배율 슬라이더 값의 간격
    private const float k_MoveSpeedStep = 0.5f;

    // 채집 속도 배율 슬라이더 값의 간격
    private const float k_GatherSpeedStep = 0.5f;

    // Y오프셋 슬라이더 값의 간격(m)
    private const float k_HeightOffsetStep = 1f;

    // 하루의 시간 수. 시간 슬라이더는 0~24시로 표시하고 하늘에는 0~1로 바꿔 넘긴다.
    private const float k_HoursPerDay = 24f;

    // 시간 슬라이더 값의 간격(시간). 0.25면 15분 단위다.
    private const float k_TimeOfDayStepHours = 0.25f;

    // 시간 진행 속도 배율 슬라이더 값의 간격
    private const float k_TimeSpeedStep = 1f;

    private ResourceInventory _inventory;

    // 스위치 줄들 아래에 만들어 두는 이동속도 배율 슬라이더 줄
    private CheatSliderRowUI _moveSpeedRow;

    // 이동속도 배율 줄 아래에 만들어 두는 채집 속도 배율 슬라이더 줄
    private CheatSliderRowUI _gatherSpeedRow;

    // 채집 속도 배율 줄 아래에 만들어 두는 Y오프셋 슬라이더 줄
    private CheatSliderRowUI _heightOffsetRow;

    // Y오프셋 줄 아래에 만들어 두는 시간 슬라이더 줄
    private CheatSliderRowUI _timeOfDayRow;

    // 시간 줄 아래에 만들어 두는 시간 진행 속도 배율 슬라이더 줄
    private CheatSliderRowUI _timeSpeedRow;

    // 시간 슬라이더가 조절하는 현재 씬의 하늘 시간 컴포넌트. 씬에 없으면 시간 줄을 숨긴다.
    private SkyboxTimeOfDay _skyTime;
    private ItemCodex _codex;
    private ResearchManager _research;

    // _entries와 같은 순서로 만들어 둔 스위치 줄들
    private readonly List<CheatToggleRowUI> _rows = new();

    private void Awake()
    {
        _closeButton.onClick.AddListener(HandleCloseButtonClick);
        BuildRows();
    }

    // 치트 목록 정의대로 스위치 줄을 한 번 만들어 둔다. (풀에서 다시 열릴 때는 상태만 갱신한다)
    private void BuildRows()
    {
        if (_entries == null || _rowPrefab == null || _rowContainer == null)
        {
            Debug.LogWarning($"CheatManagementUI({name}): 치트 목록을 만들 준비가 되지 않아 빈 화면으로 열립니다.", this);
            return;
        }

        foreach (CheatEntry entry in _entries)
        {
            CheatToggleRowUI row = Instantiate(_rowPrefab, _rowContainer);
            CheatKind kind = entry.Kind;

            row.Setup(entry.Title, entry.Description, () => HandleRowClick(kind));
            _rows.Add(row);
        }

        BuildSliderRows();
    }

    // 스위치 줄들 옆 오른쪽 열에 슬라이더 치트 줄들을 차례로 만든다.
    private void BuildSliderRows()
    {
        if (_sliderRowPrefab == null || _sliderRowContainer == null)
        {
            Debug.LogWarning($"CheatManagementUI({name}): 슬라이더 줄 프리팹이나 오른쪽 열이 없어 슬라이더 치트를 표시하지 않습니다.", this);
            return;
        }

        _moveSpeedRow = Instantiate(_sliderRowPrefab, _sliderRowContainer);
        _moveSpeedRow.Setup(_moveSpeedTitle, _moveSpeedDescription,
            MoveSpeedCheat.MinMultiplier, MoveSpeedCheat.MaxMultiplier, k_MoveSpeedStep,
            FormatMoveSpeed, HandleMoveSpeedChanged);

        _gatherSpeedRow = Instantiate(_sliderRowPrefab, _sliderRowContainer);
        _gatherSpeedRow.Setup(_gatherSpeedTitle, _gatherSpeedDescription,
            GatherSpeedCheat.MinMultiplier, GatherSpeedCheat.MaxMultiplier, k_GatherSpeedStep,
            FormatMoveSpeed, HandleGatherSpeedChanged);

        _heightOffsetRow = Instantiate(_sliderRowPrefab, _sliderRowContainer);
        _heightOffsetRow.Setup(_heightOffsetTitle, _heightOffsetDescription,
            HeightOffsetCheat.MinOffset, HeightOffsetCheat.MaxOffset, k_HeightOffsetStep,
            FormatHeightOffset, HandleHeightOffsetChanged);

        _timeOfDayRow = Instantiate(_sliderRowPrefab, _sliderRowContainer);
        _timeOfDayRow.Setup(_timeOfDayTitle, _timeOfDayDescription,
            0f, k_HoursPerDay, k_TimeOfDayStepHours,
            FormatTimeOfDay, HandleTimeOfDayChanged);

        _timeSpeedRow = Instantiate(_sliderRowPrefab, _sliderRowContainer);
        _timeSpeedRow.Setup(_timeSpeedTitle, _timeSpeedDescription,
            TimeSpeedCheat.MinMultiplier, TimeSpeedCheat.MaxMultiplier, k_TimeSpeedStep,
            FormatTimeSpeed, HandleTimeSpeedChanged);
    }

    // 이동속도 배율을 "x2.5" 형태로 표시한다.
    private static string FormatMoveSpeed(float multiplier) => $"x{multiplier:0.#}";

    // Y오프셋을 "+120m" 형태로, 최댓값이면 고정 높이 "Y500"으로 표시한다.
    private static string FormatHeightOffset(float offset) =>
        offset >= HeightOffsetCheat.MaxOffset ? $"Y{HeightOffsetCheat.MaxOffset:0}" : $"+{offset:0}m";

    // 슬라이더로 고른 배율을 이동속도 치트에 적용한다.
    private void HandleMoveSpeedChanged(float multiplier) => MoveSpeedCheat.Multiplier = multiplier;

    // 슬라이더로 고른 배율을 채집 속도 치트에 적용한다.
    private void HandleGatherSpeedChanged(float multiplier) => GatherSpeedCheat.Multiplier = multiplier;

    // 슬라이더로 고른 높이를 Y오프셋 치트에 적용한다.
    private void HandleHeightOffsetChanged(float offset) => HeightOffsetCheat.Offset = offset;

    // 시간(0~24)을 "07:30" 형태로 표시한다.
    private static string FormatTimeOfDay(float hours)
    {
        int totalMinutes = Mathf.RoundToInt(hours * 60f);
        return $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
    }

    // 시간 진행 속도 배율을 "x12" 형태로 표시한다.
    private static string FormatTimeSpeed(float multiplier) => $"x{multiplier:0}";

    // 슬라이더로 고른 배율을 시간 진행 속도 치트에 적용한다.
    private void HandleTimeSpeedChanged(float multiplier) => TimeSpeedCheat.Multiplier = multiplier;

    // 슬라이더로 고른 시간(0~24)을 하늘 시간(0~1)으로 바꿔 적용한다.
    private void HandleTimeOfDayChanged(float hours)
    {
        if (_skyTime != null)
            _skyTime.SetTimeOfDay(hours / k_HoursPerDay);
    }

    // 치트를 적용할 대상을 주입받고 목록의 현재 상태를 반영한다.
    protected override void ApplyData(CheatMenuData data)
    {
        _inventory = data.Inventory;
        _codex = data.Codex;
        _research = ResearchManager.Instance;
        _skyTime = FindFirstObjectByType<SkyboxTimeOfDay>();

        RefreshRows();
    }

    // 모든 줄의 스위치 표시와 잠금 여부를 현재 게임 상태에 맞춰 다시 그린다.
    private void RefreshRows()
    {
        for (int i = 0; i < _rows.Count && i < _entries.Length; i++)
        {
            CheatKind kind = _entries[i].Kind;

            _rows[i].SetOn(IsCheatOn(kind));
            _rows[i].SetInteractable(CanUseCheat(kind));
        }

        if (_moveSpeedRow != null)
            _moveSpeedRow.SetValue(MoveSpeedCheat.Multiplier);

        if (_gatherSpeedRow != null)
            _gatherSpeedRow.SetValue(GatherSpeedCheat.Multiplier);

        if (_heightOffsetRow != null)
            _heightOffsetRow.SetValue(HeightOffsetCheat.Offset);

        if (_timeOfDayRow != null)
        {
            _timeOfDayRow.gameObject.SetActive(_skyTime != null);
            if (_skyTime != null)
                _timeOfDayRow.SetValue(_skyTime.TimeOfDay * k_HoursPerDay);
        }

        // 흘려보낼 하늘이 없는 씬에서는 시간 진행 속도 줄도 숨긴다.
        if (_timeSpeedRow != null)
        {
            _timeSpeedRow.gameObject.SetActive(_skyTime != null);
            _timeSpeedRow.SetValue(TimeSpeedCheat.Multiplier);
        }
    }

    // 해당 치트가 이미 적용된 상태인지 반환한다. 일회성 치트는 전부 해금된 상태를 켜짐으로 본다.
    private bool IsCheatOn(CheatKind kind) => kind switch
    {
        CheatKind.InfiniteResources => _inventory != null && _inventory.IsCheatModeEnabled,
        CheatKind.UnlockAllItems => _codex != null && _codex.IsEverythingUnlocked,
        CheatKind.UnlockAllResearch => _research != null && _research.IsEverythingCompleted,
        CheatKind.IgnoreWalls => WallIgnoreCheat.IsEnabled,
        _ => false,
    };

    // 해당 치트를 지금 누를 수 있는지 반환한다. 이미 다 쓴 일회성 치트는 누를 수 없다.
    private bool CanUseCheat(CheatKind kind) => kind switch
    {
        CheatKind.InfiniteResources => _inventory != null,
        CheatKind.UnlockAllItems => _codex != null && !_codex.IsEverythingUnlocked,
        CheatKind.UnlockAllResearch => _research != null && !_research.IsEverythingCompleted,
        CheatKind.IgnoreWalls => true,
        _ => false,
    };

    // 스위치를 눌렀을 때 해당 치트를 적용하고 목록 표시를 갱신한다.
    private void HandleRowClick(CheatKind kind)
    {
        switch (kind)
        {
            case CheatKind.InfiniteResources:
                if (_inventory != null)
                    _inventory.IsCheatModeEnabled = !_inventory.IsCheatModeEnabled;
                break;

            case CheatKind.UnlockAllItems:
                if (_codex != null)
                    _codex.UnlockAllItems();
                break;

            case CheatKind.UnlockAllResearch:
                if (_research != null)
                    _research.UnlockAllResearch();
                break;

            case CheatKind.IgnoreWalls:
                WallIgnoreCheat.IsEnabled = !WallIgnoreCheat.IsEnabled;
                break;
        }

        AudioManager.PlayConfirm();
        RefreshRows();
    }

    // 다음에 열릴 때 이전 씬의 참조가 남지 않도록 비운다.
    protected override void OnReturnToPool()
    {
        _inventory = null;
        _codex = null;
        _research = null;
        _skyTime = null;
    }

    // 닫기 버튼을 누르면 UI를 닫는다. (ESC로 닫는 UIManager 경로와 동일한 동작)
    private void HandleCloseButtonClick() => Close();

    // 목록에 놓일 치트 한 줄의 정의 — 어떤 치트인지와 화면에 보여 줄 문구
    [Serializable]
    private class CheatEntry
    {
        public CheatKind Kind => _kind;

        public string Title => _title;

        public string Description => _description;

        [SerializeField] private CheatKind _kind;
        [SerializeField] private string _title;
        [SerializeField, TextArea(1, 3)] private string _description;
    }
}

// 치트 관리 화면에 전달되는 페이로드 — 치트를 적용할 인벤토리와 도감 (연구는 ResearchManager.Instance로 찾는다)
public readonly struct CheatMenuData
{
    public readonly ResourceInventory Inventory;
    public readonly ItemCodex Codex;

    // 치트 대상 인벤토리와 도감으로 페이로드를 구성한다.
    public CheatMenuData(ResourceInventory inventory, ItemCodex codex)
    {
        Inventory = inventory;
        Codex = codex;
    }
}
