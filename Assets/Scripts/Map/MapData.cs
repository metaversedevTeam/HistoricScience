using UnityEngine;

// 보로노이 다이어그램을 기반으로 맵의 바이옴 정보를 계산하는 클래스
public class MapData
{
    private readonly BiomeRegionMap m_RegionMap;
    // 경계선에 적용할 노이즈의 스케일. 클수록 더 잘게, 자주 굴곡진 경계가 만들어진다.
    private readonly float m_BoundaryNoiseScale;
    // 경계선에 적용할 노이즈의 세기. 0이면 원래의 가중 보로노이 경계(원호) 그대로 유지된다.
    private readonly float m_BoundaryNoiseStrength;
    // 이 거리보다 멀리 있는 정점은 영향력 계산에서 제외된다.
    private readonly float m_MaxInfluenceDistance;
    // maxInfluenceDistance 이내에 정점이 하나도 없을 때 대신 사용할 바이옴.
    private readonly MapBiome m_DefaultBiome;

    // 주어진 시드와 바이옴 목록으로 바이옴 영역 맵을 생성한다.
    public MapData(int seed, MapBiome[] biomes, MapBiome defaultBiome, float minWeight = 0.5f, float maxWeight = 2f, float boundaryNoiseScale = 4f, float boundaryNoiseStrength = 0.15f, float maxInfluenceDistance = 0.6f)
    {
        m_BoundaryNoiseScale = boundaryNoiseScale;
        m_BoundaryNoiseStrength = boundaryNoiseStrength;
        m_MaxInfluenceDistance = maxInfluenceDistance;
        m_DefaultBiome = defaultBiome;

        m_RegionMap = new BiomeRegionMap(seed, minWeight, maxWeight, biomes);
    }

    // 0~1로 정규화된 좌표를 기준으로 가장 가까운(가중치 적용) 바이옴을 반환한다. 범위 내 정점이 없으면 기본 바이옴을 반환한다.
    public MapBiome GetBiome(Vector2 position)
    {
        BiomeRegion[] candidates = m_RegionMap.GetRegions(position, m_MaxInfluenceDistance);
        if (candidates.Length == 0)
            return m_DefaultBiome;

        return HandleFindNearestRegion(position, candidates).Biome;
    }

    // 기즈모 표시 등 외부에서 사용할 수 있도록 주어진 사각형 영역 내의 바이옴 정점 목록을 반환한다.
    public BiomeRegion[] GetRegions(Rect area)
    {
        return m_RegionMap.GetRegions(area);
    }

    // 후보 정점들 중 가중치 거리(+ 노이즈 보정)가 가장 가까운 정점을 반환한다.
    private BiomeRegion HandleFindNearestRegion(Vector2 position, BiomeRegion[] candidates)
    {
        BiomeRegion nearest = candidates[0];
        float nearestWeightedDistanceSqr = float.MaxValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            float dx = candidates[i].Position.x - position.x;
            float dy = candidates[i].Position.y - position.y;
            float distanceSqr = dx * dx + dy * dy;

            float weightedDistanceSqr = distanceSqr / candidates[i].Weight;
            weightedDistanceSqr += HandleGetBoundaryNoise(position, candidates[i].Index);

            if (weightedDistanceSqr < nearestWeightedDistanceSqr)
            {
                nearestWeightedDistanceSqr = weightedDistanceSqr;
                nearest = candidates[i];
            }
        }

        return nearest;
    }

    // 영역마다 서로 다른 펄린 노이즈 값을 가중 거리에 더해, 두 영역 사이의 경계(원호)가 자연스럽게 굴곡지도록 만든다.
    private float HandleGetBoundaryNoise(Vector2 position, int regionIndex)
    {
        if (m_BoundaryNoiseStrength <= 0f)
            return 0f;

        float sampleX = (position.x * m_BoundaryNoiseScale) + (regionIndex * 37.13f);
        float sampleY = (position.y * m_BoundaryNoiseScale) + (regionIndex * 91.7f);
        float noise = (Mathf.PerlinNoise(sampleX, sampleY) - 0.5f) * 2f;

        return noise * m_BoundaryNoiseStrength;
    }
}
