using System.Collections.Generic;
using UnityEngine;

// 건설 가능한 건물의 정보(아이콘, 형태, 필요 자원)를 제공하는 인터페이스
public interface IBuildable : IIconProvider
{
    Mesh BuildingMesh { get; }
    IReadOnlyDictionary<ResourceData, int> BuildCost { get; }
}

// 건설 비용 하나(자원 종류·수량)를 인스펙터에서 지정하기 위한 직렬화 항목
[System.Serializable]
public struct BuildCostEntry
{
    public ResourceData Resource;
    public int Count;
}
