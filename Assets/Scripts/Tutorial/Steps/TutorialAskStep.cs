using System;

// 예·아니요 두 버튼으로 답을 받는 단계. 튜토리얼을 이미 들었는지 묻는 첫 단계에 쓴다.
public class TutorialAskStep : TutorialStep
{
    // 대화창에 띄울 질문
    private readonly string _question;

    // 왼쪽(예) 버튼 문구
    private readonly string _yesLabel;

    // 오른쪽(아니요) 버튼 문구
    private readonly string _noLabel;

    // 답을 골랐을 때 실행할 처리. 두 번째 인자가 true면 예를 고른 것이다.
    private readonly Action<TutorialRunner, bool> _onAnswered;

    // 답을 골랐는지 여부
    private bool _answered;

    // 질문과 두 버튼 문구, 답을 받았을 때의 처리를 받아 단계를 만든다.
    public TutorialAskStep(string question, string yesLabel, string noLabel, Action<TutorialRunner, bool> onAnswered)
    {
        _question = question;
        _yesLabel = yesLabel;
        _noLabel = noLabel;
        _onAnswered = onAnswered;
    }

    public override void Enter(TutorialRunner runner)
    {
        base.Enter(runner);
        _answered = false;
    }

    // 답을 고르면 끝난다.
    public override bool IsComplete => _answered;

    // 질문과 두 선택 버튼을 대화창에 표시한다.
    public override TutorialDialogueContent BuildContent()
    {
        return TutorialDialogueContent.Question(_question, _yesLabel, _noLabel);
    }

    // 고른 답을 기록하고 지정된 처리를 실행한다.
    public override void HandleChoice(bool isYes)
    {
        if (_answered) return;

        _answered = true;
        _onAnswered?.Invoke(Runner, isYes);
    }
}
