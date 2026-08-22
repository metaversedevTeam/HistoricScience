using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 선택되었을 때 아이템 도감 UI를 여는 명령을 제공하는 근거지 건물 오브젝트
public class HomeBase : MonoBehaviour, ICommandable, ISavable, IBuildable
{
    [SerializeField] private ItemCodexUI _itemCodexUiPrefab;
    [SerializeField] private Sprite _codexButtonIcon;
    // 철거 명령 버튼에 표시할 아이콘
    [SerializeField] private Sprite _demolishButtonIcon;
    // 저장/복원 기능을 제공하는 컴포지션. PrefabId는 인스펙터에서 설정한다.
    [SerializeField] private SavableHandler _savable = new();

    // 건물 선택 UI에 표시할 건물 이름
    [SerializeField] private string _buildingName;
    // 건물 선택 UI의 상세 패널에 표시할 건물 설명
    [SerializeField, TextArea(2, 4)] private string _buildingDescription;
    // 건설에 걸리는 시간(초)
    [SerializeField] private float _buildTime;
    // 건물 선택 UI에 표시할 아이콘
    [SerializeField] private Sprite _buildingIcon;
    // 배치 미리보기 홀로그램으로 소환할 모델 오브젝트
    [SerializeField] private GameObject _buildingModel;
    // 인스펙터에서 지정하는 건설 비용 목록
    [SerializeField] private List<BuildCostEntry> _buildCost = new();

    private SelectableObject _selectable;
    private PlayerManager _selectedBy;
    private IReadOnlyList<CommandData> _commands;
    private IReadOnlyDictionary<ResourceData, int> _buildCostLookup;

    // 이 근거지가 열어 둔 도감 UI. 닫히면 다시 null이 되어 중복해서 열리지 않게 한다.
    private ItemCodexUI _openCodexUI;

    public string BuildingName => _buildingName;
    public string Description => _buildingDescription;
    public float BuildTime => _buildTime;
    public Sprite Icon => _buildingIcon;
    public GameObject BuildingModel => _buildingModel;
    // 건물 선택 UI 등은 씬에 배치되지 않은 프리팹 에셋의 컴포넌트를 그대로 참조해 Awake가 실행되지 않으므로, 최초 접근 시 지연 계산한다.
    public IReadOnlyDictionary<ResourceData, int> BuildCost => _buildCostLookup ??= BuildCostLookup();

    // SelectableObject 컴포넌트를 캐싱하고 명령 목록을 생성
    private void Awake()
    {
        _selectable = GetComponent<SelectableObject>();
        _commands = new List<CommandData>
        {
            new CommandData("도감 열기", _codexButtonIcon, OpenItemCodexUI),
            new CommandData("철거", _demolishButtonIcon, ExecuteDemolish),
        };
    }

    // 자신의 선택 이벤트를 구독
    private void OnEnable()
    {
        _selectable.OnSelect += HandleSelect;
    }

    // 자신의 선택 이벤트와 PlayerManager 구독, 열어 둔 도감 UI 구독을 모두 해제
    private void OnDisable()
    {
        _selectable.OnSelect -= HandleSelect;
        UnsubscribeFromPlayer();
        UnsubscribeFromCodexUI();
    }

    // 선택되었을 때 해당 PlayerManager를 저장하고 선택해제 이벤트를 구독
    private void HandleSelect(PlayerManager playerManager)
    {
        UnsubscribeFromPlayer();

        _selectedBy = playerManager;
        _selectedBy.OnDeselected += HandleDeselected;
    }

    // 선택 해제 시 PlayerManager 구독을 해제
    private void HandleDeselected()
    {
        UnsubscribeFromPlayer();
    }

    // 구독 중인 PlayerManager 이벤트를 해제하고 참조를 비운다.
    private void UnsubscribeFromPlayer()
    {
        if (_selectedBy == null) return;

        _selectedBy.OnDeselected -= HandleDeselected;
        _selectedBy = null;
    }

    // 인스펙터에서 지정한 건설 비용 목록을 자원별 조회 테이블로 변환한다.
    private Dictionary<ResourceData, int> BuildCostLookup()
    {
        var lookup = new Dictionary<ResourceData, int>();
        foreach (var entry in _buildCost)
        {
            if (entry.Resource != null)
                lookup[entry.Resource] = entry.Count;
        }
        return lookup;
    }

    // 이 오브젝트가 제공하는 명령 목록을 반환한다.
    public IReadOnlyList<CommandData> GetCommands()
    {
        return _commands;
    }

    // 아이템 도감 UI를 조합법 힌트 비용을 치를 인벤토리와 함께 연다. 획득 여부 자체는 씬의 ItemCodex에서 도감이 직접 읽는다.
    // 이미 이 근거지가 연 도감이 떠 있으면 창이 겹쳐 쌓이지 않도록 다시 열지 않는다.
    private void OpenItemCodexUI()
    {
        if (_openCodexUI != null) return;
        if (_selectedBy == null) return;

        if (_itemCodexUiPrefab == null)
        {
            Debug.LogWarning($"HomeBase({name}): 도감 UI 프리팹이 설정되지 않아 도감을 열 수 없습니다.");
            return;
        }

        _openCodexUI = UIManager.Instance.OpenUI(_itemCodexUiPrefab, _selectedBy.ResourceInventory);
        _openCodexUI.OnFinishClose += HandleCodexUIClosed;
    }

    // 도감 UI가 닫히면 구독을 해제해 다음 명령에 다시 열 수 있게 한다.
    private void HandleCodexUIClosed(IManagedUI ui)
    {
        UnsubscribeFromCodexUI();
    }

    // 열려 있는 도감 UI의 이벤트 구독을 해제하고 참조를 비운다.
    private void UnsubscribeFromCodexUI()
    {
        if (_openCodexUI == null) return;

        _openCodexUI.OnFinishClose -= HandleCodexUIClosed;
        _openCodexUI = null;
    }

    // 선택을 해제하고 건물을 철거한다.
    private void ExecuteDemolish()
    {
        if (_selectedBy != null)
            _selectedBy.DeselectExternally();

        Destroy(gameObject);
    }

    public string PrefabId => _savable.PrefabId;

    // 현재 상태를 JSON 문자열로 캡처한다.
    public string CaptureJson() => _savable.CaptureJson(transform);

    // JSON 문자열로 상태를 복원하고 다음 프레임에 지면 높이로 맞춘다.
    public void ApplyJson(string json)
    {
        _savable.ApplyJson(transform, json);
        StartCoroutine(HandleSnapToGroundNextFrame());
    }

    // 한 프레임 대기한 뒤 GroundSnapper가 있으면 지면 높이로 맞춘다.
    private IEnumerator HandleSnapToGroundNextFrame()
    {
        yield return null;
        if (TryGetComponent(out GroundSnapper snapper))
            snapper.SnapToGround();
    }
}
