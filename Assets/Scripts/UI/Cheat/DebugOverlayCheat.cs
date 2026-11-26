using UnityEngine;

// 치트 관리 화면의 '디버그 UI' 스위치 상태. 켜면 화면 왼쪽 위에 DebugOverlayUI를 띄우고 끄면 없앤다.
// 다른 치트처럼 게임을 끌 때까지 유지되는 정적 상태이며, 오버레이는 씬이 바뀌어도 남아 있다.
public static class DebugOverlayCheat
{
    // 디버그 UI 치트가 켜져 있는지. 값을 바꾸면 오버레이를 만들거나 없앤다.
    public static bool IsEnabled
    {
        get => s_IsEnabled;
        set
        {
            s_IsEnabled = value;
            ApplyOverlay();
        }
    }

    private static bool s_IsEnabled;

    // 지금 띄워 둔 오버레이. 꺼져 있으면 null이다.
    private static DebugOverlayUI s_Overlay;

    // 켜짐 상태에 맞춰 오버레이를 만들거나 없앤다.
    private static void ApplyOverlay()
    {
        if (s_IsEnabled)
        {
            if (s_Overlay != null) return;

            var overlayObject = new GameObject("DebugOverlay");
            Object.DontDestroyOnLoad(overlayObject);
            s_Overlay = overlayObject.AddComponent<DebugOverlayUI>();
            return;
        }

        if (s_Overlay != null)
            Object.Destroy(s_Overlay.gameObject);

        s_Overlay = null;
    }

    // 도메인 리로드를 끈 에디터 플레이에서도 이전 플레이의 상태가 남지 않도록 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        s_IsEnabled = false;
        s_Overlay = null;
    }
}
