using UnityEngine;
using UnityEngine.SceneManagement;

// 튜토리얼의 유일한 진입점.
// 바깥 코드가 알아야 하는 것은 "맵을 새로 만들었다"는 사실 하나뿐이며, 그 뒤의 모든 진행은 Tutorial 폴더 안에서만 일어난다.
// 튜토리얼을 걷어낼 때는 Assets/Scripts/Tutorial 폴더를 통째로 지우고,
// MapManagementUI에서 MarkNewMapCreated를 부르는 한 줄만 지우면 된다.
public static class TutorialSession
{
    // 에디터에서 맵 생성을 거치지 않고 튜토리얼을 시험해 보기 위한 세션 플래그 키
    public const string EditorForceKey = "HistoricScience.Tutorial.ForceStart";

    // 이번 인게임 진입이 맵을 새로 만든 세션인지 여부. 이미 있던 맵을 불러온 경우에는 false로 남는다.
    private static bool _isNewMapSession;

    // 맵을 새로 만들었음을 알린다. 인게임 씬으로 넘어가기 직전에 한 번 호출한다.
    public static void MarkNewMapCreated()
    {
        _isNewMapSession = true;
    }

    // 이번에 이미 있던 맵을 불러왔음을 알린다. 앞서 만든 맵의 플래그가 남아 튜토리얼이 다시 뜨지 않게 한다.
    public static void MarkExistingMapLoaded()
    {
        _isNewMapSession = false;
    }

    // 플레이 모드에 들어갈 때마다 상태를 초기 상태로 되돌리고 씬 로드를 지켜보기 시작한다.
    // (도메인 리로드를 꺼 둔 에디터에서도 이전 실행의 값이 남지 않게 한다)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void HandleInitialize()
    {
        _isNewMapSession = false;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    // 인게임 씬이 열릴 때 맵을 새로 만든 세션이면 튜토리얼 실행기를 만들어 붙인다. 플래그는 한 번 쓰고 지운다.
    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool shouldStart = _isNewMapSession || ConsumeEditorForceFlag();
        _isNewMapSession = false;

        if (!shouldStart) return;

        // 인게임 씬이 아니면 튜토리얼이 볼 것이 없다.
        if (Object.FindFirstObjectByType<IngameSceneManager>() == null) return;

        new GameObject(nameof(TutorialRunner)).AddComponent<TutorialRunner>();
    }

    // 에디터 메뉴로 켜 둔 강제 시작 플래그를 읽고 지운다. 빌드에서는 항상 false다.
    private static bool ConsumeEditorForceFlag()
    {
#if UNITY_EDITOR
        if (!UnityEditor.SessionState.GetBool(EditorForceKey, false)) return false;

        UnityEditor.SessionState.SetBool(EditorForceKey, false);
        return true;
#else
        return false;
#endif
    }
}
