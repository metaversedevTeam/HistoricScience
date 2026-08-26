using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 튜토리얼 단계를 순서대로 진행시키는 실행기.
// 맵을 새로 만든 세션에서만 TutorialSession이 이 오브젝트를 만들고, 튜토리얼이 끝나면 만들어 둔 UI까지 스스로 지우고 사라진다.
public class TutorialRunner : MonoBehaviour
{
    // 맵 로딩이 끝난 뒤 첫 대사를 띄우기까지 기다리는 시간(초). 로딩 화면이 사라지는 연출과 겹치지 않게 한다.
    private const float StartDelay = 0.6f;

    // 씬 참조를 제공하는 문맥
    public TutorialContext Context { get; private set; }

    // 카메라 이동·아이템 획득 등 게임 쪽 변화를 지켜보는 감시자
    public TutorialProgressWatcher Progress { get; private set; }

    // 명령 버튼을 지켜보는 감시자
    public TutorialCommandWatcher Commands { get; private set; }

    // 순서대로 진행할 단계들
    private readonly List<TutorialStep> _steps = new List<TutorialStep>();

    // UIManager를 통해 열어 본 강조 UI 인스턴스들. 정리할 때 함께 파괴한다.
    private readonly List<TutorialHighlightUI> _spawnedHighlights = new List<TutorialHighlightUI>();

    // 지금 진행 중인 단계의 순번
    private int _index;

    // 화면 아래에 떠 있는 대사창
    private TutorialDialogueUI _dialogue;

    // UIManager가 복제해 열 강조 UI 원본
    private TutorialHighlightUI _highlightTemplate;

    // 지금 열려 있는 강조 UI
    private TutorialHighlightUI _openHighlight;

    // 첫 단계를 시작했는지 여부
    private bool _started;

    // 튜토리얼이 끝나 정리에 들어갔는지 여부
    private bool _finished;

    // 플레이어가 ESC로 강조 표시를 닫아, 이 단계에서는 다시 띄우지 않기로 한 상태인지 여부
    private bool _highlightDismissed;

    // 강조 UI를 실행기 스스로 닫는 중인지 여부. 플레이어가 ESC로 닫은 경우와 구분한다.
    private bool _closingHighlight;

    // 맵 로딩이 끝나기를 기다린 시간(초)
    private float _readyElapsed;

    private void Update()
    {
        if (_finished) return;

        if (!_started)
        {
            HandleWaitForMap();
            return;
        }

        HandleRunStep();
    }

    private void OnDestroy()
    {
        HandleReleaseResources();
    }

    // 튜토리얼을 즉시 끝내고 만들어 둔 UI를 정리한다. (첫 질문의 "예"와 마지막 단계 완료에서 호출한다)
    public void Finish()
    {
        if (_finished) return;

        _finished = true;
        StartCoroutine(HandleFinishRoutine());
    }

    // 지금 진행 중인 단계. 모든 단계를 끝냈으면 null이다.
    private TutorialStep CurrentStep => _index >= 0 && _index < _steps.Count ? _steps[_index] : null;

    // 맵 로딩이 끝날 때까지 기다렸다가 튜토리얼을 시작한다.
    private void HandleWaitForMap()
    {
        if (Context == null)
        {
            Context = new TutorialContext();
            if (!Context.TryResolve())
            {
                Context = null;
                _finished = true;
                Destroy(gameObject);
                return;
            }
        }

        if (!Context.IsMapReady)
        {
            _readyElapsed = 0f;
            return;
        }

        _readyElapsed += Time.deltaTime;
        if (_readyElapsed < StartDelay) return;

        HandleBegin();
    }

    // 감시자와 UI를 만들고 첫 단계를 시작한다.
    private void HandleBegin()
    {
        Progress = new TutorialProgressWatcher(Context);
        Commands = new TutorialCommandWatcher(Context.CommandPanel);

        Transform uiRoot = UIManager.Instance.UIRoot;
        _dialogue = TutorialDialogueUI.Create(uiRoot);
        _dialogue.AdvanceRequested += HandleAdvanceRequested;
        _dialogue.ChoiceSelected += HandleChoiceSelected;

        _highlightTemplate = TutorialHighlightUI.CreateTemplate(uiRoot);

        _steps.AddRange(TutorialScenario.Build());
        _index = 0;
        _started = true;

        HandleBeginStep();
    }

    // 이번 프레임의 진행을 처리한다. 감시자를 갱신하고, 대사와 강조를 다시 그린 뒤 완료 여부를 본다.
    private void HandleRunStep()
    {
        Commands.Tick();
        Progress.Tick();

        TutorialStep step = CurrentStep;
        if (step == null)
        {
            Finish();
            return;
        }

        step.Tick();

        // 다 끝난 단계의 내용을 한 프레임 더 보여 주지 않도록, 완료 판정을 먼저 하고 다음 단계의 내용을 그린다.
        if (step.IsComplete)
        {
            HandleAdvanceStep();

            step = CurrentStep;
            if (_finished || step == null) return;
        }

        HandleRefreshDialogue(step);
        HandleRefreshHighlight(step);
    }

