using UnityEngine;

// 보로노이 다이어그램을 기반으로 맵의 바이옴/높이 정보를 계산하는 불변 클래스 (생성 후 상태가 변하지 않아 여러 스레드에서 동시에 읽어도 안전하다)
public sealed class MapData
{
    // 높이 샘플 격자의 정규화 맵 좌표 1단위당 샘플 개수. GetHeight의 정수 격자 좌표를 이 값으로 나누면 맵 좌표가 된다.
    public const int HeightSamplesPerUnit = 64;

    // 이 정규화 높이(0~1) 미만의 지형은 해수면 아래로 잠겨 걸을 수 없다. 씬에 배치되는 바다 평면의 높이(터레인 최대 높이 대비)와 같은 값으로 유지해야 한다.
    public const float SeaLevelHeight = 0.12f;

    // 이 정규화 높이(0~1)를 초과하는 지형은 산/절벽 지대로 간주해 걸을 수 없다.
    public const float WalkableMaxHeight = 0.28f;

    // 높이 영향력 계산의 완충값. 가중 거리에 더해져 정점 근처에서 영향력이 발산하는 것을 막고, 클수록 경계의 높이 전환이 넓고 완만해진다.
    private const float k_HeightBlendSoftness = 0.01f;

    // 이 맵 데이터를 생성할 때 사용된 랜덤 시드
    private readonly int m_Seed;
    private readonly BiomeRegionMap m_RegionMap;
    // 경계선에 적용할 노이즈의 스케일. 클수록 더 잘게, 자주 굴곡진 경계가 만들어진다.
    private readonly float m_BoundaryNoiseScale;
    // 경계선에 적용할 노이즈의 세기. 0이면 원래의 가중 보로노이 경계(원호) 그대로 유지된다.
    private readonly float m_BoundaryNoiseStrength;
    // 이 거리보다 멀리 있는 정점은 영향력 계산에서 제외된다.
    private readonly float m_MaxInfluenceDistance;
    // maxInfluenceDistance 이내에 정점이 하나도 없을 때 대신 사용할 바이옴.
    private readonly MapBiome m_DefaultBiome;
    // 시드마다 높이 굴곡 노이즈가 달라지도록 하는 펄린 샘플 오프셋
    private readonly Vector2 m_HeightNoiseOffset;

    // 이 맵 데이터를 생성할 때 사용된 랜덤 시드를 반환한다.
    public int Seed => m_Seed;

