using UnityEngine;

// 대사만 차례로 보여 주고 클릭으로 넘어가는 단계. 개념 설명(바이옴 등)이나 마무리 인사에 쓴다.
public class TutorialTalkStep : TutorialStep
{
    // 차례로 보여 줄 대사들
    private readonly string[] _lines;

    // 지금 보여 주고 있는 대사의 순번
    private int _index;

    // 보여 줄 대사들을 받아 단계를 만든다.
    public TutorialTalkStep(params string[] lines)
    {
        _lines = lines;
    }

    public override void Enter(TutorialRunner runner)
    {
        base.Enter(runner);
        _index = 0;
    }

    // 준비된 대사를 모두 넘겼으면 끝난다.
    public override bool IsComplete => _lines == null || _index >= _lines.Length;

    // 지금 순번의 대사를 클릭으로 넘길 수 있는 상태로 표시한다.
    public override TutorialDialogueContent BuildContent()
    {
        if (IsComplete) return TutorialDialogueContent.Line(string.Empty, false);

        return TutorialDialogueContent.Line(_lines[Mathf.Clamp(_index, 0, _lines.Length - 1)]);
    }

    // 다음 대사로 넘어간다.
    public override void HandleAdvance()
    {
        _index++;
    }
}
