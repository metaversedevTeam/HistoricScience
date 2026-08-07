// Age 열거형을 UI 표기 문자열로 바꾸는 확장 메서드 모음
public static class AgeExtensions
{
    // 시대를 배지에 쓰는 짧은 한국어 이름으로 변환한다. (예: Paleolithic -> "구석기")
    public static string ToShortName(this Age age) => age switch
    {
        Age.nature => "자연",
        Age.Paleolithic => "구석기",
        Age.Neolithic => "신석기",
        Age.bronzeAge => "청동기",
        _ => age.ToString()
    };

    // 시대를 필터 탭에 쓰는 긴 한국어 이름으로 변환한다. (예: Paleolithic -> "구석기 시대")
    public static string ToTabName(this Age age) => $"{age.ToShortName()} 시대";
}
