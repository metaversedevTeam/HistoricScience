using System.Collections.Generic;
using UnityEngine;

// 보로노이 다이어그램을 기반으로 맵의 바이옴/높이 정보를 계산하는 클래스
public class MapData
{
    // 높이 샘플 격자의 정규화 맵 좌표 1단위당 샘플 개수. GetHeight의 정수 격자 좌표를 이 값으로 나누면 맵 좌표가 된다.
    public const int HeightSamplesPerUnit = 64;

    // 높이 영향력 계산의 완충값. 가중 거리에 더해져 정점 근처에서 영향력이 발산하는 것을 막고, 클수록 경계의 높이 전환이 넓고 완만해진다.
    private const float k_HeightBlendSoftness = 0.01f;

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
    // 바이옴별 굴곡 노이즈 샘플 오프셋 캐시 (바이옴 이름 해시 기반)
    private readonly Dictionary<MapBiome, Vector2> m_BiomeNoiseShifts = new Dictionary<MapBiome, Vector2>();

    // 주어진 시드와 바이옴 목록으로 바이옴 영역 맵을 생성한다.
    public MapData(int seed, MapBiome[] biomes, MapBiome defaultBiome, float minWeight = 0.5f, float maxWeight = 2f, float boundaryNoiseScale = 4f, float boundaryNoiseStrength = 0.15f, float maxInfluenceDistance = 0.6f)
    {
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

    // 바이옴 이름에서 결정론적인 굴곡 노이즈 샘플 오프셋을 계산해 캐시한다.
    private Vector2 HandleGetBiomeNoiseShift(MapBiome biome)
    {
        if (m_BiomeNoiseShifts.TryGetValue(biome, out Vector2 shift))
            return shift;

        // FNV-1a 해시: string.GetHashCode와 달리 세션이 바뀌어도 값이 유지된다.
        uint hash = 2166136261u;
        foreach (char character in biome.Name ?? string.Empty)
            hash = (hash ^ character) * 16777619u;

        shift = new Vector2((hash & 0xFFFF) / 65536f * 97f, ((hash >> 16) & 0xFFFF) / 65536f * 97f);
        m_BiomeNoiseShifts[biome] = shift;

        return shift;
    }

    // 시드마다 높이 노이즈 패턴이 달라지도록 시드로부터 펄린 샘플 오프셋을 계산한다.
    private static Vector2 HandleGetHeightNoiseOffset(int seed)
    {
        uint hash = (uint)seed * 0x9E3779B1u;
        hash ^= hash >> 16;

        return new Vector2((hash & 0xFFFF) / 65536f * 997f, ((hash >> 16) & 0xFFFF) / 65536f * 997f);
    }
}
