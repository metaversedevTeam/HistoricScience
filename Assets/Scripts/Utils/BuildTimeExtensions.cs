using UnityEngine;

// 건설 시간(초)을 UI 표기 문자열로 바꾸는 확장 메서드 모음
public static class BuildTimeExtensions
{
    // 초 단위 건설 시간을 "분:초" 표기로 변환한다. (예: 150 -> "2:30")
    public static string ToBuildTimeText(this float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{totalSeconds / 60}:{totalSeconds % 60:D2}";
    }
}
