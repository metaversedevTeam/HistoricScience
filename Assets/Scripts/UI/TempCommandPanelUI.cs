using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 선택된 유닛의 ICommandable 명령 목록을 하단 패널에 버튼으로 표시하는, 씬에 상주하는 UI
public class TempCommandPanelUI : MonoBehaviour
{
    [SerializeField] private Transform _buttonContainer;

    [SerializeField] private CommandButtonView _commandButtonPrefab;

    [SerializeField] private PlayerManager _playerManager;

    private readonly List<GameObject> _activeButtons = new();

    private string _dbg = "waiting...";

    // 씬 상주 오브젝트이므로 로드 시점에 바로 PlayerManager의 선택 이벤트를 구독한다.
    private void Awake()
    {
        if (_playerManager == null)
        {
            Debug.LogWarning("[TempCommandPanelUI] PlayerManager가 Inspector에 할당되지 않았습니다.", this);
            return;
        }

        _playerManager.OnSelected   += HandleSelected;
        _playerManager.OnDeselected += HandleDeselected;
    }

    // 파괴 시 이벤트 구독을 해제한다.
    private void OnDestroy()
    {
        if (_playerManager == null) return;

        _playerManager.OnSelected   -= HandleSelected;
        _playerManager.OnDeselected -= HandleDeselected;
    }

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            bool over = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            string sel = EventSystem.current?.currentSelectedGameObject?.name ?? "none";
            _dbg = $"overUI:{over} sel:{sel}";
            Debug.Log($"[CommandPanelUI] click — {_dbg}");
        }
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 700, 30), $"[DBG] {_dbg}  pos:{Mouse.current?.position.ReadValue()}");
    }

    // 선택된 오브젝트의 ICommandable 명령 목록을 읽어 버튼을 생성한다.
    private void HandleSelected(SelectableObject selected)
    {
        ClearButtons();
        var commandable = selected.GetComponent<ICommandable>();
        if (commandable == null) return;

        foreach (var cmd in commandable.GetCommands())
            CreateButton(cmd);
    }

    // 패널을 비우고 모든 버튼을 제거한다.
    private void HandleDeselected()
    {
        ClearButtons();
    }

    // CommandData 하나에 대응하는 버튼을 프리팹으로부터 생성해 컨테이너에 추가한다.
    private void CreateButton(CommandData cmd)
    {
        var view = Instantiate(_commandButtonPrefab, _buttonContainer);
        view.Bind(cmd);
        _activeButtons.Add(view.gameObject);
    }

    // 생성된 버튼 오브젝트를 모두 파괴한다.
    private void ClearButtons()
    {
        foreach (var go in _activeButtons)
            Destroy(go);
        _activeButtons.Clear();
    }
}
