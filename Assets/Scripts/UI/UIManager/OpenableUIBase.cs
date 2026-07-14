// 페이로드 없이 열리는 관리형 UI의 추상 베이스 — Open을 명시적 구현으로 봉인해 UIManager 전용 규약을 강제한다
public abstract class OpenableUIBase : ManagedUIBase, IOpenableUI
{
    // UIManager 전용 열기 진입점 — 자신을 활성화하고 열기 연출을 시작한다
    void IOpenableUI.Open()
    {
        if (!TryBeginOpen())
        {
            return;
        }

        PlayOpenTransition();
    }
}

// TData 페이로드를 받아야만 열리는 관리형 UI의 추상 베이스 — Open을 명시적 구현으로 봉인해 UIManager 전용 규약을 강제한다
public abstract class OpenableUIBase<TData> : ManagedUIBase, IOpenableUI<TData>
{
    // UIManager 전용 열기 진입점 — 데이터를 주입한 뒤 자신을 활성화하고 열기 연출을 시작한다
    void IOpenableUI<TData>.Open(TData data)
    {
        if (!TryBeginOpen())
        {
            return;
        }

        ApplyData(data);
        PlayOpenTransition();
    }

    // 주입받은 페이로드로 UI 내용을 초기화하는 훅
    protected abstract void ApplyData(TData data);
}