    // 이번 단계를 시작한다. 단계별 판정 기록을 비우고 이전 단계의 강조를 걷는다.
    private void HandleBeginStep()
    {
        Progress.Reset();
        Commands.ResetClicks();
        _highlightDismissed = false;
        HandleCloseHighlight();

        CurrentStep?.Enter(this);
    }

    // 이번 단계를 끝내고 다음 단계로 넘어간다. 남은 단계가 없으면 튜토리얼을 끝낸다.
    private void HandleAdvanceStep()
    {
        CurrentStep.Exit();
        _index++;

        if (CurrentStep == null)
        {
            Finish();
            return;
        }

        HandleBeginStep();
    }

    // 대사창에 표시할 내용을 갱신한다.
    private void HandleRefreshDialogue(TutorialStep step)
    {
        _dialogue.SetContent(step.BuildContent());
    }

    // 이번 단계가 강조할 대상이 있으면 강조 UI를 열고, 없으면 닫는다.
    private void HandleRefreshHighlight(TutorialStep step)
    {
        bool wantsHighlight = !_highlightDismissed && step.BuildHighlight() != null;

        if (wantsHighlight && _openHighlight == null)
        {
            HandleOpenHighlight();
            return;
        }

        if (!wantsHighlight && _openHighlight != null)
            HandleCloseHighlight();
    }

    // 강조 UI를 UIManager로 연다. 지금 열려 있는 다른 UI보다 뒤에 붙으므로 그 위에 그려진다.
    private void HandleOpenHighlight()
    {
        var data = new TutorialHighlightData(HandleProvideHighlight, _dialogue.transform);

        _openHighlight = UIManager.Instance.OpenUI(_highlightTemplate, data);
        _openHighlight.OnFinishClose += HandleHighlightClosed;

        if (!_spawnedHighlights.Contains(_openHighlight))
            _spawnedHighlights.Add(_openHighlight);
    }

    // 열려 있는 강조 UI를 실행기 쪽에서 닫는다.
    private void HandleCloseHighlight()
    {
        if (_openHighlight == null) return;

        _closingHighlight = true;
        _openHighlight.Close(true);
        _closingHighlight = false;
    }

    // 강조 UI가 닫혔을 때의 뒤처리. 실행기가 닫은 것이 아니라면 플레이어가 ESC로 닫은 것이므로,
    // 이 단계 동안에는 다시 띄우지 않아 다음 ESC가 일시정지 화면까지 닿게 한다.
    private void HandleHighlightClosed(IManagedUI ui)
    {
        ui.OnFinishClose -= HandleHighlightClosed;

        if (!_closingHighlight)
            _highlightDismissed = true;

        _openHighlight = null;
    }

    // 강조 UI가 매 프레임 호출하는 대상 제공자. 지금 단계에 물어 강조 대상을 넘긴다.
    private TutorialHighlightRequest? HandleProvideHighlight()
    {
        return _finished ? null : CurrentStep?.BuildHighlight();
    }

    // 대화창 클릭을 지금 단계로 넘긴다.
    private void HandleAdvanceRequested()
    {
        if (_finished) return;

        CurrentStep?.HandleAdvance();
    }

    // 예·아니요 선택을 지금 단계로 넘긴다.
    private void HandleChoiceSelected(bool isYes)
    {
        if (_finished) return;

        CurrentStep?.HandleChoice(isYes);
    }

    // 만들어 둔 UI를 먼저 지우고, 실제로 파괴된 다음 프레임에 그려 둔 스프라이트를 정리한 뒤 스스로 사라진다.
    private IEnumerator HandleFinishRoutine()
    {
        HandleCloseHighlight();
        HandleDestroyUI();

        yield return null;

        HandleReleaseResources();
        Destroy(gameObject);
    }

    // 튜토리얼이 만든 UI 오브젝트를 모두 파괴한다.
    private void HandleDestroyUI()
    {
        if (_dialogue != null)
        {
            _dialogue.AdvanceRequested -= HandleAdvanceRequested;
            _dialogue.ChoiceSelected -= HandleChoiceSelected;
            Destroy(_dialogue.gameObject);
            _dialogue = null;
        }

        // UIManager 풀에 남은 인스턴스까지 지워, 정리한 스프라이트를 참조하는 오브젝트가 남지 않게 한다.
        foreach (TutorialHighlightUI highlight in _spawnedHighlights)
        {
            if (highlight != null)
                Destroy(highlight.gameObject);
        }
        _spawnedHighlights.Clear();

        if (_highlightTemplate != null)
        {
            Destroy(_highlightTemplate.gameObject);
            _highlightTemplate = null;
        }
    }

    // 구독과 실행 중 만든 스프라이트를 정리한다. 씬을 벗어나 파괴될 때도 호출된다.
    private void HandleReleaseResources()
    {
        Commands?.Dispose();
        Progress?.Dispose();
        Context?.Dispose();

        Commands = null;
        Progress = null;
        Context = null;

        TutorialSpriteLibrary.Release();
    }
}
