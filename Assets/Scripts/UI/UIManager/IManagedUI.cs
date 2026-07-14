using System;

// 관리형 UI의 수명주기 상태
public enum UIState
{
    // 닫혀 있음 (풀 대기 상태 포함)
    Closed,

    // 열기 연출 진행 중
    Opening,

    // 완전히 열림
    Open,

    // 닫기 연출 진행 중
    Closing,
}

// UIManager가 수명주기(풀링 포함)를 관리하는 캔버스 UI의 공통 계약 (닫기·풀 반납).
// 여는 방법은 페이로드 유무에 따라 IOpenableUI / IOpenableUI<TData>로 나뉜다.
// 상태는 Closed → Opening → Open → Closing → Closed 순서로만 전이되며,
// OnFinishClose는 한 사이클(열림→닫힘)당 정확히 1회 발행되어야 한다.
public interface IManagedUI
{
    // UI 닫기 완료 시 풀 반납을 위해 발행되는 이벤트 (발신자 전달)
    public event Action<IManagedUI> OnFinishClose;

    // 현재 수명주기 상태
    public UIState State { get; }

    // 닫기 연출을 시작하고, 완료 시 OnFinishClose를 발행한다. immediate가 true면 연출 없이 즉시 닫는다 (씬 전환 등 일괄 정리용).
    // 재진입 규칙 — Closed: 무시 / Closing: immediate가 true일 때만 남은 연출을 건너뛰고 즉시 완료 / Opening: 열기 연출을 중단하고 닫기로 전환.
    public void Close(bool immediate = false);
}
