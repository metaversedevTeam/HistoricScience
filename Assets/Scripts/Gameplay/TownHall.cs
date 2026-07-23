using System.Collections.Generic;
using UnityEngine;

// 선택되었을 때 식량을 소모해 시민을 소환하는 명령을 제공하는 중앙 건물 오브젝트
public class TownHall : MonoBehaviour, ICommandable, ISavable
{
    [SerializeField] private Sprite _summonCommandIcon;
    // 소환할 시민 프리팹
    [SerializeField] private Citizen _citizenPrefab;
    // 시민이 소환될 위치를 지정하는 트랜스폼
    [SerializeField] private Transform _spawnPoint;
    // 소환에 소모되는 식량 자원 데이터
    [SerializeField] private ItemData _foodData;
    // 시민 한 명을 소환하는 데 필요한 식량 수량
    [SerializeField, Min(0)] private int _foodCost = 50;
    // 저장/복원 기능을 제공하는 컴포지션. PrefabId는 인스펙터에서 설정한다.
    [SerializeField] private SavableHandler _savable = new();

    private SelectableObject _selectable;
    private PlayerManager _selectedBy;
    private IReadOnlyList<CommandData> _commands;

    // SelectableObject 컴포넌트를 캐싱하고 명령 목록을 생성
    private void Awake()
    {
        _selectable = GetComponent<SelectableObject>();
        _commands = new List<CommandData> { new CommandData("시민 소환", _summonCommandIcon, SummonCitizen) };
    }

    // 자신의 선택 이벤트를 구독
    private void OnEnable()
    {
        _selectable.OnSelect += HandleSelect;
    }

    // 자신의 선택 이벤트와 PlayerManager 구독을 해제
    private void OnDisable()
    {
        _selectable.OnSelect -= HandleSelect;
        UnsubscribeFromPlayer();
    }

    // 이 오브젝트가 제공하는 명령 목록을 반환한다.
    public IReadOnlyList<CommandData> GetCommands()
    {
        return _commands;
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

    // 선택 중인 플레이어의 식량을 소모하고 지정된 위치에 시민을 소환한다. 식량이 부족하면 소환하지 않는다.
    private void SummonCitizen()
    {
        if (_selectedBy == null) return;

        if (_citizenPrefab == null || _foodData == null)
        {
            Debug.LogWarning($"TownHall({name}): 시민 프리팹 또는 식량 데이터가 설정되지 않아 소환할 수 없습니다.");
            return;
        }

        if (!_selectedBy.ResourceInventory.Remove(_foodData, _foodCost))
            return;

        Transform spawnAt = _spawnPoint != null ? _spawnPoint : transform;
        Instantiate(_citizenPrefab, spawnAt.position, spawnAt.rotation);
    }

    public string PrefabId => _savable.PrefabId;

    // 현재 상태를 JSON 문자열로 캡처한다.
    public string CaptureJson() => _savable.CaptureJson(transform);

    // JSON 문자열로 상태를 복원한다.
    public void ApplyJson(string json) => _savable.ApplyJson(transform, json);
}
