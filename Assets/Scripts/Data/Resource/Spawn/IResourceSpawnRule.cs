using System.Collections.Generic;
using UnityEngine;

// 자원 소스를 청크 어디에 몇 개 놓을지 결정하는 소환 방식(소환 공식)
public interface IResourceSpawnRule
{
    // 이 소환 방식이 맵에 소환할 자원 소스 프리팹
    GameObject SourcePrefab { get; }

    // 주어진 청크 정보로 자원 소스가 놓일 자리들을 계산해 결과 목록에 채운다.
    void GetPlacements(in ResourceSpawnContext context, List<ResourceSpawnPlacement> results);
}
