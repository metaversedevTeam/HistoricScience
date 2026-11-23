using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 시민이 상호작용하면 연구 목록 UI를 여는 명령을 제공하는 고인돌 건물 오브젝트
public class Dolmen : MonoBehaviour, ICommandable, ISavable, IBuildable
{
    [SerializeField] private ResearchUI _researchUiPrefab;
    // 이 건물에서 볼 수 있는 연구 목록
    [SerializeField] private ResearchDataList _researchDataList;
    [SerializeField] private Sprite _researchButtonIcon;
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

    private IReadOnlyList<CommandData> _commands;
    private IReadOnlyDictionary<ResourceData, int> _buildCostLookup;

    // 이 고인돌이 열어 둔 연구 목록 UI. 닫히면 다시 null이 되어 중복해서 열리지 않게 한다.
    private ResearchUI _openResearchUI;

    public string BuildingName => _buildingName;
    public string Description => _buildingDescription;
    public float BuildTime => _buildTime;
    public Sprite Icon => _buildingIcon;
    public GameObject BuildingModel => _buildingModel;
    // 건물 선택 UI 등은 씬에 배치되지 않은 프리팹 에셋의 컴포넌트를 그대로 참조해 Awake가 실행되지 않으므로, 최초 접근 시 지연 계산한다.
    public IReadOnlyDictionary<ResourceData, int> BuildCost => _buildCostLookup ??= BuildCostLookup();

    // 명령 목록을 생성
    private void Awake()
    {
        _commands = new List<CommandData>
        {
            new CommandData("연구 목록 열기", _researchButtonIcon, OpenResearchUI),
            new CommandData("철거", _demolishButtonIcon, _ => ExecuteDemolish()),
        };
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

    // 연구 목록 UI를 연구 목록과 명령을 내린 플레이어의 자원 인벤토리와 함께 연다.
    // 이미 이 고인돌이 연 UI가 떠 있으면 창이 겹쳐 쌓이지 않도록 다시 열지 않는다.
    private void OpenResearchUI(PlayerManager player)
    {
        if (player == null || _openResearchUI != null) return;

        if (_researchUiPrefab == null)
        {
            Debug.LogWarning($"Dolmen({name}): 연구 UI 프리팹이 설정되지 않아 연구 목록을 열 수 없습니다.");
            return;
        }

        var data = new ResearchListData(_researchDataList, player.ResourceInventory, _buildingName);
        _openResearchUI = UIManager.Instance.OpenUI(_researchUiPrefab, data);
        _openResearchUI.OnFinishClose += HandleResearchUIClosed;
    }

    // 연구 목록 UI가 닫히면 구독을 해제해 다음 명령에 다시 열 수 있게 한다.
    private void HandleResearchUIClosed(IManagedUI ui)
    {
        UnsubscribeFromResearchUI();
    }

    // 열려 있는 연구 목록 UI의 이벤트 구독을 해제하고 참조를 비운다.
    private void UnsubscribeFromResearchUI()
    {
        if (_openResearchUI == null) return;

        _openResearchUI.OnFinishClose -= HandleResearchUIClosed;
        _openResearchUI = null;
    }

    // 건물을 철거한다.
    private void ExecuteDemolish()
    {
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