    // 주어진 시드와 바이옴 목록으로 바이옴 영역 맵을 생성한다.
    public MapData(int seed, MapBiome[] biomes, MapBiome defaultBiome, float minWeight = 0.5f, float maxWeight = 2f, float boundaryNoiseScale = 4f, float boundaryNoiseStrength = 0.15f, float maxInfluenceDistance = 0.6f)
    {
        m_Seed = seed;
        m_HeightNoiseOffset = HandleGetHeightNoiseOffset(seed);
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

    // 높이 샘플 격자 좌표(맵 좌표 × HeightSamplesPerUnit)의 지형 높이(0~1)를 반환한다. 범위 내 정점이 없으면 기본 바이옴의 높이를 반환한다.
    public float GetHeight(Vector2Int pos)
    {
        Vector2 position = (Vector2)pos / HeightSamplesPerUnit;

        BiomeRegion[] candidates = m_RegionMap.GetRegions(position, m_MaxInfluenceDistance);
        if (candidates.Length == 0)
            return Mathf.Clamp01(HandleSampleBiomeHeight(m_DefaultBiome, position));

        // 각 정점의 영향력(가중 거리 역수의 제곱)에 비례해 바이옴 높이 프로필을 섞어, 바이옴 경계에서 높이가 자연스럽게 이어지도록 한다.
        float totalInfluence = 0f;
        float blendedHeight = 0f;

        for (int i = 0; i < candidates.Length; i++)
        {
            float dx = candidates[i].Position.x - position.x;
            float dy = candidates[i].Position.y - position.y;

            float weightedDistanceSqr = (dx * dx + dy * dy) / candidates[i].Weight;
            weightedDistanceSqr += HandleGetBoundaryNoise(position, candidates[i].Index);

            float falloff = Mathf.Max(weightedDistanceSqr, 0f) + k_HeightBlendSoftness;
            float influence = 1f / (falloff * falloff);

            totalInfluence += influence;
            blendedHeight += influence * HandleSampleBiomeHeight(candidates[i].Biome, position);
        }

        return Mathf.Clamp01(blendedHeight / totalInfluence);
    }

    // 주어진 맵 좌표가 걸을 수 있는 지형인지 판정한다. 지형 표면 높이가 해수면 위이면서 지정한 최대 높이 이하이면 걸을 수 있다. GetHeight만 사용하는 순수 계산이라 Unity 내비게이션/피직스 API에 의존하지 않고 백그라운드 스레드에서도 안전하다.
    public bool IsWalkable(Vector2 position, float minHeight = SeaLevelHeight, float maxHeight = WalkableMaxHeight)
    {
        return HandleIsHeightWalkable(HandleSampleSurfaceHeight(position), minHeight, maxHeight);
    }

    // 중심(center)에서 반지름(radius, 정규화 맵 좌표 단위) 안에 걸을 수 없는 지형이 하나라도 있으면 true를 반환한다. 건물 배치처럼 일정 범위 전체가 걸을 수 있어야 하는 경우에 쓴다.
    // 높이 표본 격자 해상도로 원 안의 표본만 검사하므로 비용은 반지름의 제곱에 비례하며, 걸을 수 없는 표본을 처음 만나면 즉시 종료한다. IsWalkable과 마찬가지로 Unity API에 의존하지 않는다.
    public bool HasUnwalkableWithin(Vector2 center, float radius, float minHeight = SeaLevelHeight, float maxHeight = WalkableMaxHeight)
    {
        // 반지름이 표본 격자 한 칸보다 작아 원이 격자점을 하나도 담지 못하더라도 중심점만은 항상 검사한다.
        if (!HandleIsHeightWalkable(HandleSampleSurfaceHeight(center), minHeight, maxHeight))
            return true;

        Vector2 centerGrid = center * HeightSamplesPerUnit;
        float gridRadius = Mathf.Abs(radius) * HeightSamplesPerUnit;
        float gridRadiusSqr = gridRadius * gridRadius;

        int minGridX = Mathf.FloorToInt(centerGrid.x - gridRadius);
        int maxGridX = Mathf.CeilToInt(centerGrid.x + gridRadius);
        int minGridY = Mathf.FloorToInt(centerGrid.y - gridRadius);
        int maxGridY = Mathf.CeilToInt(centerGrid.y + gridRadius);

        for (int gridY = minGridY; gridY <= maxGridY; gridY++)
        {
            for (int gridX = minGridX; gridX <= maxGridX; gridX++)
            {
                float dx = gridX - centerGrid.x;
                float dy = gridY - centerGrid.y;
                if (dx * dx + dy * dy > gridRadiusSqr)
                    continue;

                if (!HandleIsHeightWalkable(GetHeight(new Vector2Int(gridX, gridY)), minHeight, maxHeight))
                    return true;
            }
        }

        return false;
    }

    // 맵 좌표를 둘러싼 높이 격자점 4개를 이중선형 보간해, 실제 터레인 표면과 같은 높이(0~1)를 계산한다. (TerrainPainter가 높이맵을 굽는 방식과 동일하다)
    private float HandleSampleSurfaceHeight(Vector2 position)
    {
        Vector2 gridPosition = position * HeightSamplesPerUnit;
        int gridX = Mathf.FloorToInt(gridPosition.x);
        int gridY = Mathf.FloorToInt(gridPosition.y);
        float tx = gridPosition.x - gridX;
        float ty = gridPosition.y - gridY;

        float h00 = GetHeight(new Vector2Int(gridX, gridY));
        float h10 = GetHeight(new Vector2Int(gridX + 1, gridY));
        float h01 = GetHeight(new Vector2Int(gridX, gridY + 1));
        float h11 = GetHeight(new Vector2Int(gridX + 1, gridY + 1));

        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
    }

    // 주어진 지형 높이가 걸을 수 있는 높이 범위(해수면 위 ~ 최대 높이) 안에 있는지 판정한다. 걷기 가능 조건을 한곳에 모아 IsWalkable과 HasUnwalkableWithin이 함께 사용한다.
    private static bool HandleIsHeightWalkable(float height, float minHeight, float maxHeight)
    {
        return height >= minHeight && height <= maxHeight;
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

    // 한 바이옴의 높이 프로필(기준 높이 + 굴곡 노이즈)을 주어진 맵 좌표에서 샘플링한다.
    private float HandleSampleBiomeHeight(MapBiome biome, Vector2 position)
    {
        // 바이옴마다 샘플 위치를 밀어, 서로 다른 바이옴이 같은 굴곡 패턴을 공유하지 않게 한다.
        Vector2 shift = HandleGetBiomeNoiseShift(biome);

        float noise = Mathf.PerlinNoise(
            position.x * biome.HeightNoiseScale + m_HeightNoiseOffset.x + shift.x,
            position.y * biome.HeightNoiseScale + m_HeightNoiseOffset.y + shift.y);

        return biome.BaseHeight + noise * biome.HeightNoiseAmplitude;
    }

    // 바이옴 이름에서 결정론적인 굴곡 노이즈 샘플 오프셋을 계산한다.
    private static Vector2 HandleGetBiomeNoiseShift(MapBiome biome)
    {
        // FNV-1a 해시: string.GetHashCode와 달리 세션이 바뀌어도 값이 유지된다.
        uint hash = 2166136261u;
        foreach (char character in biome.Name ?? string.Empty)
            hash = (hash ^ character) * 16777619u;

        return new Vector2((hash & 0xFFFF) / 65536f * 97f, ((hash >> 16) & 0xFFFF) / 65536f * 97f);
    }

    // 시드마다 높이 노이즈 패턴이 달라지도록 시드로부터 펄린 샘플 오프셋을 계산한다.
    private static Vector2 HandleGetHeightNoiseOffset(int seed)
    {
        uint hash = (uint)seed * 0x9E3779B1u;
        hash ^= hash >> 16;

        return new Vector2((hash & 0xFFFF) / 65536f * 997f, ((hash >> 16) & 0xFFFF) / 65536f * 997f);
    }
}
