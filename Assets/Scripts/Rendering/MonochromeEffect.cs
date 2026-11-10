using UnityEngine;

// 흑백 화면 효과의 전역 셰이더 값을 한곳에서 읽고 쓰는 도우미
public static class MonochromeEffect
{
    private static readonly int IntensityId = Shader.PropertyToID("_MonochromeIntensity");

    // 흑백 강도가 0보다 커서 렌더 패스를 실행해야 하는지 여부
    public static bool IsActive => Shader.GetGlobalFloat(IntensityId) > 0f;

    // 전역 흑백 강도(0 = 원래 색, 1 = 완전 흑백)를 설정한다
    public static void SetIntensity(float intensity)
    {
        Shader.SetGlobalFloat(IntensityId, Mathf.Clamp01(intensity));
    }
}
