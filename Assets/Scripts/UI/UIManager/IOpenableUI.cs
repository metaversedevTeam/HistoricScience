// Open은 UIManager 전용이므로 반드시 명시적 인터페이스 구현(void IOpenableUI.Open())으로 구현할 것.
// 그러면 구체 타입 참조에서는 Open이 보이지 않아, 인터페이스 참조로 호출하는 UIManager만 열 수 있다.
// 직접 구현하는 대신 OpenableUIBase / OpenableUIBase<TData>를 상속하면 이 규약과 상태 전이가 자동으로 지켜진다.

// 페이로드 없이 열리는 관리형 UI의 계약
public interface IOpenableUI : IManagedUI
{
    // UIManager 전용, 명시적 구현 필수 — 다른 코드에서는 UIManager를 통해 열어야 한다.
    // 자신을 활성화하고 열기 연출 시작. State가 Closed일 때만 유효하며 그 외 상태에서는 무시된다.
    public void Open();
}

// TData 페이로드를 받아야만 열리는 관리형 UI의 계약
public interface IOpenableUI<TData> : IManagedUI
{
    // UIManager 전용, 명시적 구현 필수 — 다른 코드에서는 UIManager를 통해 열어야 한다.
    // 전달받은 데이터로 자신을 초기화한 뒤 활성화하고 열기 연출 시작. State가 Closed일 때만 유효하며 그 외 상태에서는 무시된다.
    public void Open(TData data);
}
