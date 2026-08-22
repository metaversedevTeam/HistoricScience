using System.Collections.Generic;
using UnityEngine;

// 무한 평면 위의 보로노이 바이옴 정점들을 정의된 생성 규칙에 따라 계산해 제공하는 클래스. 정점을 저장하지 않고 조회할 때마다 규칙으로 다시 계산하되,
// 한 영역을 반복해서 조회할 때는 CreateField로 그 영역의 정점을 미리 계산해 두고 EnumerateRegions에 넘겨 재계산을 피할 수 있다.
// 생성 규칙: 평면을 격자 셀로 나눠 셀마다 정점을 하나씩 배치하고, 저주파 고도/습도 노이즈 값을 바이옴 에셋의 배치 범위와 목록 순서대로 대조해 처음 맞는 바이옴을 배정한다.
// 노이즈가 저주파라 이웃 정점들이 같은 바이옴으로 뭉쳐 좁은 파편 영역이 생기지 않으며, 주변 정점에 인접 금지 바이옴이 있으면 에셋에 지정된 대체 바이옴으로 바뀐다.
// 생성 후 상태가 변하지 않는 불변 클래스로, 여러 스레드에서 동시에 읽어도 안전하다.
public sealed class BiomeRegionMap
{
    // 정점 배치 격자의 셀 한 변 길이 (정규화 좌표). 셀마다 정점이 하나씩 배치된다.
    private const float k_CellSize = 0.2f;
    // 셀 가장자리에 정점이 배치되지 않도록 하는 여백 비율(0~0.5). 이웃 정점끼리 지나치게 붙어 좁은 파편 영역이 생기는 것을 막는다.
    private const float k_CellMargin = 0.2f;
    // 고도 노이즈의 스케일. 작을수록 바다/산 덩어리가 넓어져 파편화가 줄어든다.
    private const float k_ElevationNoiseScale = 1.2f;
    // 습도 노이즈의 스케일. 작을수록 사막/평원 덩어리가 넓어져 파편화가 줄어든다.
    private const float k_MoistureNoiseScale = 0.9f;
    // 인접 금지 검사에서 확인할 주변 셀 반경 (체비쇼프 거리). 가중치 범위가 기본값(0.5~2)일 때 보로노이 이웃이 될 수 있는 범위를 덮는다.
    private const int k_IncompatibleCheckRadius = 2;
    // MapData의 경계 노이즈 펄린 샘플 좌표가 너무 커지지 않도록 정점 인덱스 값 범위를 제한하는 마스크
    private const int k_RegionIndexMask = 0x3FF;

    private readonly int m_Seed;
    private readonly float m_MinWeight;
    private readonly float m_MaxWeight;
    // 배치 우선순위 순서의 바이옴 목록. 앞선 바이옴의 배치 범위가 먼저 검사된다.
    private readonly MapBiome[] m_Biomes;
    // 시드마다 고도/습도 노이즈가 달라지도록 하는 펄린 샘플 오프셋
    private readonly Vector2 m_ElevationNoiseOffset;
    private readonly Vector2 m_MoistureNoiseOffset;

    // 시드, 정점 가중치 범위, 바이옴 목록으로 맵을 초기화한다.
    public BiomeRegionMap(int seed, float minWeight, float maxWeight, MapBiome[] biomes)
    {
        m_Seed = seed;
        m_MinWeight = minWeight;
        m_MaxWeight = maxWeight;
        // 호출부가 넘긴 배열을 나중에 바꾸더라도 이 객체의 상태가 변하지 않도록 방어적으로 복사해 둔다.
        m_Biomes = biomes != null ? (MapBiome[])biomes.Clone() : new MapBiome[0];

        if (m_Biomes.Length == 0)
            Debug.LogError("BiomeRegionMap: 바이옴 목록이 비어 있습니다.");
        m_ElevationNoiseOffset = new Vector2(HandleHash01(0, 0, 101) * 997f, HandleHash01(0, 0, 102) * 997f);
        m_MoistureNoiseOffset = new Vector2(HandleHash01(0, 0, 103) * 997f, HandleHash01(0, 0, 104) * 997f);
    }

    // 주어진 위치에서 maxInfluenceDistance 이내의 정점들을, 배열을 만들지 않고 순서대로 훑는 열거자를 반환한다.
    // 정점 필드를 넘기면 미리 계산된 정점을 꺼내 쓰고, null이거나 필드 범위 밖의 셀이면 예전처럼 그 자리에서 계산한다.
    // 훑는 순서와 걸러내는 조건은 필드 유무와 무관하게 같으므로, 필드를 넘겨도 결과가 달라지지 않는다.
    public RegionEnumerator EnumerateRegions(Vector2 pos, float maxInfluenceDistance, ChunkRegionField field)
    {
        return new RegionEnumerator(this, pos, maxInfluenceDistance, field);
    }

