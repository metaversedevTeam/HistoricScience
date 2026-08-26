using System;
using UnityEngine;

// 안내 대사를 클릭으로 넘긴 뒤, 실제로 조작이 이루어질 때까지 기다리는 단계.
// 기다리는 동안 표시할 문구와 완료 판정은 같은 함수에서 함께 만들어져, 진행도 표시와 판정이 어긋나지 않는다.
public class TutorialTaskStep : TutorialStep
{
    // 목표를 알리기 전에 클릭으로 넘길 안내 대사들. 없어도 된다.
    private readonly string[] _introLines;

    // 매 프레임 호출해 안내 문구와 달성 여부를 받아 오는 함수
    private readonly Func<TutorialRunner, TutorialStepStatus> _status;

    // 매 프레임 호출해 강조 대상을 받아 오는 함수. 없으면 강조하지 않는다.
    private readonly Func<TutorialRunner, TutorialHighlightRequest?> _highlight;

    // 이 단계 내내 대화창 옆에 띄울 그림. 없으면 그림 카드를 띄우지 않는다.
    private readonly Sprite _image;

    // 그림 카드 위에 적을 설명
    private readonly string _imageCaption;

    // 지금 보여 주고 있는 안내 대사의 순번
    private int _index;

    // 안내 대사, 목표 판정, 강조 대상, 함께 띄울 그림을 받아 단계를 만든다.
    public TutorialTaskStep(
        string[] introLines,
        Func<TutorialRunner, TutorialStepStatus> status,
        Func<TutorialRunner, TutorialHighlightRequest?> highlight = null,
        Sprite image = null,
        string imageCaption = null)
    {
        _introLines = introLines ?? Array.Empty<string>();
        _status = status;
        _highlight = highlight;
        _image = image;
        _imageCaption = imageCaption;
    }

    public override void Enter(TutorialRunner runner)
    {
        base.Enter(runner);
        _index = 0;
    }

    // 안내 대사를 모두 넘겼고 목표까지 달성했으면 끝난다.
    public override bool IsComplete => IsIntroFinished && _status(Runner).IsDone;

    // 안내 대사를 보여 주는 중에는 클릭으로 넘길 수 있게, 목표를 기다리는 중에는 진행도 문구를 표시한다.
    public override TutorialDialogueContent BuildContent()
    {
        if (!IsIntroFinished)
            return TutorialDialogueContent.Line(_introLines[_index], true, _image, _imageCaption);

        return TutorialDialogueContent.Line(_status(Runner).Text, false, _image, _imageCaption);
    }

    // 안내 대사를 읽는 중에도 어디를 봐야 하는지 알 수 있도록, 단계 내내 강조 대상을 알려 준다.
    public override TutorialHighlightRequest? BuildHighlight()
    {
        return _highlight?.Invoke(Runner);
    }

    // 다음 안내 대사로 넘어간다. 이미 목표를 기다리는 중이면 아무 일도 하지 않는다.
    public override void HandleAdvance()
    {
        if (IsIntroFinished) return;

        _index++;
    }

    // 안내 대사를 모두 넘겼는지 여부
    private bool IsIntroFinished => _index >= _introLines.Length;
}
