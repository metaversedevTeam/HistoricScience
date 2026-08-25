using System;

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

    // 도감 기준으로 바로 앞 시대를 구한다. 시대 구분 대상이 아닌 자연 자원은 앞 시대로 치지 않는다.
    public static bool TryGetPreviousAge(this Age age, out Age previous)
    {
        bool found = false;
        previous = default;

        foreach (Age candidate in Enum.GetValues(typeof(Age)))
        {
            if (candidate == Age.nature) continue;
            if (candidate >= age) continue;
            if (found && candidate <= previous) continue;

            previous = candidate;
            found = true;
        }

        return found;
    }
}
