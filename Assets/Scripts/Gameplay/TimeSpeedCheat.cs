using UnityEngine;

// 치트 관리 화면에서 조절하는 시간 진행 속도 배율. SkyboxTimeOfDay가 하루 시간을 흘려보낼 때 이 배율을 곱한다.
// MoveSpeedCheat처럼 씬 오브젝트가 아니라 정적 상태로 두어, 씬이 바뀌어도 게임을 끌 때까지 같은 배율이 유지된다.
public static class TimeSpeedCheat
{
    // 슬라이더로 고를 수 있는 최소 배율 (치트 꺼짐과 같다)
    public const float MinMultiplier = 1f;

    // 슬라이더로 고를 수 있는 최대 배율. 기본 하루 길이 300초 기준으로 5초에 하루가 지난다.
    public const float MaxMultiplier = 60f;

    // 지금 적용 중인 시간 진행 속도 배율 (1~60)
    public static float Multiplier
    {
        get => s_multiplier;
        set => s_multiplier = Mathf.Clamp(value, MinMultiplier, MaxMultiplier);
    }

    private static float s_multiplier = MinMultiplier;

    // 도메인 리로드를 끈 에디터 플레이에서도 이전 플레이의 배율이 남지 않도록 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => s_multiplier = MinMultiplier;
}
