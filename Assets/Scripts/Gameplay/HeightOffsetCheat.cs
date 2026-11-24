using UnityEngine;

// 치트 관리 화면에서 조절하는 Y오프셋. 모든 GroundMover가 땅 높이보다 이 값만큼 위에 떠서 움직인다.
// 최댓값이면 땅 높이와 상관없이 월드 Y가 최댓값으로 고정된다. MoveSpeedCheat처럼 게임을 끌 때까지 유지되는 정적 상태이며,
// 땅 높이가 매 프레임 바뀌므로 GroundMover가 이벤트 대신 매 프레임 이 값을 읽어 적용한다.
public static class HeightOffsetCheat
{
    // 슬라이더로 고를 수 있는 최소 오프셋 (치트 꺼짐과 같다)
    public const float MinOffset = 0f;

    // 슬라이더로 고를 수 있는 최대 오프셋. 이 값이면 땅 높이 대신 월드 Y 이 값에 고정된다.
    public const float MaxOffset = 500f;

    // 지금 적용 중인 Y오프셋 (0~500)
    public static float Offset
    {
        get => s_offset;
        set => s_offset = Mathf.Clamp(value, MinOffset, MaxOffset);
    }

    // 땅 높이를 무시하고 월드 Y를 MaxOffset에 고정하는 상태인지
    public static bool IsPinnedToMax => s_offset >= MaxOffset;

    private static float s_offset = MinOffset;

    // 도메인 리로드를 끈 에디터 플레이에서도 이전 플레이의 오프셋이 남지 않도록 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => s_offset = MinOffset;
}
