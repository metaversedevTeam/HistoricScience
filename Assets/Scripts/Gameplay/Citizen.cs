using System.Collections.Generic;
using UnityEngine;

// 시민 유닛을 나타내며 선택된 상태에서 우클릭한 대상이 IGatherable이면 채집하고, 아니면 대상 추적 또는 위치 이동을 명령하는 컴포넌트. 일터에 등록되어 일할 수도 있다.
public class Citizen : MonoBehaviour, ICommandable, ISavable, IWorker
{
    [SerializeField] private Sprite _gatherCommandIcon;
    [SerializeField] private Sprite _buildCommandIcon;
    // 건물 선택 UI 프리팹
    [SerializeField] private BuildingSelectUI _buildingSelectUiPrefab;
    // 건물 선택 UI에 나열할, IBuildable을 구현한 건물 프리팹 목록
    [SerializeField] private List<GameObject> _buildablePrefabs;
    // 건물 선택 후 위치 지정 모드를 담당하는 컨트롤러 프리팹
    [SerializeField] private BuildingPlacementController _buildingPlacementControllerPrefab;
    // 저장/복원 기능을 제공하는 컴포지션. PrefabId는 인스펙터에서 설정한다.
    [SerializeField] private SavableHandler _savable = new();

    private SelectableObject _selectable;
    private IMover _mover;
    private Gatherer _gatherer;
    private PlayerManager _selectedBy;
    private IReadOnlyList<CommandData> _commands;

    private PlayerManager _pendingGatherPlayer;
    private IGatherable _gatherTarget;
    private ResourceInventory _gatherInventory;

    // 현재 소속된 일터. 등록되지 않았으면 null이다.
    private WorkPlace _currentWorkPlace;

    // 열려 있는 건물 선택 UI. 열려 있지 않으면 null이다.
    private BuildingSelectUI _openBuildingSelectUI;

    // SelectableObject, IMover, Gatherer 컴포넌트를 캐싱하고 명령 목록을 생성
    private void Awake()
    {
        _selectable = GetComponent<SelectableObject>();
        _mover = GetComponent<IMover>();
        _gatherer = GetComponent<Gatherer>();
        _commands = new List<CommandData>
        {
            new CommandData("채집", _gatherCommandIcon, BeginGatherTargeting),
            new CommandData("건물 짓기", _buildCommandIcon, OpenBuildingSelectUI),
        };
    }

    // 자신의 선택 이벤트를 구독
    private void OnEnable()
    {
        _selectable.OnSelect += HandleSelect;
    }

    // 자신의 선택 이벤트와 PlayerManager 구독을 해제하고 채집 타겟 지정 대기와 건물 선택 UI 구독을 취소
    private void OnDisable()
    {
        _selectable.OnSelect -= HandleSelect;
        UnsubscribeFromPlayer();
        CancelGatherTargeting();
        UnsubscribeFromBuildingSelectUI();
    }

    // 채집 대상이 지정되어 있으면 매 프레임 채집을 시도
    private void Update()
    {
        HandleGathering();
    }

    // 이 오브젝트가 제공하는 명령 목록을 반환한다.
    public IReadOnlyList<CommandData> GetCommands()
    {
        return _commands;
    }

    // 선택되었을 때 해당 PlayerManager의 우클릭/선택해제 이벤트를 구독
    private void HandleSelect(PlayerManager playerManager)
    {
        UnsubscribeFromPlayer();

        _selectedBy = playerManager;
        _selectedBy.OnMouseRightClick += HandleRightClick;
        _selectedBy.OnDeselected += HandleDeselected;
    }

    // 선택 해제 시 PlayerManager 구독을 해제
    private void HandleDeselected()
    {
        UnsubscribeFromPlayer();
    }

    // 우클릭한 대상이 IGatherable이면 채집을 시작하고, 아니면 대상을 추적하거나 클릭한 위치로 이동하며 진행 중이던 채집과 채집 타겟 지정 대기를 취소
    private void HandleRightClick(Vector2 pos, ClickableObject clickable)
    {
        CancelGatherTargeting();
        CancelGathering();

        if (clickable == null)
        {
            _mover.Move(pos);
            return;
        }

        var gatherable = clickable.GetComponent<IGatherable>();
        if (gatherable != null)
            BeginGathering(gatherable, _selectedBy.ResourceInventory, clickable.transform);
        else
            _mover.Move(clickable.transform);
    }

    // 구독 중인 PlayerManager 이벤트를 해제하고 참조를 비운다.
    private void UnsubscribeFromPlayer()
    {
        if (_selectedBy == null) return;

        _selectedBy.OnMouseRightClick -= HandleRightClick;
        _selectedBy.OnDeselected -= HandleDeselected;
        _selectedBy = null;
    }

    // 채집 명령을 실행해 다음 좌클릭 대상을 채집 대상으로 지정하는 모드로 진입한다.
    private void BeginGatherTargeting()
    {
        if (_selectedBy == null) return;

        CancelGatherTargeting();

        _pendingGatherPlayer = _selectedBy;
        _pendingGatherPlayer.OnMouseLeftClick += HandleGatherTargetClick;
    }

