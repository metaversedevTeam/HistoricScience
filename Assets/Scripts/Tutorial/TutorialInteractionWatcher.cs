using System.Collections.Generic;
using UnityEngine;

// 플레이어 시민 앞에 뜨는 상호작용 명령 목록(휠로 고르고 F로 실행)을 지켜보는 감시자.
// 명령 실행은 시민 안에서만 일어나 밖에서 알 방법이 없으므로, 목록에 뜬 명령 이름과 선택 줄을 시민과 같은 규칙으로 다시 계산해
// F가 눌린 순간 선택돼 있던 명령을 실행된 것으로 기록한다.
public class TutorialInteractionWatcher
{
    // IGatherable 대상에게 시민이 띄우는 명령 이름. Citizen의 채집 명령 이름과 같아야 한다.
    public const string GatherCommand = "채집";

    // 씬 참조를 제공하는 문맥
    private readonly TutorialContext _context;

    // 지금 목록에 떠 있는 명령 이름들. 시민이 목록을 만드는 순서(채집 먼저, 그 뒤 ICommandable 명령)와 같다.
    private readonly List<string> _names = new List<string>();

    // 이번 단계에서 실행된 명령의 이름들
    private readonly HashSet<string> _executed = new HashSet<string>();

    // 직전 프레임에 선택돼 있던 줄 번호. 휠로 선택을 바꿨는지 판단하는 기준이다.
    private int _lastSelectedIndex = -1;

    // 지금 시민 앞에 있는 상호작용 대상. 없으면 null이다.
    public GameObject Target { get; private set; }

    // 이번 단계에서 휠로 명령 선택을 바꿨는지 여부
    public bool SelectionChanged { get; private set; }

    // 지금 목록에서 선택된 명령 이름. 목록이 떠 있지 않으면 null이다.
    public string SelectedName
    {
        get
        {
            InteractionCommandListUI list = _context.CommandList;
            if (list == null || Target == null) return null;

            int index = list.SelectedIndex;
            return index >= 0 && index < _names.Count ? _names[index] : null;
        }
    }

    // 지켜볼 문맥을 받아 감시자를 만든다.
    public TutorialInteractionWatcher(TutorialContext context)
    {
        _context = context;
    }

    // 시민 앞 대상과 목록을 다시 읽고, 휠 선택 변화와 F로 실행된 명령을 기록한다.
    public void Tick()
    {
        HandleRefreshTarget();
        HandleTrackSelection();
        HandleTrackExecute();
    }

    // 이번 단계에서 지정한 이름의 명령이 실행됐는지 확인한다.
    public bool WasExecuted(string commandName)
    {
        return _executed.Contains(commandName);
    }

    // 지금 목록에 지정한 이름의 명령이 떠 있는지 확인한다.
    public bool HasCommand(string commandName)
    {
        return Target != null && _names.Contains(commandName);
    }

    // 지금 시민 앞 대상이 지정한 컴포넌트를 가진 오브젝트인지 확인한다.
    public bool IsFacing<T>() where T : Component
    {
        return Target != null && Target.GetComponentInParent<T>() != null;
    }
    // 단계별 기록을 비운다. 단계가 바뀔 때 호출한다.
    public void Reset()
    {
        _executed.Clear();
        SelectionChanged = false;
        _lastSelectedIndex = _context.CommandList != null && Target != null ? _context.CommandList.SelectedIndex : -1;
    }

    // 플레이어 시민 앞 대상을 읽고, 시민과 같은 규칙으로 목록의 명령 이름을 다시 만든다.
    private void HandleRefreshTarget()
    {
        Citizen citizen = _context.FollowedCitizen;
        Target = citizen != null ? citizen.InteractionTarget : null;

        _names.Clear();
        if (Target == null) return;

        if (Target.GetComponentInParent<IGatherable>() != null)
            _names.Add(GatherCommand);

        ICommandable commandable = Target.GetComponentInParent<ICommandable>();
        if (commandable == null) return;

        foreach (CommandData command in commandable.GetCommands())
            _names.Add(command.Name);
    }

    // 같은 목록이 떠 있는 동안 선택 줄이 바뀌면 휠로 선택을 바꾼 것으로 기록한다.
    private void HandleTrackSelection()
    {
        InteractionCommandListUI list = _context.CommandList;
        int index = list != null && Target != null ? list.SelectedIndex : -1;

        if (index >= 0 && _lastSelectedIndex >= 0 && index != _lastSelectedIndex)
            SelectionChanged = true;

        _lastSelectedIndex = index;
    }

    // F가 눌린 프레임에 목록이 떠 있었다면 선택돼 있던 명령을 실행된 것으로 기록한다.
    private void HandleTrackExecute()
    {
        PlayerManager player = _context.PlayerManager;
        if (player == null || !player.IsInteractPressed) return;

        string selected = SelectedName;
        if (selected != null)
            _executed.Add(selected);
    }
}
