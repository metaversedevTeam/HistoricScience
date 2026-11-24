using UnityEngine;

// 치트 관리 화면의 '벽 무시' 스위치 상태. 켜져 있으면 GroundMover가 WASD 이동 중 걸을 수 없는 지형과 내브메시 경계를 무시하고,
// 지형이 로딩된 청크 안이라면 어디든 지나갈 수 있다. 다른 이동 치트처럼 게임을 끌 때까지 유지되는 정적 상태다.
public static class WallIgnoreCheat
{
    // 벽 무시 치트가 켜져 있는지
    public static bool IsEnabled { get; set; }

    // 도메인 리로드를 끈 에디터 플레이에서도 이전 플레이의 상태가 남지 않도록 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => IsEnabled = false;
}
