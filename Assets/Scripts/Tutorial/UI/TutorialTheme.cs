using UnityEngine;

// 피그마 "tutorial UI" 프레임(Game Tutorial UI Kit)에서 그대로 뽑아낸 튜토리얼 UI의 색과 치수 모음.
// 튜토리얼은 프리팹 없이 코드로 화면을 만들기 때문에, 디자인 값을 이 한 곳에만 모아 둔다.
public static class TutorialTheme
{
    // 대화창·팝업 본체의 배경색
    public static readonly Color PanelFill = FromHex(0x0E1527);

    // 대화창·칩의 테두리색
    public static readonly Color PanelBorder = FromHex(0x1E293B);

    // 말하는 이 배지와 버튼의 배경색
    public static readonly Color ChipFill = FromHex(0x1E293B);

    // 강조 링·화살표·배지 글자에 쓰는 청록 강조색
    public static readonly Color Accent = FromHex(0x06B6D4);

    // 진행 표시 삼각형과 점선 링에 쓰는 호박색 강조색
    public static readonly Color Amber = FromHex(0xFBBF24);

    // 본문 글자색
    public static readonly Color BodyText = FromHex(0xF8FAFC);

    // 보조 설명 글자색
    public static readonly Color MutedText = FromHex(0x94A3B8);

    // 예·아니요 선택 버튼의 배경색
    public static readonly Color ButtonFill = FromHex(0x171B2B);

    // 강조 링 바깥을 덮는 스포트라이트 마스크 색 (피그마 spotlight-dim-mask)
    public static readonly Color SpotlightDim = new Color(0f, 0f, 0f, 0.55f);

    // 아바타 자리표시자의 배경색
    public static readonly Color AvatarFill = FromHex(0x132033);

    // 대화창 본체 크기 (피그마 dialogue-box 900x180)
    public static readonly Vector2 DialogueSize = new Vector2(900f, 180f);

    // 예·아니요 버튼이 함께 뜰 때의 대화창 높이. 버튼이 본문 글자를 덮지 않도록 그만큼 키운다.
    public const float DialogueQuestionHeight = 240f;

    // 대화창 위쪽 끝에서 아바타까지의 거리 (피그마 character-avatar-container y=45)
    public const float AvatarTopMargin = 45f;

    // 대화창을 화면 아래에서 띄울 거리. 씬의 커맨드 패널(높이 200)을 가리지 않도록 그보다 위에 둔다.
    public const float DialogueBottomMargin = 220f;

    // 화면 가장자리에서 대화창 등을 띄울 여백
    public const float ScreenMargin = 48f;

    // 아바타 원의 지름 (피그마 character-avatar-container 90x90)
    public const float AvatarSize = 90f;

    // 대화창 왼쪽 끝에서 아바타까지의 거리
    public const float AvatarLeftMargin = 24f;

    // 대화창 왼쪽 끝에서 본문 영역까지의 거리 (피그마 dialogue-content x=138)
    public const float ContentLeftMargin = 138f;

    // 대화창 오른쪽 끝에서 본문 영역까지의 거리
    public const float ContentRightMargin = 24f;

    // 말하는 이 배지 크기 (피그마 speaker-badge 66x26)
    public static readonly Vector2 BadgeSize = new Vector2(66f, 26f);

    // 다음으로 넘어갈 수 있음을 알리는 삼각형 크기 (피그마 triangle 16x10을 조금 키운 값)
    public static readonly Vector2 NextIndicatorSize = new Vector2(22f, 14f);

    // 대화창 옆에 붙는 그림 카드의 크기
    public static readonly Vector2 ImageCardSize = new Vector2(220f, 230f);

    // 그림 카드와 대화창 사이의 간격
    public const float ImageCardGap = 20f;

    // 그림 카드 안쪽 여백
    public const float ImageCardPadding = 14f;

    // 그림 카드의 설명 줄 높이
    public const float ImageCaptionHeight = 26f;

    // 그림 카드 설명 글자 크기
    public const float ImageCaptionFontSize = 16f;

    // 예·아니요 선택 버튼 하나의 크기
    public static readonly Vector2 ChoiceButtonSize = new Vector2(168f, 44f);

    // 대화창 모서리 반지름
    public const int PanelRadius = 16;

    // 배지·버튼 모서리 반지름
    public const int ChipRadius = 8;

    // 본문 글자 크기
    public const float BodyFontSize = 20f;

    // 배지 글자 크기
    public const float BadgeFontSize = 15f;

    // 버튼 글자 크기
    public const float ButtonFontSize = 17f;

    // 강조 대상의 반지름에 더해 스포트라이트를 넉넉하게 잡는 여유 거리(px)
    public const float SpotlightPadding = 26f;

    // 스포트라이트가 아무리 작아도 유지할 최소 반지름(px)
    public const float SpotlightMinRadius = 58f;

    // 스포트라이트가 화면을 다 덮지 않도록 제한하는 최대 반지름(px)
    public const float SpotlightMaxRadius = 420f;

    // 강조 링을 스포트라이트보다 얼마나 크게 그릴지의 비율
    public const float FocusRingScale = 1.18f;

    // 강조 링이 커졌다 작아지는 맥동 폭 (비율)
    public const float FocusRingPulse = 0.04f;

    // 강조 링 위에 띄우는 지시 화살표의 크기 (피그마 applied-glowing-arrow 64x64)
    public const float ArrowSize = 64f;

    // 강조 링 위쪽 끝과 지시 화살표 사이의 간격
    public const float ArrowGap = 16f;

    // 지시 화살표가 위아래로 흔들리는 거리
    public const float ArrowBob = 8f;

    // 말하는 이 이름 (피그마 speaker-badge 문구)
    public const string SpeakerName = "안내자";

    // 16진수 RGB 값을 불투명 Color로 바꾼다.
    private static Color FromHex(int rgb)
    {
        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f);
    }
}
