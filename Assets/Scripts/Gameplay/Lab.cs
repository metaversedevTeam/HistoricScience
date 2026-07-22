using System.Collections.Generic;
using UnityEngine;

// 선택되었을 때 작업대 UI를 여는 명령을 제공하는 연구소 오브젝트
public class Lab : MonoBehaviour, ICommandable, ISavable
{
    [SerializeField] private WorkbenchUI _workbenchUiPrefab;
    [SerializeField] private Sprite _workbenchButtonIcon;
    // 저장/복원 기능을 제공하는 컴포지션. PrefabId는 인스펙터에서 설정한다.
    [SerializeField] private SavableHandler _savable = new();

    private SelectableObject _selectable;
    private PlayerManager _selectedBy;
    private IReadOnlyList<CommandData> _commands;

    // SelectableObject 컴포넌트를 캐싱하고 명령 목록을 생성
    private void Awake()
    {
        _selectable = GetComponent<SelectableObject>();
        _commands = new List<CommandData> { new CommandData("작업대 열기", _workbenchButtonIcon, OpenWorkbenchUI) };
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

    // 작업대 UI를 선택 중인 플레이어의 자원 인벤토리와 함께 연다.
    private void OpenWorkbenchUI()
    {
        if (_selectedBy == null) return;
        UIManager.Instance.OpenUI(_workbenchUiPrefab, _selectedBy.ResourceInventory);
    }

    public string PrefabId => _savable.PrefabId;

    // 현재 상태를 JSON 문자열로 캡처한다.
    public string CaptureJson() => _savable.CaptureJson(transform);

    // JSON 문자열로 상태를 복원한다.
    public void ApplyJson(string json) => _savable.ApplyJson(transform, json);
}
