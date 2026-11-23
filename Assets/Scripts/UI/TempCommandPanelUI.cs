using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// 선택된 상호작용 개체(ICommandable)의 명령 목록을 하단 패널에 버튼으로 표시하던, 씬에 상주하는 UI.
// PlayerManager의 선택 기능이 없어져 더 이상 선택 이벤트를 받지 않으며, 버튼도 만들지 않는다.
[Obsolete("PlayerManager의 선택 기능이 없어져 더 이상 쓰지 않는다. 명령은 Citizen 앞 대상의 상호작용 명령 목록(InteractionCommandListUI)에 표시된다.")]
public class TempCommandPanelUI : MonoBehaviour
{
    [SerializeField] private Transform _buttonContainer;

    [SerializeField] private CommandButtonView _commandButtonPrefab;

    [SerializeField] private PlayerManager _playerManager;

    private readonly List<GameObject> _activeButtons = new();

    // 파괴 시 남아 있는 버튼을 정리한다.
    private void OnDestroy()
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

    // 생성된 버튼 오브젝트를 모두 파괴한다. 파괴할 버튼이 현재 EventSystem의 선택 상태로 남아있으면
    // 먼저 선택을 해제해, 다음 프레임에 파괴된 오브젝트를 참조하는 일이 없게 한다.
    private void ClearButtons()
    {
        if (EventSystem.current != null && _activeButtons.Contains(EventSystem.current.currentSelectedGameObject))
            EventSystem.current.SetSelectedGameObject(null);

        foreach (var go in _activeButtons)
            Destroy(go);
        _activeButtons.Clear();
    }
}
