using UnityEditor;
using UnityEngine;

// 맵 생성을 거치지 않고도 튜토리얼을 시험해 볼 수 있게 하는 에디터 전용 메뉴.
// 메뉴를 누른 뒤 인게임 씬을 실행하면 그 한 번만 튜토리얼이 시작된다.
public static class TutorialDebugMenu
{
    // 메뉴 경로
    private const string MenuPath = "Tools/튜토리얼/다음 인게임 진입에서 튜토리얼 시작";

    // 다음 인게임 씬 진입에서 튜토리얼이 시작되도록 표시한다.
    [MenuItem(MenuPath)]
    private static void RequestStart()
    {
        SessionState.SetBool(TutorialSession.EditorForceKey, true);
        Debug.Log("튜토리얼: 다음 인게임 씬 진입에서 한 번 시작됩니다.");
    }

    // 메뉴에 지금 켜져 있는지 표시한다.
    [MenuItem(MenuPath, true)]
    private static bool ValidateRequestStart()
    {
        Menu.SetChecked(MenuPath, SessionState.GetBool(TutorialSession.EditorForceKey, false));
        return true;
    }
}
