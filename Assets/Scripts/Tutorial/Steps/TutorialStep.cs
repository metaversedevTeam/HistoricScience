// 튜토리얼 한 단계의 공통 계약.
// 진행 상황이 계속 바뀌므로 대화 내용과 강조 대상은 저장해 두지 않고 매 프레임 새로 만들어 돌려준다.
public abstract class TutorialStep
{
    // 이 단계를 진행시키고 있는 실행기
    protected TutorialRunner Runner { get; private set; }

    // 씬 참조를 제공하는 문맥
    protected TutorialContext Context => Runner.Context;

    // 시점 회전·이동·아이템 획득 등 게임 쪽 변화를 지켜보는 감시자
    protected TutorialProgressWatcher Progress => Runner.Progress;

    // 시민 앞 상호작용 명령 목록을 지켜보는 감시자
    protected TutorialInteractionWatcher Interaction => Runner.Interaction;

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

    // 대화창을 클릭하거나 Space·Enter로 다음으로 넘기려 할 때 호출된다.
    public virtual void HandleAdvance()
    {
    }

    // 대화창의 예·아니요 버튼을 누르거나 1·2 키로 답했을 때 호출된다.
    public virtual void HandleChoice(bool isYes)
    {
    }
}
