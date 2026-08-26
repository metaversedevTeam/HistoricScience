// 튜토리얼 한 단계의 공통 계약.
// 진행 상황이 계속 바뀌므로 대화 내용과 강조 대상은 저장해 두지 않고 매 프레임 새로 만들어 돌려준다.
public abstract class TutorialStep
{
    // 이 단계를 진행시키고 있는 실행기
    protected TutorialRunner Runner { get; private set; }

    // 씬 참조를 제공하는 문맥
    protected TutorialContext Context => Runner.Context;

    // 카메라 이동·아이템 획득 등 게임 쪽 변화를 지켜보는 감시자
    protected TutorialProgressWatcher Progress => Runner.Progress;

    // 명령 버튼을 지켜보는 감시자
    protected TutorialCommandWatcher Commands => Runner.Commands;

    // 이 단계가 시작될 때 호출된다. 재정의할 때는 반드시 base를 먼저 호출할 것.
    public virtual void Enter(TutorialRunner runner)
    {
        Runner = runner;
    }

    // 매 프레임 호출된다. 단계 안에서 따로 세어야 할 것이 있으면 여기서 처리한다.
    public virtual void Tick()
    {
    }

    // 이 단계가 끝났는지 여부
    public abstract bool IsComplete { get; }

    // 이 단계가 끝날 때 호출된다.
    public virtual void Exit()
    {
    }

    // 지금 대화창에 표시할 내용을 만든다.
    public abstract TutorialDialogueContent BuildContent();

    // 지금 강조할 대상을 만든다. null이면 강조 표시를 띄우지 않는다.
    public virtual TutorialHighlightRequest? BuildHighlight()
    {
        return null;
    }

    // 대화창을 클릭해 다음으로 넘기려 할 때 호출된다.
    public virtual void HandleAdvance()
    {
    }

    // 대화창의 예·아니요 버튼을 눌렀을 때 호출된다.
    public virtual void HandleChoice(bool isYes)
    {
    }
}
