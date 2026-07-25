using UnityEngine;

// 일터(WorkPlace)에 등록되어 일할 수 있는 유닛이 구현하는 인터페이스. 등록되면 JSON으로 접혀 보관되고 해제되면 새 인스턴스로 복원된다.
public interface IWorker : ISavable
{
    // 현재 소속된 일터. 소속되지 않았으면 null. 파괴는 프레임 끝에 처리되므로, 등록 직후 다른 일터에 중복 등록되는 것을 막는 데 쓴다.
    WorkPlace CurrentWorkPlace { get; }

    // 일터에 등록되기 직전 호출. 진행 중인 명령·선택·이벤트 구독을 정리한다. 이 호출 직후의 상태가 JSON으로 캡처된다. (파괴는 WorkPlace가 담당)
    void OnEnterWorkPlace(WorkPlace workPlace);

    // 일터에서 해제되어 새 인스턴스로 복원된 직후 호출. 위치 배치는 WorkPlace가 이미 마친 상태다.
    void OnExitWorkPlace();
}
