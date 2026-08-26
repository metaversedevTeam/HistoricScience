using UnityEngine;

// 튜토리얼 대화창에 한 번에 표시할 내용. 매 프레임 다시 만들어져 대화창에 전달된다.
public readonly struct TutorialDialogueContent
{
    // 대화창 본문. TMP 리치 텍스트를 그대로 쓸 수 있다.
    public readonly string Body;

    // 클릭해서 다음으로 넘어갈 수 있는 상태인지 여부 (피그마 next-indicator 표시 여부와 같다)
    public readonly bool CanAdvance;

    // 선택 버튼을 띄울 때의 왼쪽 버튼 문구. 비어 있으면 선택 버튼을 띄우지 않는다.
    public readonly string YesLabel;

    // 선택 버튼을 띄울 때의 오른쪽 버튼 문구
    public readonly string NoLabel;

    // 대화창 옆에 함께 띄울 그림. 비어 있으면 그림 카드를 띄우지 않는다.
    public readonly Sprite Image;

    // 그림 카드 위에 적을 설명
    public readonly string ImageCaption;

    // 본문과 표시 방식을 직접 지정해 대화 내용을 만든다.
    private TutorialDialogueContent(string body, bool canAdvance, string yesLabel, string noLabel, Sprite image, string imageCaption)
    {
        Body = body;
        CanAdvance = canAdvance;
        YesLabel = yesLabel;
        NoLabel = noLabel;
        Image = image;
        ImageCaption = imageCaption;
    }

    // 클릭으로 넘길 수 있는 평범한 한 줄 대사를 만든다.
    public static TutorialDialogueContent Line(string body, bool canAdvance = true, Sprite image = null, string imageCaption = null)
    {
        return new TutorialDialogueContent(body, canAdvance, null, null, image, imageCaption);
    }

    // 예·아니요 버튼이 함께 뜨는 질문을 만든다.
    public static TutorialDialogueContent Question(string body, string yesLabel, string noLabel)
    {
        return new TutorialDialogueContent(body, false, yesLabel, noLabel, null, null);
    }

    // 선택 버튼을 띄워야 하는 내용인지 여부
    public bool HasChoices => !string.IsNullOrEmpty(YesLabel) && !string.IsNullOrEmpty(NoLabel);

    // 그림 카드를 띄워야 하는 내용인지 여부
    public bool HasImage => Image != null;

    // 다른 내용과 화면에 그려질 결과가 같은지 비교한다. 같으면 대화창을 다시 그리지 않는다.
    public bool SameAs(in TutorialDialogueContent other)
    {
        return Body == other.Body
            && CanAdvance == other.CanAdvance
            && YesLabel == other.YesLabel
            && NoLabel == other.NoLabel
            && Image == other.Image
            && ImageCaption == other.ImageCaption;
    }
}
