using UnityEngine;

// 청크 하나를 굽는 동안 쓸 높이 격자점(MapData.GetHeight)을 청크 영역보다 한 칸씩 넓게 미리 계산해 두고, 맵 좌표의 표면 높이와 경사를 조회하는 클래스.
// 높이맵과 경사 판정이 같은 격자점을 공유해 GetHeight를 두 번 계산하지 않는다. 격자점이 전역 격자 좌표 기준이라 이웃 청크 경계에서 같은 높이·경사가 나온다.
// 생성 후 상태가 변하지 않으므로 백그라운드 스레드에서 읽어도 안전하다.
public sealed class ChunkHeightGrid
{
    // 경사를 구할 때 쓸 수 있는 가장 넓은 중앙 차분 간격(격자 칸). 출력 영역 밖으로 이만큼 + 1칸 여백을 두어, 영역 가장자리에서도 이웃 청크와 같은 경사가 나오게 한다.
    public const int MaxSlopeStencil = 3;
    // 출력 영역 밖으로 미리 읽어 둘 여백(격자 칸)
    public const int Padding = MaxSlopeStencil + 1;

    // 담고 있는 격자의 최소 전역 격자 좌표 (여백 포함)
    private readonly Vector2Int m_Origin;
    // 격자점 높이(m). [z, x] 순서이며 m_Origin을 (0, 0)으로 둔다.
    private readonly float[,] m_Heights;
    // 격자 한 칸의 길이 (정규화 맵 좌표)
    private readonly float m_Spacing;
    // 격자 한 칸의 월드 길이(m). 경사를 실제 각도로 바꿀 때 쓴다.
    private readonly float m_CellWorldSize;

    // 전역 격자 좌표 [min, max] 범위를 여백(Padding)과 함께 MapData에서 읽어 격자를 만든다.
    public ChunkHeightGrid(MapData mapData, ChunkRegionField regionField, Vector2Int min, Vector2Int max)
    {
        MapTerrainConfig config = mapData.TerrainConfig;
        m_Spacing = mapData.HeightSampleSpacing;
        m_CellWorldSize = m_Spacing * config.ChunkWorldSize / config.MapViewSize;
        m_Origin = min - Vector2Int.one * Padding;

        int width = max.x - min.x + 1 + Padding * 2;
        int height = max.y - min.y + 1 + Padding * 2;
        m_Heights = new float[height, width];

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
                m_Heights[z, x] = mapData.GetHeight(new Vector2Int(m_Origin.x + x, m_Origin.y + z), regionField);
        }
    }

    // 맵 좌표 영역을 덮는 최소 전역 격자 좌표 범위를 구한다.
    public static void GetGridBounds(Vector2 mapMin, Vector2 mapMax, float spacing, out Vector2Int min, out Vector2Int max)
    {
        min = new Vector2Int(Mathf.FloorToInt(mapMin.x / spacing), Mathf.FloorToInt(mapMin.y / spacing));
        max = new Vector2Int(Mathf.CeilToInt(mapMax.x / spacing), Mathf.CeilToInt(mapMax.y / spacing));
    }

    // 전역 격자 좌표의 높이(m)를 반환한다. 격자 범위(여백 포함) 밖이면 가장자리 값으로 자른다.
    public float GetGridHeight(int gridX, int gridZ)
    {
        int x = Mathf.Clamp(gridX - m_Origin.x, 0, m_Heights.GetLength(1) - 1);
        int z = Mathf.Clamp(gridZ - m_Origin.y, 0, m_Heights.GetLength(0) - 1);
        return m_Heights[z, x];
    }

    // 맵 좌표의 표면 높이(m)를 둘러싼 격자점 4개의 이중선형 보간으로 구한다. MapData.GetSurfaceHeight와 같은 결과다.
    public float SampleHeight(Vector2 mapPosition)
    {
        HandleLocateCell(mapPosition, out int gridX, out int gridZ, out float tx, out float tz);

        float h00 = GetGridHeight(gridX, gridZ);
        float h10 = GetGridHeight(gridX + 1, gridZ);
        float h01 = GetGridHeight(gridX, gridZ + 1);
        float h11 = GetGridHeight(gridX + 1, gridZ + 1);

        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
    }

    // 맵 좌표의 경사(도, 0=평지 ~ 90=수직)를 구한다. 둘러싼 격자점 4개의 중앙 차분 기울기를 이중선형 보간해, 셀 경계에서도 경사가 끊기지 않게 한다.
    // stencil은 중앙 차분 간격(격자 칸, 1 ~ MaxSlopeStencil)으로, 클수록 잔 요철을 무시한 넓은 범위의 평균 경사가 된다.
    public float SampleSlopeDegrees(Vector2 mapPosition, int stencil)
    {
        stencil = Mathf.Clamp(stencil, 1, MaxSlopeStencil);
        HandleLocateCell(mapPosition, out int gridX, out int gridZ, out float tx, out float tz);

        Vector2 g00 = HandleGetGridGradient(gridX, gridZ, stencil);
        Vector2 g10 = HandleGetGridGradient(gridX + 1, gridZ, stencil);
        Vector2 g01 = HandleGetGridGradient(gridX, gridZ + 1, stencil);
        Vector2 g11 = HandleGetGridGradient(gridX + 1, gridZ + 1, stencil);

        Vector2 gradient = Vector2.Lerp(Vector2.Lerp(g00, g10, tx), Vector2.Lerp(g01, g11, tx), tz);
        return Mathf.Atan(gradient.magnitude) * Mathf.Rad2Deg;
    }

    // 맵 좌표가 속한 격자 칸의 좌하단 전역 격자 좌표와 칸 안의 보간 비율을 구한다. 격자 범위 끝에 걸친 좌표는 마지막 칸 안으로 넣는다.
    private void HandleLocateCell(Vector2 mapPosition, out int gridX, out int gridZ, out float tx, out float tz)
    {
        Vector2 gridPosition = mapPosition / m_Spacing;
        int maxCellX = m_Origin.x + m_Heights.GetLength(1) - 1 - Padding;
        int maxCellZ = m_Origin.y + m_Heights.GetLength(0) - 1 - Padding;

        gridX = Mathf.Clamp(Mathf.FloorToInt(gridPosition.x), m_Origin.x + Padding, maxCellX);
        gridZ = Mathf.Clamp(Mathf.FloorToInt(gridPosition.y), m_Origin.y + Padding, maxCellZ);
        tx = Mathf.Clamp01(gridPosition.x - gridX);
        tz = Mathf.Clamp01(gridPosition.y - gridZ);
    }

    // 격자점의 높이 기울기(수평 1m당 높이 변화)를 stencil칸 떨어진 격자점과의 중앙 차분으로 구한다.
    private Vector2 HandleGetGridGradient(int gridX, int gridZ, int stencil)
    {
        float dx = GetGridHeight(gridX + stencil, gridZ) - GetGridHeight(gridX - stencil, gridZ);
        float dz = GetGridHeight(gridX, gridZ + stencil) - GetGridHeight(gridX, gridZ - stencil);
        return new Vector2(dx, dz) / (2f * stencil * m_CellWorldSize);
    }
}
