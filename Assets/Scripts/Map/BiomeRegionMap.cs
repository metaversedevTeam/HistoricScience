using System.Collections.Generic;
using UnityEngine;

// 무한 평면 위의 보로노이 바이옴 정점들을 정의된 생성 규칙에 따라 계산해 제공하는 클래스. 정점을 저장하지 않고 조회할 때마다 규칙으로 다시 계산한다. (캐싱은 추후 도입 예정)
public class BiomeRegionMap
{
    // (임시) 임시 랜덤 생성 규칙이 만들어낼 정점 개수
    private const int k_TempRegionCount = 30;

    private readonly int m_Seed;
    private readonly float m_MinWeight;
    private readonly float m_MaxWeight;
    private readonly MapBiome[] m_Biomes;

    // 시드와 (임시) 랜덤 생성 규칙의 파라미터로 맵을 초기화한다.
    public BiomeRegionMap(int seed, float minWeight, float maxWeight, MapBiome[] biomes)
    {
        m_Seed = seed;
        m_MinWeight = minWeight;
        m_MaxWeight = maxWeight;
        m_Biomes = biomes;
    }

    // 주어진 위치에서 maxInfluenceDistance 이내의 정점들을 계산해 반환한다. 범위 내 정점이 없으면 빈 배열을 반환한다.
    public BiomeRegion[] GetRegions(Vector2 pos, float maxInfluenceDistance)
    {
        // 원을 감싸는 외접 사각형으로 먼저 후보를 좁힌 뒤, 실제 원형 거리로 다시 걸러낸다.
        var boundingArea = new Rect(pos.x - maxInfluenceDistance, pos.y - maxInfluenceDistance, maxInfluenceDistance * 2f, maxInfluenceDistance * 2f);
        BiomeRegion[] candidates = GetRegions(boundingArea);

        float maxDistSqr = maxInfluenceDistance * maxInfluenceDistance;
        var result = new List<BiomeRegion>();

        for (int i = 0; i < candidates.Length; i++)
        {
            float dx = candidates[i].Position.x - pos.x;
            float dy = candidates[i].Position.y - pos.y;
            float distSqr = dx * dx + dy * dy;

            if (distSqr <= maxDistSqr)
                result.Add(candidates[i]);
        }

        return result.ToArray();
    }

    // 주어진 사각형 영역 내의 정점들을 생성 규칙에 따라 계산해 반환한다. 영역 내 정점이 없으면 빈 배열을 반환한다.
    public BiomeRegion[] GetRegions(Rect area)
    {
        BiomeRegion[] regions = HandleSpawnRandomRegions();
        var result = new List<BiomeRegion>();

        for (int i = 0; i < regions.Length; i++)
        {
            if (area.Contains(regions[i].Position))
                result.Add(regions[i]);
        }

        return result.ToArray();
    }

    // (임시) 시드 기반 랜덤 위치의 정점 목록을 계산한다. 정식 생성 규칙이 별도로 구현되면 대체될 예정이다.
    private BiomeRegion[] HandleSpawnRandomRegions()
    {
        Random.State previousState = Random.state;
        Random.InitState(m_Seed);

        var regions = new BiomeRegion[k_TempRegionCount];
        for (int i = 0; i < k_TempRegionCount; i++)
        {
            regions[i] = new BiomeRegion
            {
                Index = i,
                Position = new Vector2(Random.value / 2f + 0.25f, Random.value / 2f + 0.25f),
                Weight = Random.Range(m_MinWeight, m_MaxWeight),
                Biome = m_Biomes[Random.Range(0, m_Biomes.Length)],
            };
        }

        Random.state = previousState;
        return regions;
    }
}
