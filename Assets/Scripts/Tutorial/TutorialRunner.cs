using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 튜토리얼 단계를 순서대로 진행시키는 실행기.
// 맵을 새로 만든 세션에서만 TutorialSession이 이 오브젝트를 만들고, 튜토리얼이 끝나면 만들어 둔 UI까지 스스로 지우고 사라진다.
public class TutorialRunner : MonoBehaviour
{
    // 맵 로딩이 끝난 뒤 첫 대사를 띄우기까지 기다리는 시간(초). 로딩 화면이 사라지는 연출과 겹치지 않게 한다.
    private const float StartDelay = 0.6f;

    // 씬 참조를 제공하는 문맥
    public TutorialContext Context { get; private set; }

    // 시점 회전·이동·아이템 획득 등 게임 쪽 변화를 지켜보는 감시자
    public TutorialProgressWatcher Progress { get; private set; }

    // 시민 앞 상호작용 명령 목록을 지켜보는 감시자
    public TutorialInteractionWatcher Interaction { get; private set; }

    // 순서대로 진행할 단계들
    private readonly List<TutorialStep> _steps = new List<TutorialStep>();

    // 지금 진행 중인 단계의 순번
    private int _index;

    // 화면 아래에 떠 있는 대사창
    private TutorialDialogueUI _dialogue;

    // 첫 단계를 시작했는지 여부
    private bool _started;

    // 튜토리얼이 끝나 정리에 들어갔는지 여부
    private bool _finished;

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
        Interaction = new TutorialInteractionWatcher(Context);

        Transform uiRoot = UIManager.Instance.UIRoot;
        _dialogue = TutorialDialogueUI.Create(uiRoot);
        _dialogue.AdvanceRequested += HandleAdvanceRequested;
        _dialogue.ChoiceSelected += HandleChoiceSelected;

        _steps.AddRange(TutorialScenario.Build());
        _index = 0;
        _started = true;

        HandleBeginStep();
    }

    // 이번 프레임의 진행을 처리한다. 감시자를 갱신하고, 완료 여부를 본 뒤 대사를 다시 그린다.
    private void HandleRunStep()
    {
        Interaction.Tick();
        Progress.Tick();

        TutorialStep step = CurrentStep;
        if (step == null)
        {
            Finish();
            return;
        }

        step.Tick();
        HandleKeyboardInput(step);

        // 다 끝난 단계의 내용을 한 프레임 더 보여 주지 않도록, 완료 판정을 먼저 하고 다음 단계의 내용을 그린다.
        if (step.IsComplete)
        {
            HandleAdvanceStep();

            step = CurrentStep;
            if (_finished || step == null) return;
        }

        HandleRefreshDialogue(step);
    }

    // 3인칭 시점에서는 커서가 화면 가운데에 잠겨 대화창을 클릭할 수 없으므로, 키보드로 대사를 넘기고 질문에 답하게 한다.
    // 대사는 Space나 Enter로 넘기고, 예·아니요 질문은 1·2 키로 답한다. (Alt로 커서를 풀면 클릭도 그대로 된다)
    private void HandleKeyboardInput(TutorialStep step)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (step.BuildContent().HasChoices)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                HandleKeyChoice(true);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                HandleKeyChoice(false);
            return;
        }

        bool advance = keyboard.spaceKey.wasPressedThisFrame
            || keyboard.enterKey.wasPressedThisFrame
            || keyboard.numpadEnterKey.wasPressedThisFrame;

        if (advance && step.BuildContent().CanAdvance)
        {
            AudioManager.PlayButtonClick();
            HandleAdvanceRequested();
        }
    }

    // 1·2 키로 고른 답을 버튼을 누른 것과 같은 소리와 함께 지금 단계로 넘긴다.
    private void HandleKeyChoice(bool isYes)
    {
        AudioManager.PlayButtonClick();
        HandleChoiceSelected(isYes);
    }

    // 이번 단계를 시작한다. 단계별 판정 기록을 비운다.
    private void HandleBeginStep()
    {
        Progress.Reset();
        Interaction.Reset();

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

    // 대사창에 표시할 내용과 단계 진행도를 갱신한다.
    private void HandleRefreshDialogue(TutorialStep step)
    {
        _dialogue.SetContent(step.BuildContent());
        _dialogue.SetProgress(_index + 1, _steps.Count);
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

    // 만들어 둔 UI를 먼저 지우고, 다음 프레임에 구독을 정리한 뒤 스스로 사라진다.
    private IEnumerator HandleFinishRoutine()
    {
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
    }

    // 감시자와 문맥의 구독을 정리한다. 씬을 벗어나 파괴될 때도 호출된다.
    private void HandleReleaseResources()
    {
        Interaction = null;
        Progress?.Dispose();
        Context?.Dispose();

        Progress = null;
        Context = null;
    }
}
