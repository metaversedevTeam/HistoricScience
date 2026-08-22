using System.Collections.Generic;
using UnityEngine;

// 건설 가능한 건물의 정보(이름, 설명, 아이콘, 모델, 필요 자원, 건설 시간)를 제공하는 인터페이스
public interface IBuildable : IIconProvider
{
    // 건물 선택 UI의 카드와 상세 패널에 표시할 건물 이름
    string BuildingName { get; }

    // 건물 선택 UI의 상세 패널에 표시할 건물 설명
    string Description { get; }

    // 건설에 걸리는 시간(초). 건물 선택 UI에서 분:초 형식으로 표시한다.
    float BuildTime { get; }

    // 배치 홀로그램으로 소환할 모델 역할의 게임 오브젝트
    GameObject BuildingModel { get; }

    IReadOnlyDictionary<ResourceData, int> BuildCost { get; }
}

// 건설 비용 하나(자원 종류·수량)를 인스펙터에서 지정하기 위한 직렬화 항목
[System.Serializable]
public struct BuildCostEntry
{
    public ResourceData Resource;
    public int Count;
}
