using System.Collections.Generic;
using UnityEngine;

// 청크 한 개분의 자원 소환 계획. 메인 스레드에서 소환 방식과 청크 크기를 담아 만들고, 백그라운드 스레드에서 자리를 계산한 뒤, 메인 스레드에서 그 자리에 소환한다.
// 소환 방식 유효성 검사(프리팹 null 비교)처럼 메인 스레드에서만 할 수 있는 일은 만들 때 끝내 두므로, 자리 계산은 Unity 씬 API를 쓰지 않는다.
public class ChunkResourcePlan
{
    // 소환에 참여하는 유효한 소환 방식들. 만들 때 메인 스레드에서 걸러 둔다.
    private readonly ResourceSpawnRule[] m_Rules;
    // 이 청크의 월드 크기(y는 터레인 최대 높이). 메인 스레드에서 터레인으로부터 읽어 둔 값이다.
    private readonly Vector3 m_ChunkSize;
    // 소환 방식별로 계산된 자리 목록. m_Rules와 같은 순서이며, 자리를 계산하기 전에는 null이다.
    private List<ResourceSpawnPlacement>[] m_Placements;

    // 유효한 소환 방식 목록과 청크 크기로 아직 자리를 계산하지 않은 계획을 만든다. 메인 스레드에서 호출해야 한다.
    public ChunkResourcePlan(ResourceSpawnRule[] rules, Vector3 chunkSize)
    {
        m_Rules = rules;
        m_ChunkSize = chunkSize;
    }

    // 자리 계산이 끝나 소환할 준비가 되었는지 여부
    public bool IsComputed => m_Placements != null;

    // 계획에 참여하는 소환 방식 개수
    public int RuleCount => m_Rules.Length;

    // index번째 소환 방식이 소환할 자원 소스 프리팹을 반환한다.
    public GameObject GetPrefab(int index)
    {
        return m_Rules[index].SourcePrefab;
    }

    // index번째 소환 방식이 계산한 자리 목록을 반환한다. 자리를 계산하기 전에는 호출하면 안 된다.
    public IReadOnlyList<ResourceSpawnPlacement> GetPlacements(int index)
    {
        return m_Placements[index];
    }

    // 모든 소환 방식의 소환 자리를 계산해 보관한다. 터레인을 건드리지 않는 순수 계산이라 백그라운드 스레드에서 호출해도 안전하다.
    // regionField는 이 청크를 굽는 데 쓴 보로노이 정점 필드로, 소환 자리 판정이 같은 정점을 다시 만들지 않게 한다. null이어도 결과는 같다.
    public void ComputePlacements(Vector2 mapViewOrigin, float mapViewSize, MapData mapData, ChunkRegionField regionField)
    {
        ResourceSpawnContext context = new ResourceSpawnContext(mapViewOrigin, mapViewSize, mapData, m_ChunkSize, regionField);
        List<ResourceSpawnPlacement>[] placements = new List<ResourceSpawnPlacement>[m_Rules.Length];

        // 소환 방식마다 목록을 따로 만들어 소환 방식 사이에 상태를 공유하지 않는다.
        for (int i = 0; i < m_Rules.Length; i++)
        {
            placements[i] = new List<ResourceSpawnPlacement>();
            m_Rules[i].GetPlacements(context, placements[i]);
        }

        m_Placements = placements;
    }
}
