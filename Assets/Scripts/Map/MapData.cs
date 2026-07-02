using UnityEngine;

// 보로노이 다이어그램을 기반으로 맵의 바이옴 정보를 계산하는 클래스
public class MapData
{
    // 바이옴 영역의 중심이 되는 보로노이 정점 하나를 표현하는 구조체
    public struct BiomeRegion
    {
        // 정점의 위치 (0~1로 정규화된 좌표)
        public Vector2 Position;
        // 정점이 차지하는 영역의 가중치
        public float Weight;
        // 정점이 속한 바이옴
        public MapBiome Biome;
    }

    // 생성된 바이옴 영역(보로노이 정점) 목록
    private readonly BiomeRegion[] m_Regions;
    // 경계선에 적용할 노이즈의 스케일. 클수록 더 잘게, 자주 굴곡진 경계가 만들어진다.
    private readonly float m_BoundaryNoiseScale;
    // 경계선에 적용할 노이즈의 세기. 0이면 원래의 가중 보로노이 경계(원호) 그대로 유지된다.
    private readonly float m_BoundaryNoiseStrength;
    // 이 거리(제곱)보다 멀리 있는 영역은 영향력 계산에서 제외된다.
    private readonly float m_MaxInfluenceDistanceSqr;

    // 주어진 시드와 바이옴 목록으로 랜덤 바이옴 영역들을 생성한다.
    public MapData(int seed, MapBiome[] biomes, int regionCount = 12, float minWeight = 0.5f, float maxWeight = 2f, float boundaryNoiseScale = 4f, float boundaryNoiseStrength = 0.15f, float maxInfluenceDistance = 0.6f)
    {
        m_BoundaryNoiseScale = boundaryNoiseScale;
        m_BoundaryNoiseStrength = boundaryNoiseStrength;
        m_MaxInfluenceDistanceSqr = maxInfluenceDistance * maxInfluenceDistance;

        Random.State previousState = Random.state;
        Random.InitState(seed);

        m_Regions = new BiomeRegion[regionCount];

        for (int i = 0; i < regionCount; i++)
        {
            m_Regions[i] = new BiomeRegion
            {
                Position = new Vector2(Random.value, Random.value),
                Weight = Random.Range(minWeight, maxWeight),
                Biome = biomes[Random.Range(0, biomes.Length)],
            };
        }

        Random.state = previousState;
    }

    // 0~1로 정규화된 좌표를 기준으로 가장 가까운(가중치 적용) 바이옴 영역의 바이옴을 반환한다.
    public MapBiome GetBiome(Vector2 position)
    {
        return m_Regions[HandleFindNearestRegionIndex(position)].Biome;
    }

    // 기즈모 표시 등 외부에서 사용할 수 있도록 생성된 바이옴 영역 목록 전체를 반환한다.
    public BiomeRegion[] GetRegions()
    {
        return m_Regions;
    }

    // 주어진 좌표와 가중치 거리(+ 영역별 노이즈 보정)가 가장 가까운 바이옴 영역의 인덱스를 찾는다.
    private int HandleFindNearestRegionIndex(Vector2 position)
    {
        int nearestIndex = 0;
        float nearestWeightedDistanceSqr = float.MaxValue;

        for (int i = 0; i < m_Regions.Length; i++)
        {
            float dx = m_Regions[i].Position.x - position.x;
            float dy = m_Regions[i].Position.y - position.y;
            float distanceSqr = (dx * dx) + (dy * dy);

            // 후보가 이미 존재하는 경우에만 거리 제한 컷오프를 적용해 항상 최소 하나의 영역이 선택되도록 보장한다.
            if (nearestWeightedDistanceSqr < float.MaxValue && distanceSqr > m_MaxInfluenceDistanceSqr)
                continue;

            float weightedDistanceSqr = distanceSqr / m_Regions[i].Weight;
            weightedDistanceSqr += HandleGetBoundaryNoise(position, i);

            if (weightedDistanceSqr < nearestWeightedDistanceSqr)
            {
                nearestWeightedDistanceSqr = weightedDistanceSqr;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    // 영역마다 서로 다른 펄린 노이즈 값을 가중 거리에 더해, 두 영역 사이의 경계(원호)가 자연스럽게 굴곡지도록 만든다.
    private float HandleGetBoundaryNoise(Vector2 position, int regionIndex)
    {
        if (m_BoundaryNoiseStrength <= 0f)
        {
            return 0f;
        }

        float sampleX = (position.x * m_BoundaryNoiseScale) + (regionIndex * 37.13f);
        float sampleY = (position.y * m_BoundaryNoiseScale) + (regionIndex * 91.7f);
        float noise = (Mathf.PerlinNoise(sampleX, sampleY) - 0.5f) * 2f;

        return noise * m_BoundaryNoiseStrength;
    }
}
