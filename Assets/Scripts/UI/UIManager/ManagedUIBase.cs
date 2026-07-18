using System;
using UnityEngine;

// 관리형 UI의 상태 전이, 닫기 이벤트 발행, 중복 호출 가드를 공통 구현하는 추상 베이스.
// 열기 진입점까지 필요한 UI는 이 클래스 대신 OpenableUIBase / OpenableUIBase<TData>를 상속할 것.
public abstract class ManagedUIBase : MonoBehaviour, IManagedUI
{
    public event Action<IManagedUI> OnStartClose;

    public event Action<IManagedUI> OnFinishClose;

    public UIState State { get; private set; } = UIState.Closed;

    // IManagedUI.Close 구현 — 상태별 재진입 규칙은 IManagedUI의 계약 주석 참고
    public void Close(bool immediate = false)
    {
        switch (State)
        {
            case UIState.Closed:
                return;

            case UIState.Closing:
                // 이미 닫히는 중이므로 immediate 요청만 남은 연출을 건너뛰고 즉시 완료한다
                if (immediate)
                {
                    StopTransition();
                    FinishClose();
                }
                return;

            case UIState.Opening:
                // 열기 연출을 중단하고 닫기로 전환한다
                StopTransition();
                break;

            case UIState.Open:
                break;
        }

        State = UIState.Closing;
        OnStartClose?.Invoke(this);

        if (immediate)
        {
            FinishClose();
        }
        else
        {
            PlayCloseTransition();
        }
    }

    // 열기 상태 전이 공통 처리 — Closed일 때만 자신을 활성화하고 Opening으로 전환한 뒤 true를 반환한다
    protected bool TryBeginOpen()
    {
        if (State != UIState.Closed)
        {
            Debug.LogWarning($"[{name}] State가 {State}인 UI에 대한 Open 요청은 무시된다.", this);
            return false;
        }

        State = UIState.Opening;
        gameObject.SetActive(true);
        return true;
    }

    // 열기 연출 완료 처리 — 파생 클래스의 열기 연출이 끝나는 시점에 호출할 것 (Opening이 아닐 때의 호출은 무시)
    protected void FinishOpen()
    {
        if (State != UIState.Opening)
        {
            return;
        }

        State = UIState.Open;
    }

    // 닫기 완료 처리 — 비활성화 후 반납 정리 훅을 거쳐 OnFinishClose를 1회 발행한다 (Closing이 아닐 때의 호출은 무시)
    protected void FinishClose()
    {
        if (State != UIState.Closing)
        {
            return;
        }

        State = UIState.Closed;
        gameObject.SetActive(false);
        OnReturnToPool();
        OnFinishClose?.Invoke(this);
    }

    // 풀 반납 직전 정리 훅 — 재사용 시 이전 상태(입력값, 스크롤 위치, 임시 구독 등)가 남지 않도록 오버라이드
    protected virtual void OnReturnToPool()
    {
    }

    // 열기 연출 시작 훅 — 연출이 끝나면 반드시 FinishOpen을 호출할 것 (기본 구현은 연출 없이 즉시 완료)
    protected virtual void PlayOpenTransition()
    {
        FinishOpen();
    }

    // 닫기 연출 시작 훅 — 연출이 끝나면 반드시 FinishClose를 호출할 것 (기본 구현은 연출 없이 즉시 완료)
    protected virtual void PlayCloseTransition()
    {
        FinishClose();
    }

    // 진행 중인 열기·닫기 연출 중단 훅 — 연출 코루틴이나 트윈을 사용하는 파생 클래스에서 정리용으로 오버라이드
    protected virtual void StopTransition()
    {
    }
}
