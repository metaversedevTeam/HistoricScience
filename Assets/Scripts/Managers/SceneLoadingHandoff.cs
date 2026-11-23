// 씬 전환 사이에 로딩 화면을 맡기고 꺼내 가는 보관소. 이전 씬이 맡기고, 다음 씬이 진입하면서 꺼내 간다.
public static class SceneLoadingHandoff
{
    // 다음 씬이 꺼내 가기를 기다리는 로딩 화면
    private static ISceneLoadingScreen _pending;

    // 다음 씬에 넘겨줄 로딩 화면을 맡긴다.
    public static void Hand(ISceneLoadingScreen screen)
    {
        _pending = screen;
    }

    // 맡겨진 로딩 화면을 꺼내 간다. 맡겨진 것이 없거나 이미 파괴되었으면 null을 돌려준다.
    public static ISceneLoadingScreen Take()
    {
        ISceneLoadingScreen screen = _pending;
        _pending = null;

        if (screen is UnityEngine.Object unityObject && unityObject == null)
            return null;

        return screen;
    }

    // 맡겨 둔 화면을 아무도 꺼내 가지 않았으면 도로 거둬들이고 true를 돌려준다.
    public static bool TryRevoke(ISceneLoadingScreen screen)
    {
        if (!ReferenceEquals(_pending, screen))
            return false;

        _pending = null;
        return true;
    }
}
