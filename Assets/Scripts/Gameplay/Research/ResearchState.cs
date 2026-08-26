// 연구 하나가 지금 어떤 상태인지 나타내는 열거형. 카드와 상세 패널의 표시·버튼 동작을 이 값으로 결정한다.
public enum ResearchState
{
    // 시대 제한에 걸려 아직 볼 수만 있는 상태
    AgeLocked,

    // 선행 연구가 끝나지 않아 연구할 수 없는 상태
    PrerequisiteLocked,

    // 지금 연구할 수 있는 상태 (자원이 모자라면 연구만 막힌다)
    Available,

    // 이미 끝난 상태
    Completed,
}
