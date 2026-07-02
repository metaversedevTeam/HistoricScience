using System.Collections.Generic;
using UnityEngine;

// 보로노이 다이어그램 정점들의 위치와 가중치를 관리하는 클래스
public class BiomeRegionMap
{
    private readonly BiomeRegion[] m_Regions;

    public int RegionCount => m_Regions.Length;

    // 시드와 파라미터로 보로노이 정점들을 초기화하고 바이옴을 배정한다.
    public BiomeRegionMap(int seed, int regionCount, float minWeight, float maxWeight, MapBiome[] biomes)
    {
        Random.State previousState = Random.state;
        Random.InitState(seed);

        m_Regions = new BiomeRegion[regionCount];
        for (int i = 0; i < regionCount; i++)
        {
            m_Regions[i] = new BiomeRegion
            {
                Index = i,
                Position = new Vector2(Random.value, Random.value),
                Weight = Random.Range(minWeight, maxWeight),
                Biome = biomes[Random.Range(0, biomes.Length)],
            };
        }

        Random.state = previousState;
    }

    // 주어진 위치에서 maxInfluenceDistance 이내의 정점들을 반환한다. 범위 내 정점이 없으면 가장 가까운 정점 하나를 반환한다.
    public BiomeRegion[] GetRegions(Vector2 pos, float maxInfluenceDistance)
    {
        float maxDistSqr = maxInfluenceDistance * maxInfluenceDistance;
        var result = new List<BiomeRegion>();
        int nearestIndex = 0;
        float nearestDistSqr = float.MaxValue;

        for (int i = 0; i < m_Regions.Length; i++)
        {
            float dx = m_Regions[i].Position.x - pos.x;
            float dy = m_Regions[i].Position.y - pos.y;
            float distSqr = dx * dx + dy * dy;

            if (distSqr < nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearestIndex = i;
            }

            if (distSqr <= maxDistSqr)
                result.Add(m_Regions[i]);
        }

        if (result.Count == 0)
            result.Add(m_Regions[nearestIndex]);

        return result.ToArray();
    }

    // 모든 정점을 반환한다.
    public BiomeRegion[] GetAllRegions()
    {
        return m_Regions;
    }
}
