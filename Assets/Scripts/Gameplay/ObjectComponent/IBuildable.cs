using System.Collections.Generic;
using UnityEngine;

// 건설 가능한 건물의 정보(아이콘, 형태, 필요 자원)를 제공하는 인터페이스
public interface IBuildable : IIconProvider
{
    Mesh BuildingMesh { get; }
    IReadOnlyDictionary<ResourceData, int> BuildCost { get; }
}
