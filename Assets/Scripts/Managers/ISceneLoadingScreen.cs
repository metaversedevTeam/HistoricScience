// 씬을 넘어 이어지는 로딩 화면. 이전 씬이 넘겨 두면, 다음 씬이 자기 로딩 진행도를 알리고 다 끝나면 걷어 낸다.
public interface ISceneLoadingScreen
{
    // 다음 씬 쪽의 로딩 진행도(0~1)를 알린다.
    void SetProgress(float progress);

    // 다음 씬의 로딩이 끝났으므로 화면을 걷어 낸다.
    void Finish();
}
