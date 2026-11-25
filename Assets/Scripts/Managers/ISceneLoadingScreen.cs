using System;

// 씬을 넘어 이어지는 로딩 화면. 이전 씬이 넘겨 두면, 다음 씬이 자기 로딩 진행도를 알리고 다 끝나면 걷어 낸다.
// 플레이어가 로딩 취소를 누르면 다음 씬이 로딩을 멈춘 뒤, 화면에 돌아갈 씬으로의 전환을 맡긴다.
public interface ISceneLoadingScreen
{
    // 플레이어가 로딩 취소를 눌렀을 때 알린다. 다음 씬이 화면을 꺼내 가기 전에 눌렀다면 IsCancelRequested로 확인한다.
    event Action CancelRequested;

    // 플레이어가 이미 로딩 취소를 눌렀는지 여부
    bool IsCancelRequested { get; }

    // 다음 씬 쪽의 로딩 진행도(0~1)를 알린다.
    void SetProgress(float progress);

    // 다음 씬의 로딩이 끝났으므로 화면을 걷어 낸다.
    void Finish();

    // 로딩이 취소되었으므로 화면을 걷지 않은 채 지정한 씬(메인 메뉴)으로 되돌아간다.
    void ReturnTo(string sceneName);
}
