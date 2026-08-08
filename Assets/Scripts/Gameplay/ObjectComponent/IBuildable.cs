using System.Collections.Generic;
using UnityEngine;

// 건설 가능한 건물의 정보(아이콘, 모델, 필요 자원)를 제공하는 인터페이스
public interface IBuildable : IIconProvider
{
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
