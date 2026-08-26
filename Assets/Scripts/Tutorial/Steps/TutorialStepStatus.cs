// 목표 단계가 매 프레임 돌려주는 현재 상태 — 대화창에 띄울 안내 문구와 목표 달성 여부.
// 문구와 판정을 한 번에 만들어, 진행도 표시와 완료 조건이 서로 어긋나지 않게 한다.
public readonly struct TutorialStepStatus
{
    // 대화창에 띄울 안내 문구
    public readonly string Text;

    // 목표를 달성했는지 여부
    public readonly bool IsDone;

    // 안내 문구와 달성 여부로 상태를 만든다.
    public TutorialStepStatus(string text, bool isDone)
    {
        Text = text;
        IsDone = isDone;
    }

    // 아직 달성하지 못한 상태를 만든다.
    public static TutorialStepStatus Waiting(string text) => new TutorialStepStatus(text, false);

    // 이미 달성한 상태를 만든다.
    public static TutorialStepStatus Done(string text) => new TutorialStepStatus(text, true);
}
