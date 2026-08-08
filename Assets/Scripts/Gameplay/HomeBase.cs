using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 선택되었을 때 아이템 도감 UI를 여는 명령을 제공하는 근거지 건물 오브젝트
public class HomeBase : MonoBehaviour, ICommandable, ISavable, IBuildable
{
    [SerializeField] private ItemCodexUI _itemCodexUiPrefab;
    [SerializeField] private Sprite _codexButtonIcon;
    // 저장/복원 기능을 제공하는 컴포지션. PrefabId는 인스펙터에서 설정한다.
    [SerializeField] private SavableHandler _savable = new();

    // 건물 선택 UI에 표시할 아이콘
    [SerializeField] private Sprite _buildingIcon;
    // 배치 미리보기 홀로그램으로 소환할 모델 오브젝트
    [SerializeField] private GameObject _buildingModel;
    // 인스펙터에서 지정하는 건설 비용 목록
    [SerializeField] private List<BuildCostEntry> _buildCost = new();

    private IReadOnlyList<CommandData> _commands;
    private IReadOnlyDictionary<ResourceData, int> _buildCostLookup;

    // 이 근거지가 열어 둔 도감 UI. 닫히면 다시 null이 되어 중복해서 열리지 않게 한다.
    private ItemCodexUI _openCodexUI;

    public Sprite Icon => _buildingIcon;
    public GameObject BuildingModel => _buildingModel;
    // 건물 선택 UI 등은 씬에 배치되지 않은 프리팹 에셋의 컴포넌트를 그대로 참조해 Awake가 실행되지 않으므로, 최초 접근 시 지연 계산한다.
    public IReadOnlyDictionary<ResourceData, int> BuildCost => _buildCostLookup ??= BuildCostLookup();

    // 명령 목록을 생성
    private void Awake()
    {
        _commands = new List<CommandData> { new CommandData("도감 열기", _codexButtonIcon, OpenItemCodexUI) };
    }

    // 파괴·비활성화 시 열어 둔 도감 UI의 구독을 해제한다.
    private void OnDisable()
    {
        UnsubscribeFromCodexUI();
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

    // 아이템 도감 UI를 연다. 도감은 플레이어별 데이터가 아니라 씬의 ItemCodex를 직접 읽으므로 넘겨줄 페이로드가 없다.
    // 이미 이 근거지가 연 도감이 떠 있으면 창이 겹쳐 쌓이지 않도록 다시 열지 않는다.
    private void OpenItemCodexUI()
    {
        if (_openCodexUI != null) return;

        if (_itemCodexUiPrefab == null)
        {
            Debug.LogWarning($"HomeBase({name}): 도감 UI 프리팹이 설정되지 않아 도감을 열 수 없습니다.");
            return;
        }

        _openCodexUI = UIManager.Instance.OpenUI(_itemCodexUiPrefab);
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
