using System.Collections.Generic;
using UnityEngine;

// 무한 평면 위의 보로노이 바이옴 정점들을 정의된 생성 규칙에 따라 계산해 제공하는 클래스. 정점을 저장하지 않고 조회할 때마다 규칙으로 다시 계산한다. (캐싱은 추후 도입 예정)
// 생성 규칙: 평면을 격자 셀로 나눠 셀마다 정점을 하나씩 배치하고, 저주파 고도/습도 노이즈로 바다/산/사막/평원을 배정한다. 노이즈가 저주파라 이웃 정점들이 같은 바이옴으로 뭉쳐 좁은 파편 영역이 생기지 않으며, 산 근처의 사막은 평원으로 강등해 산과 사막이 접하지 않는다.
public class BiomeRegionMap
{
    // 정점 배치 격자의 셀 한 변 길이 (정규화 좌표). 셀마다 정점이 하나씩 배치된다.
    private const float k_CellSize = 0.2f;
    // 셀 가장자리에 정점이 배치되지 않도록 하는 여백 비율(0~0.5). 이웃 정점끼리 지나치게 붙어 좁은 파편 영역이 생기는 것을 막는다.
    private const float k_CellMargin = 0.2f;
    // 고도 노이즈의 스케일. 작을수록 바다/산 덩어리가 넓어져 파편화가 줄어든다.
    private const float k_ElevationNoiseScale = 1.2f;
    // 습도 노이즈의 스케일. 작을수록 사막/평원 덩어리가 넓어져 파편화가 줄어든다.
    private const float k_MoistureNoiseScale = 0.9f;
    // 고도 노이즈가 이 값 미만인 정점은 바다가 된다.
    private const float k_SeaLevel = 0.35f;
    // 고도 노이즈가 이 값 이상인 정점은 산이 된다.
    private const float k_MountainLevel = 0.65f;
    // 육지 정점 중 습도 노이즈가 이 값 미만이면 사막이 된다.
    private const float k_DesertMoistureLevel = 0.35f;
    // 산-사막 인접 금지 검사에서 확인할 주변 셀 반경 (체비쇼프 거리). 가중치 범위가 기본값(0.5~2)일 때 보로노이 이웃이 될 수 있는 범위를 덮는다.
    private const int k_MountainCheckRadius = 2;
    // MapData의 경계 노이즈 펄린 샘플 좌표가 너무 커지지 않도록 정점 인덱스 값 범위를 제한하는 마스크
    private const int k_RegionIndexMask = 0x3FF;

    private readonly int m_Seed;
    private readonly float m_MinWeight;
    private readonly float m_MaxWeight;
    // MapBiomeType 값을 인덱스로 하는 바이옴 에셋 조회 테이블
    private readonly MapBiome[] m_BiomesByType;
    // 시드마다 고도/습도 노이즈가 달라지도록 하는 펄린 샘플 오프셋
    private readonly Vector2 m_ElevationNoiseOffset;
    private readonly Vector2 m_MoistureNoiseOffset;

    // 시드, 정점 가중치 범위, 바이옴 목록으로 맵을 초기화한다.
    public BiomeRegionMap(int seed, float minWeight, float maxWeight, MapBiome[] biomes)
    {
        m_Seed = seed;
        m_MinWeight = minWeight;
        m_MaxWeight = maxWeight;
        m_BiomesByType = HandleBuildBiomeTable(biomes);
        m_ElevationNoiseOffset = new Vector2(HandleHash01(0, 0, 101) * 997f, HandleHash01(0, 0, 102) * 997f);
        m_MoistureNoiseOffset = new Vector2(HandleHash01(0, 0, 103) * 997f, HandleHash01(0, 0, 104) * 997f);
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

    // 주어진 사각형 영역과 겹치는 격자 셀들의 정점을 생성 규칙에 따라 계산해, 실제로 영역 안에 있는 것만 반환한다.
    public BiomeRegion[] GetRegions(Rect area)
    {
        int minCellX = Mathf.FloorToInt(area.xMin / k_CellSize);
        int maxCellX = Mathf.FloorToInt(area.xMax / k_CellSize);
        int minCellY = Mathf.FloorToInt(area.yMin / k_CellSize);
        int maxCellY = Mathf.FloorToInt(area.yMax / k_CellSize);

        var result = new List<BiomeRegion>();

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                BiomeRegion region = HandleCreateRegion(cellX, cellY);
                if (area.Contains(region.Position))
                    result.Add(region);
            }
        }