    // 대기 중이던 채집 타겟 지정 모드에서 좌클릭 결과를 받아 대상이 유효하면 채집을 시작한다.
    private void HandleGatherTargetClick(Vector2 pos, ClickableObject clickable)
    {
        var chosenPlayer = _pendingGatherPlayer;
        CancelGatherTargeting();

        var gatherable = clickable != null ? clickable.GetComponent<IGatherable>() : null;
        if (gatherable == null) return;

        BeginGathering(gatherable, chosenPlayer.ResourceInventory, clickable.transform);
    }

    // 지정한 대상을 채집 대상으로 설정하고 대상에게 이동을 시작한다.
    private void BeginGathering(IGatherable gatherable, ResourceInventory inventory, Transform targetTransform)
    {
        _gatherTarget = gatherable;
        _gatherInventory = inventory;
        _mover.Move(targetTransform);
    }

    // 채집 타겟 지정 대기 상태를 취소하고 구독을 해제한다.
    private void CancelGatherTargeting()
    {
        if (_pendingGatherPlayer == null) return;

        _pendingGatherPlayer.OnMouseLeftClick -= HandleGatherTargetClick;
        _pendingGatherPlayer = null;
    }

    // 채집 대상이 유효하면 Gatherer로 채집을 시도하고, 대상이 파괴되었으면 채집을 취소한다.
    private void HandleGathering()
    {
        if (_gatherTarget == null) return;

        if (_gatherTarget is Component component && component == null)
        {
            CancelGathering();
            return;
        }

        _gatherer.TryGather(_gatherTarget, _gatherInventory);
    }

    // 진행 중인 채집 대상과 캐싱된 인벤토리 참조를 비운다.
    private void CancelGathering()
    {
        _gatherTarget = null;
        _gatherInventory = null;
    }

    // 건물 짓기 명령을 실행해 건물 선택 UI를 열고 선택·닫기 결과를 구독한다.
    private void OpenBuildingSelectUI()
    {
        if (_selectedBy == null) return;

        UnsubscribeFromBuildingSelectUI();

        _openBuildingSelectUI = UIManager.Instance.OpenUI(_buildingSelectUiPrefab, CollectBuildables());
        _openBuildingSelectUI.OnBuildingSelected += HandleBuildingSelected;
        _openBuildingSelectUI.OnFinishClose += HandleBuildingSelectUIClosed;
    }

    // 인스펙터에 등록된 건물 프리팹 중 IBuildable을 구현한 것만 골라 반환한다.
    private IReadOnlyList<IBuildable> CollectBuildables()
    {
        var buildables = new List<IBuildable>();
        foreach (var prefab in _buildablePrefabs)
        {
            if (prefab != null && prefab.TryGetComponent(out IBuildable buildable))
                buildables.Add(buildable);
        }
        return buildables;
    }

    // 건물 선택 UI에서 건물이 선택되면 선택 UI를 닫고 위치 지정 모드를 시작한다.
    private void HandleBuildingSelected(IBuildable buildable)
    {
        if (_selectedBy == null) return;

        _openBuildingSelectUI.Close();

        var placementController = Instantiate(_buildingPlacementControllerPrefab);
        placementController.BeginPlacement(buildable, (buildable as Component)?.gameObject, _selectedBy);
    }

    // 건물 선택 UI가 닫히면 구독을 해제한다.
    private void HandleBuildingSelectUIClosed(IManagedUI ui)
    {
        UnsubscribeFromBuildingSelectUI();
    }

    // 열려 있는 건물 선택 UI의 이벤트 구독을 해제하고 참조를 비운다.
    private void UnsubscribeFromBuildingSelectUI()
    {
        if (_openBuildingSelectUI == null) return;

        _openBuildingSelectUI.OnBuildingSelected -= HandleBuildingSelected;
        _openBuildingSelectUI.OnFinishClose -= HandleBuildingSelectUIClosed;
        _openBuildingSelectUI = null;
    }

    public WorkPlace CurrentWorkPlace => _currentWorkPlace;

    // 일터에 등록되기 직전 호출. 진행 중인 이동·채집과 플레이어 구독을 정리해, 곧바로 캡처될 상태에 남은 명령이 없게 한다.
    public void OnEnterWorkPlace(WorkPlace workPlace)
    {
        _currentWorkPlace = workPlace;

        CancelGatherTargeting();
        CancelGathering();
        UnsubscribeFromPlayer();
        _mover.Stop();
    }

    // 일터에서 해제되어 새 인스턴스로 복원된 직후 호출. 소속을 비워 다시 다른 일터에 등록될 수 있게 한다.
    public void OnExitWorkPlace()
    {
        _currentWorkPlace = null;
    }

    public string PrefabId => _savable.PrefabId;

    // 현재 상태를 JSON 문자열로 캡처한다.
    public string CaptureJson() => _savable.CaptureJson(transform);

    // JSON 문자열로 상태를 복원한다.
    public void ApplyJson(string json) => _savable.ApplyJson(transform, json);
}
