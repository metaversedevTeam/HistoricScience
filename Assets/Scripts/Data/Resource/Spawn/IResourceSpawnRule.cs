using System.Collections.Generic;

// 아이템의 자원 소스를 청크 어디에 몇 개 놓을지 결정하는 소환 방식(소환 공식)
public interface IResourceSpawnRule
{
    // 주어진 청크 정보로 자원 소스가 놓일 자리들을 계산해 결과 목록에 채운다.
    void GetPlacements(ItemData item, in ResourceSpawnContext context, List<ResourceSpawnPlacement> results);
}
