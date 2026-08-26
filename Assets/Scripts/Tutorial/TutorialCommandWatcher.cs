using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 선택된 유닛의 명령 버튼(커맨드 패널)을 지켜보며, 강조할 버튼의 위치를 알려주고 어떤 명령이 눌렸는지 기록하는 감시자.
// 명령 실행은 각 오브젝트 안에서만 일어나 밖에서 알 방법이 없으므로, 버튼의 클릭 이벤트에 청취자만 하나 더 얹어 눌린 사실을 읽어 낸다.
public class TutorialCommandWatcher : IDisposable
{
    // 클릭을 가로채 기록하기 위해 버튼에 걸어 둔 청취자 한 건
    private readonly struct Hook
    {
        // 이 버튼이 실행하는 명령의 이름
        public readonly string CommandName;

        // 정리할 때 떼어 내야 하는 청취자
        public readonly UnityAction Listener;

        // 명령 이름과 걸어 둔 청취자로 기록을 만든다.
        public Hook(string commandName, UnityAction listener)
        {
            CommandName = commandName;
            Listener = listener;
        }
    }

    // 커맨드 패널을 찾지 못했을 때 씬 전체를 다시 훑기까지 기다리는 시간(초)
    private const float GlobalSearchInterval = 0.2f;

    // 명령 버튼이 만들어지는 패널. 씬에서 찾지 못했으면 null이며, 그때는 씬 전체 검색으로 대신한다.
    private readonly TempCommandPanelUI _panel;

    // 커맨드 패널이 없을 때 씬 전체를 다시 훑을 시각
    private float _nextGlobalSearchTime;

    // 청취자를 걸어 둔 버튼들
    private readonly Dictionary<Button, Hook> _hooks = new Dictionary<Button, Hook>();

    // 이번 단계에서 눌린 명령의 이름들
    private readonly HashSet<string> _clicked = new HashSet<string>();

    // 매 프레임 버튼을 훑을 때 재사용하는 버퍼
    private readonly List<CommandButtonView> _viewBuffer = new List<CommandButtonView>();

    // 파괴된 버튼을 목록에서 지울 때 재사용하는 버퍼
    private readonly List<Button> _removeBuffer = new List<Button>();

    // 지켜볼 커맨드 패널을 받아 감시자를 만든다.
    public TutorialCommandWatcher(TempCommandPanelUI panel)
    {
        _panel = panel;
    }

    // 새로 만들어진 명령 버튼에 청취자를 걸고, 파괴된 버튼을 목록에서 지운다.
    public void Tick()
    {
        CollectViews();

        foreach (CommandButtonView view in _viewBuffer)
            HookIfNeeded(view);

        RemoveDestroyedHooks();
    }

    // 이번 단계에서 지정한 이름의 명령이 눌렸는지 확인한다.
    public bool WasClicked(string commandName)
    {
        return _clicked.Contains(commandName);
    }

    // 눌린 명령 기록을 비운다. 단계가 바뀔 때 호출한다.
    public void ResetClicks()
    {
        _clicked.Clear();
    }

    // 지정한 이름의 명령 버튼이 화면에 떠 있으면 그 RectTransform을 돌려준다. 없으면 null이다.
    public RectTransform FindButton(string commandName)
    {
        foreach (KeyValuePair<Button, Hook> pair in _hooks)
        {
            if (pair.Key == null || pair.Value.CommandName != commandName) continue;
            if (!pair.Key.gameObject.activeInHierarchy) continue;

            return (RectTransform)pair.Key.transform;
        }

        return null;
    }

    // 걸어 둔 청취자를 모두 떼어 낸다.
    public void Dispose()
    {
        foreach (KeyValuePair<Button, Hook> pair in _hooks)
        {
            if (pair.Key != null)
                pair.Key.onClick.RemoveListener(pair.Value.Listener);
        }

        _hooks.Clear();
        _clicked.Clear();
    }

    // 지금 화면에 있는 명령 버튼들을 버퍼에 모은다. 커맨드 패널을 찾지 못했으면 씬 전체에서 찾아 대신 쓴다.
    private void CollectViews()
    {
        if (_panel != null)
        {
            _panel.GetComponentsInChildren(true, _viewBuffer);
            return;
        }

        if (Time.time < _nextGlobalSearchTime) return;

        _nextGlobalSearchTime = Time.time + GlobalSearchInterval;
        _viewBuffer.Clear();
        _viewBuffer.AddRange(UnityEngine.Object.FindObjectsByType<CommandButtonView>(FindObjectsSortMode.None));
    }

    // 아직 청취자를 걸지 않은 버튼이면, 표시된 명령 이름을 읽어 클릭 기록용 청취자를 건다.
    private void HookIfNeeded(CommandButtonView view)
    {
        Button button = view.GetComponentInChildren<Button>(true);
        if (button == null || _hooks.ContainsKey(button)) return;

        TextMeshProUGUI label = view.GetComponentInChildren<TextMeshProUGUI>(true);
        string commandName = label != null ? label.text : string.Empty;

        UnityAction listener = () => _clicked.Add(commandName);
        button.onClick.AddListener(listener);
        _hooks.Add(button, new Hook(commandName, listener));
    }

    // 선택이 바뀌며 파괴된 버튼들을 목록에서 지운다.
    private void RemoveDestroyedHooks()
    {
        _removeBuffer.Clear();

        foreach (KeyValuePair<Button, Hook> pair in _hooks)
        {
            if (pair.Key == null)
                _removeBuffer.Add(pair.Key);
        }

        foreach (Button button in _removeBuffer)
            _hooks.Remove(button);
    }
}
