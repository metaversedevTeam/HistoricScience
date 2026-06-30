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
        // 정점이 속한 바이옴 종류
        public MapBiome Biome;
    }

    // 생성된 바이옴 영역(보로노이 정점) 목록
    private readonly BiomeRegion[] m_Regions;

    // 주어진 시드로 랜덤 바이옴 영역들을 생성한다.
    public MapData(int seed, int regionCount = 12, float minWeight = 0.5f, float maxWeight = 2f)
    {
        Random.State previousState = Random.state;
        Random.InitState(seed);

        m_Regions = new BiomeRegion[regionCount];
        int biomeCount = System.Enum.GetValues(typeof(MapBiome)).Length;

        for (int i = 0; i < regionCount; i++)
        {
            m_Regions[i] = new BiomeRegion
            {
                Position = new Vector2(Random.value, Random.value),
                Weight = Random.Range(minWeight, maxWeight),
                Biome = (MapBiome)Random.Range(0, biomeCount),
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

    // 주어진 좌표와 가중치 거리가 가장 가까운 바이옴 영역의 인덱스를 찾는다.
    private int HandleFindNearestRegionIndex(Vector2 position)
    {
        int nearestIndex = 0;
        float nearestWeightedDistanceSqr = float.MaxValue;

        for (int i = 0; i < m_Regions.Length; i++)
        {
            float dx = m_Regions[i].Position.x - position.x;
            float dy = m_Regions[i].Position.y - position.y;
            float distanceSqr = (dx * dx) + (dy * dy);
            float weightedDistanceSqr = distanceSqr / m_Regions[i].Weight;

            if (weightedDistanceSqr < nearestWeightedDistanceSqr)
            {
                nearestWeightedDistanceSqr = weightedDistanceSqr;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }
}