        return result.ToArray();
    }

    // 셀 좌표 하나에 대응하는 바이옴 정점을 생성 규칙에 따라 계산한다.
    private BiomeRegion HandleCreateRegion(int cellX, int cellY)
    {
        return new BiomeRegion
        {
            Index = (int)(HandleHash(cellX, cellY, 3) & k_RegionIndexMask),
            Position = HandleGetRegionPosition(cellX, cellY),
            Weight = Mathf.Lerp(m_MinWeight, m_MaxWeight, HandleHash01(cellX, cellY, 2)),
            Biome = m_BiomesByType[(int)HandleGetBiomeType(cellX, cellY)],
        };
    }

    // 셀 내부의 결정론적 지터(여백 안쪽) 위치에 정점을 배치한다.
    private Vector2 HandleGetRegionPosition(int cellX, int cellY)
    {
        float jitterX = Mathf.Lerp(k_CellMargin, 1f - k_CellMargin, HandleHash01(cellX, cellY, 0));
        float jitterY = Mathf.Lerp(k_CellMargin, 1f - k_CellMargin, HandleHash01(cellX, cellY, 1));

        return new Vector2((cellX + jitterX) * k_CellSize, (cellY + jitterY) * k_CellSize);
    }

    // 산-사막 인접 금지 규칙까지 반영한 셀 정점의 최종 바이옴 종류를 계산한다.
    private MapBiomeType HandleGetBiomeType(int cellX, int cellY)
    {
        MapBiomeType rawType = HandleGetRawBiomeType(cellX, cellY);
        if (rawType != MapBiomeType.Desert)
            return rawType;

        // 사막 정점 주변에 산 정점이 있으면 평원으로 강등해, 산과 사막 사이에 항상 완충 지대가 생기도록 한다.
        for (int offsetY = -k_MountainCheckRadius; offsetY <= k_MountainCheckRadius; offsetY++)
        {
            for (int offsetX = -k_MountainCheckRadius; offsetX <= k_MountainCheckRadius; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                    continue;

                if (HandleGetRawBiomeType(cellX + offsetX, cellY + offsetY) == MapBiomeType.Mountain)
                    return MapBiomeType.Plains;
            }
        }

        return MapBiomeType.Desert;
    }

    // 고도/습도 노이즈만으로 셀 정점의 기본 바이옴 종류를 계산한다. 고도가 낮으면 바다, 높으면 산, 그 사이 육지는 습도에 따라 사막 또는 평원이 된다.
    private MapBiomeType HandleGetRawBiomeType(int cellX, int cellY)
    {
        Vector2 position = HandleGetRegionPosition(cellX, cellY);

        float elevation = Mathf.PerlinNoise(
            position.x * k_ElevationNoiseScale + m_ElevationNoiseOffset.x,
            position.y * k_ElevationNoiseScale + m_ElevationNoiseOffset.y);

        if (elevation < k_SeaLevel)
            return MapBiomeType.Sea;

        if (elevation >= k_MountainLevel)
            return MapBiomeType.Mountain;

        float moisture = Mathf.PerlinNoise(
            position.x * k_MoistureNoiseScale + m_MoistureNoiseOffset.x,
            position.y * k_MoistureNoiseScale + m_MoistureNoiseOffset.y);

        return moisture < k_DesertMoistureLevel ? MapBiomeType.Desert : MapBiomeType.Plains;
    }

    // 바이옴 목록을 종류별 조회 테이블로 변환한다. 누락된 종류는 에러 로그를 남기고 목록의 첫 바이옴으로 대체한다.
    private static MapBiome[] HandleBuildBiomeTable(MapBiome[] biomes)
    {
        int typeCount = System.Enum.GetValues(typeof(MapBiomeType)).Length;
        var table = new MapBiome[typeCount];

        if (biomes == null || biomes.Length == 0)
        {
            Debug.LogError("BiomeRegionMap: 바이옴 목록이 비어 있습니다.");
            return table;
        }

        for (int i = 0; i < biomes.Length; i++)
        {
            int typeIndex = (int)biomes[i].BiomeType;
            if (table[typeIndex] == null)
                table[typeIndex] = biomes[i];
        }

        for (int i = 0; i < typeCount; i++)
        {
            if (table[i] != null)
                continue;

            Debug.LogError($"BiomeRegionMap: {(MapBiomeType)i} 종류의 바이옴이 목록에 없어 첫 번째 바이옴({biomes[0].Name})으로 대체합니다.");
            table[i] = biomes[0];
        }

        return table;
    }

    // 셀 좌표와 용도 구분값(salt)을 시드와 섞어 결정론적인 32비트 해시를 계산한다.
    private uint HandleHash(int cellX, int cellY, int salt)
    {
        uint hash = (uint)m_Seed;
        hash ^= (uint)cellX * 0x8DA6B343u;
        hash ^= (uint)cellY * 0xD8163841u;
        hash ^= (uint)salt * 0xCB1AB31Fu;

        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        hash *= 0x846CA68Bu;
        hash ^= hash >> 16;

        return hash;
    }

    // 해시 값을 0(포함)~1(미만) 범위의 float으로 변환한다.
    private float HandleHash01(int cellX, int cellY, int salt)
    {
        return (HandleHash(cellX, cellY, salt) & 0xFFFFFF) / 16777216f;
    }
}