    // 주어진 사각형 영역을 덮는 셀 격자 범위의 정점을 한 번에 계산해 조회용 필드로 만든다. 계산 비용은 영역의 넓이에 비례하므로
    // 청크 하나처럼 한정된 영역에만 쓴다. 호출자는 영역 밖에서 영향력을 미치는 정점까지 담기도록 영향 반경만큼 넓혀서 넘겨야 한다.
    public ChunkRegionField CreateField(Rect area)
    {
        int minCellX = Mathf.FloorToInt(area.xMin / k_CellSize);
        int maxCellX = Mathf.FloorToInt(area.xMax / k_CellSize);
        int minCellY = Mathf.FloorToInt(area.yMin / k_CellSize);
        int maxCellY = Mathf.FloorToInt(area.yMax / k_CellSize);

        int cellCountX = Mathf.Max(maxCellX - minCellX + 1, 0);
        int cellCountY = Mathf.Max(maxCellY - minCellY + 1, 0);
        BiomeRegion[] regions = new BiomeRegion[cellCountX * cellCountY];

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                regions[(cellY - minCellY) * cellCountX + (cellX - minCellX)] = HandleCreateRegion(cellX, cellY);
        }

        return new ChunkRegionField(minCellX, minCellY, cellCountX, cellCountY, regions);
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
        return new BiomeRegion(
            index: (int)(HandleHash(cellX, cellY, 3) & k_RegionIndexMask),
            position: HandleGetRegionPosition(cellX, cellY),
            weight: Mathf.Lerp(m_MinWeight, m_MaxWeight, HandleHash01(cellX, cellY, 2)),
            biome: HandleGetBiome(cellX, cellY));
    }

    // 셀 내부의 결정론적 지터(여백 안쪽) 위치에 정점을 배치한다.
    private Vector2 HandleGetRegionPosition(int cellX, int cellY)
    {
        float jitterX = Mathf.Lerp(k_CellMargin, 1f - k_CellMargin, HandleHash01(cellX, cellY, 0));
        float jitterY = Mathf.Lerp(k_CellMargin, 1f - k_CellMargin, HandleHash01(cellX, cellY, 1));

        return new Vector2((cellX + jitterX) * k_CellSize, (cellY + jitterY) * k_CellSize);
    }

    // 인접 금지 규칙까지 반영한 셀 정점의 최종 바이옴을 계산한다.
    private MapBiome HandleGetBiome(int cellX, int cellY)
    {
        MapBiome rawBiome = HandleGetRawBiome(cellX, cellY);
        if (rawBiome == null || !rawBiome.HasIncompatibleRule)
            return rawBiome;

        // 주변 정점에 인접 금지 바이옴이 있으면 대체 바이옴으로 바꿔, 두 바이옴 사이에 항상 완충 지대가 생기도록 한다.
        for (int offsetY = -k_IncompatibleCheckRadius; offsetY <= k_IncompatibleCheckRadius; offsetY++)
        {
            for (int offsetX = -k_IncompatibleCheckRadius; offsetX <= k_IncompatibleCheckRadius; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                    continue;

                if (rawBiome.IsIncompatibleWith(HandleGetRawBiome(cellX + offsetX, cellY + offsetY)))
                    return rawBiome.FallbackBiome;
            }
        }

        return rawBiome;
    }

    // 고도/습도 노이즈 값을 바이옴 목록의 배치 범위와 순서대로 대조해 셀 정점의 기본 바이옴을 계산한다. 맞는 바이옴이 없으면 목록의 첫 바이옴을 반환한다.
    private MapBiome HandleGetRawBiome(int cellX, int cellY)
    {
        if (m_Biomes.Length == 0)
            return null;

        Vector2 position = HandleGetRegionPosition(cellX, cellY);

        float elevation = Mathf.PerlinNoise(
            position.x * k_ElevationNoiseScale + m_ElevationNoiseOffset.x,
            position.y * k_ElevationNoiseScale + m_ElevationNoiseOffset.y);

        float moisture = Mathf.PerlinNoise(
            position.x * k_MoistureNoiseScale + m_MoistureNoiseOffset.x,
            position.y * k_MoistureNoiseScale + m_MoistureNoiseOffset.y);

        for (int i = 0; i < m_Biomes.Length; i++)
        {
            if (m_Biomes[i].Matches(elevation, moisture))
                return m_Biomes[i];
        }

        return m_Biomes[0];
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

    // EnumerateRegions가 돌려주는, 영향 반경 안의 정점을 하나씩 훑는 열거자. foreach에 필요한 것만 갖춘 구조체라 훑는 동안 힙 할당이 없다.
    // 원을 감싸는 외접 사각형의 셀 범위를 행 우선으로 돌면서, 사각형 안에 실제로 들어간 정점만 골라 다시 원형 거리로 걸러낸다.
    public struct RegionEnumerator
    {
        // 필드에 담겨 있지 않은 셀의 정점을 그 자리에서 계산할 때 쓰는 원본 맵
        private readonly BiomeRegionMap m_Map;
        // 미리 계산된 정점 필드. null이면 모든 정점을 그 자리에서 계산한다.
        private readonly ChunkRegionField m_Field;
        // 영향 반경의 중심이 되는 조회 위치
        private readonly Vector2 m_Position;
        // 영향 반경 원을 감싸는 외접 사각형. 정점이 이 사각형 안에 있는지 먼저 검사한다.
        private readonly Rect m_BoundingArea;
        // 영향 반경의 제곱. 사각형을 통과한 정점을 원형 거리로 다시 걸러내는 데 쓴다.
        private readonly float m_MaxDistanceSqr;
        // 훑을 셀 격자 범위
        private readonly int m_MinCellX;
        private readonly int m_MaxCellX;
        private readonly int m_MaxCellY;
        // 다음에 검사할 셀 좌표
        private int m_CellX;
        private int m_CellY;
        // 마지막으로 통과한 정점
        private BiomeRegion m_Current;

        // 조회 위치와 영향 반경으로 훑을 셀 범위를 정하고 첫 셀에서 시작하도록 초기화한다.
        public RegionEnumerator(BiomeRegionMap map, Vector2 pos, float maxInfluenceDistance, ChunkRegionField field)
        {
            m_Map = map;
            m_Field = field;
            m_Position = pos;
            m_BoundingArea = new Rect(pos.x - maxInfluenceDistance, pos.y - maxInfluenceDistance, maxInfluenceDistance * 2f, maxInfluenceDistance * 2f);
            m_MaxDistanceSqr = maxInfluenceDistance * maxInfluenceDistance;

            m_MinCellX = Mathf.FloorToInt(m_BoundingArea.xMin / k_CellSize);
            m_MaxCellX = Mathf.FloorToInt(m_BoundingArea.xMax / k_CellSize);
            m_MaxCellY = Mathf.FloorToInt(m_BoundingArea.yMax / k_CellSize);

            m_CellX = m_MinCellX;
            m_CellY = Mathf.FloorToInt(m_BoundingArea.yMin / k_CellSize);
            m_Current = default;
        }

        // foreach가 열거자를 얻을 때 호출한다. 구조체 자신을 값으로 복사해 돌려주므로 같은 열거자를 여러 번 돌릴 수 있다.
        public RegionEnumerator GetEnumerator()
        {
            return this;
        }

        // 마지막으로 통과한 정점
        public BiomeRegion Current => m_Current;

        // 다음으로 조건을 통과하는 정점을 찾아 Current에 담는다. 더 없으면 false를 반환한다.
        public bool MoveNext()
        {
            while (m_CellY <= m_MaxCellY)
            {
                int cellX = m_CellX;
                int cellY = m_CellY;

                m_CellX++;
                if (m_CellX > m_MaxCellX)
                {
                    m_CellX = m_MinCellX;
                    m_CellY++;
                }

                BiomeRegion region = HandleResolveRegion(cellX, cellY);
                if (!m_BoundingArea.Contains(region.Position))
                    continue;

                float dx = region.Position.x - m_Position.x;
                float dy = region.Position.y - m_Position.y;
                float distSqr = dx * dx + dy * dy;

                if (distSqr <= m_MaxDistanceSqr)
                {
                    m_Current = region;
                    return true;
                }
            }

            return false;
        }

        // 셀 좌표의 정점을 필드에서 꺼내고, 담겨 있지 않으면 생성 규칙으로 그 자리에서 계산한다.
        private BiomeRegion HandleResolveRegion(int cellX, int cellY)
        {
            if (m_Field != null && m_Field.TryGetRegion(cellX, cellY, out BiomeRegion region))
                return region;

            return m_Map.HandleCreateRegion(cellX, cellY);
        }
    }
}
