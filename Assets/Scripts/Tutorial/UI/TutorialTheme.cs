using UnityEngine;

// 튜토리얼 UI의 색과 치수 모음. 메인 메뉴·일시정지 화면과 같은 사각형 모션그래픽 디자인
// (모서리 없는 어두운 판 + 얇은 테두리 + 왼쪽 강조 막대 + 번호·영문 캡션 + 작은 사각 표식)을 따른다.
// 튜토리얼은 프리팹 없이 코드로 화면을 만들기 때문에, 디자인 값을 이 한 곳에만 모아 둔다.
public static class TutorialTheme
{
    // 대화창·버튼 본체의 배경색 (일시정지 화면 메뉴 칸과 같은 값)
    public static readonly Color PanelFill = FromHex(0x0D0D1C, 0.85f);

    // 판 테두리와 구분선 색
    public static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.12f);

    // 대사를 읽는 중을 나타내는 하늘색 강조색 (일시정지 화면 01 이어하기)
    public static readonly Color Cyan = FromHex(0x38BDF8);

    // 질문에 답하는 중을 나타내는 초록 강조색 (일시정지 화면 02 저장 후 나가기)
    public static readonly Color Green = FromHex(0x4ADE80);

    // 목표를 수행하는 중을 나타내는 주황 강조색 (일시정지 화면 04 메뉴로 나가기)
    public static readonly Color Orange = FromHex(0xFB923C);

    // 제목·본문 글자색
    public static readonly Color TitleText = FromHex(0xEBF0FF);

    // 보조 설명·키 안내 글자색
    public static readonly Color MutedText = FromHex(0x94A3B8);

    // 영문 캡션 글자색
    public static readonly Color CaptionText = FromHex(0x94A3B8, 0.7f);

    // 판 테두리 두께
    public const float BorderThickness = 2f;

    // 판 왼쪽 강조 막대의 폭
    public const float AccentBarWidth = 6f;

    // 대화창 본체 크기
    public static readonly Vector2 DialogueSize = new Vector2(960f, 196f);

    // 예·아니요 버튼이 함께 뜰 때의 대화창 높이. 버튼이 본문 글자를 덮지 않도록 그만큼 키운다.
    public const float DialogueQuestionHeight = 264f;

    // 대화창을 화면 아래에서 띄울 거리
    public const float DialogueBottomMargin = 72f;

    // 화면 가장자리에서 대화창을 띄울 여백 (위쪽에 붙을 때)
    public const float ScreenMargin = 48f;

    // 대화창 안쪽 좌우 여백
    public const float ContentPadding = 36f;

    // 대화창 맨 위 세 색 막대의 높이와 각 막대 폭 (일시정지 화면 Accent-Bars를 대화창 크기에 맞게 줄인 값)
    public const float TriBarHeight = 6f;
    public static readonly Vector3 TriBarWidths = new Vector3(44f, 20f, 8f);

    // 막대 사이 간격
    public const float TriBarGap = 7f;

    // 머리글 줄(말하는 이·단계 캡션)의 위쪽 여백과 높이
    public const float HeaderTop = 38f;
    public const float HeaderHeight = 34f;

    // 머리글 아래 구분선 위치(위쪽 끝 기준)
    public const float DividerTop = 80f;

    // 본문 위치(위쪽 끝 기준)와 높이
    public const float BodyTop = 94f;
    public const float BodyHeight = 64f;

    // 아래쪽 키 안내 줄의 아래 여백과 높이
    public const float FooterBottom = 18f;
    public const float FooterHeight = 24f;

    // 사각 표식 크기 (일시정지 화면 Marker 14를 줄인 값)
    public const float MarkerSize = 10f;

    // 예·아니요 선택 버튼 하나의 크기와 버튼 사이 간격
    public static readonly Vector2 ChoiceButtonSize = new Vector2(300f, 60f);
    public const float ChoiceGap = 16f;

    // 대화창 옆에 붙는 그림 카드의 크기
    public static readonly Vector2 ImageCardSize = new Vector2(232f, 264f);

    // 그림 카드와 대화창 사이의 간격
    public const float ImageCardGap = 16f;

    // 그림 카드 안쪽 여백
    public const float ImageCardPadding = 18f;

    // 그림 카드 머리 캡션 두 줄(영문·한글)이 차지하는 높이
    public const float ImageCaptionHeight = 58f;

    // 그림 카드 모서리 표식 크기 (일시정지 화면 Emblem CornerA·B)
    public const float CornerMarkerSize = 12f;

    // 말하는 이 이름 글자 크기 (BMHANNA)
    public const float SpeakerFontSize = 28f;

    // 본문 글자 크기 (BMHANNA)
    public const float BodyFontSize = 24f;

    // 버튼 글자 크기 (BMHANNA)
    public const float ButtonFontSize = 26f;

    // 버튼 번호 글자 크기 (영문, 일시정지 화면 Index 22)
    public const float IndexFontSize = 22f;

    // 그림 카드 한글 설명 글자 크기 (BMHANNA)
    public const float ImageCaptionFontSize = 18f;

    // 영문 캡션·키 안내 글자 크기와 자간 (일시정지 화면 Caption·Hint)
    public const float CaptionFontSize = 15f;
    public const float CaptionSpacing = 6f;

    // 번호 글자 자간 (일시정지 화면 Index)
    public const float IndexSpacing = 8f;

    // 내용이 바뀔 때 본문이 떠오르는 데 걸리는 시간(초)과 떠오르는 거리
    public const float BodyRevealDuration = 0.18f;
    public const float BodyRevealShift = 10f;

    // 말하는 이 이름
    public const string SpeakerName = "안내자";

    // 16진수 RGB 값과 투명도로 Color를 만든다.
    private static Color FromHex(int rgb, float alpha = 1f)
    {
        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            alpha);
    }
}
