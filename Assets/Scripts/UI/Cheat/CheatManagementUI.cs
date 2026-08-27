using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 치트 관리 화면(Figma의 cheat-management-ui). 자원 무한처럼 언제든 껐다 켤 수 있는 치트와
// 모든 아이템·연구 해금처럼 되돌릴 수 없는 일회성 치트를 한 목록에서 다룬다.
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

    private ResourceInventory _inventory;
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
    }

    // 치트를 적용할 대상을 주입받고 목록의 현재 상태를 반영한다.
    protected override void ApplyData(CheatMenuData data)
    {
        _inventory = data.Inventory;
        _codex = data.Codex;
        _research = ResearchManager.Instance;

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
    }

    // 해당 치트가 이미 적용된 상태인지 반환한다. 일회성 치트는 전부 해금된 상태를 켜짐으로 본다.
    private bool IsCheatOn(CheatKind kind) => kind switch
    {
        CheatKind.InfiniteResources => _inventory != null && _inventory.IsCheatModeEnabled,
        CheatKind.UnlockAllItems => _codex != null && _codex.IsEverythingUnlocked,
        CheatKind.UnlockAllResearch => _research != null && _research.IsEverythingCompleted,
        _ => false,
    };

    // 해당 치트를 지금 누를 수 있는지 반환한다. 이미 다 쓴 일회성 치트는 누를 수 없다.
    private bool CanUseCheat(CheatKind kind) => kind switch
    {
        CheatKind.InfiniteResources => _inventory != null,
        CheatKind.UnlockAllItems => _codex != null && !_codex.IsEverythingUnlocked,
        CheatKind.UnlockAllResearch => _research != null && !_research.IsEverythingCompleted,
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
        }

        RefreshRows();
    }

    // 다음에 열릴 때 이전 씬의 참조가 남지 않도록 비운다.
    protected override void OnReturnToPool()
    {
        _inventory = null;
        _codex = null;
        _research = null;
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
