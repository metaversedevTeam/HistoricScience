// 치트 관리 UI가 다루는 치트의 종류. 스위치 한 줄이 이 값 하나에 대응한다.
public enum CheatKind
{
    // 모든 자원이 무한한 것처럼 동작한다. 언제든 껐다 켤 수 있다.
    InfiniteResources,

    // 도감의 모든 아이템과 조합법 힌트를 해금한다. 되돌릴 수 없는 일회성 치트다.
    UnlockAllItems,

    // 모든 연구를 비용 없이 완료 처리한다. 되돌릴 수 없는 일회성 치트다.
    UnlockAllResearch,
}
