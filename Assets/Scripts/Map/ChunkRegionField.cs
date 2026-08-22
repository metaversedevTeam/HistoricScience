using UnityEngine;

// 한 영역을 굽는 동안 반복해서 조회되는 보로노이 정점들을, 그 영역을 덮는 셀 격자 범위째로 미리 계산해 담아 두는 조회용 클래스.
// 정점은 셀 좌표와 시드만의 함수라 굽는 내내 값이 변하지 않으므로, 알파맵 텍셀마다 다시 만들지 않고 여기서 꺼내 쓴다.
// 담아 둔 범위 밖의 셀은 모른다고 답하며, 그때는 BiomeRegionMap이 예전처럼 그 자리에서 정점을 계산하므로 결과는 달라지지 않는다.
// 생성 후 상태가 변하지 않는 불변 클래스로, 여러 스레드에서 동시에 읽어도 안전하다.
public sealed class ChunkRegionField
{
    // 담아 둔 셀 격자 범위의 좌하단 셀 좌표
    private readonly int m_MinCellX;
    private readonly int m_MinCellY;
    // 담아 둔 셀 격자 범위의 한 변당 셀 개수
    private readonly int m_CellCountX;
    private readonly int m_CellCountY;
    // 셀 격자 범위 안의 정점들을 행 우선(cellY 바깥, cellX 안쪽) 순서로 담은 배열
    private readonly BiomeRegion[] m_Regions;

    // 담아 둘 셀 격자 범위와, 그 범위의 정점을 행 우선 순서로 채운 배열을 받아 초기화한다. 배열 길이는 cellCountX × cellCountY여야 한다.
    public ChunkRegionField(int minCellX, int minCellY, int cellCountX, int cellCountY, BiomeRegion[] regions)
    {
        m_MinCellX = minCellX;
        m_MinCellY = minCellY;
        m_CellCountX = Mathf.Max(cellCountX, 0);
        m_CellCountY = Mathf.Max(cellCountY, 0);
        m_Regions = regions;

        if (regions == null || regions.Length != m_CellCountX * m_CellCountY)
            Debug.LogError("ChunkRegionField: 정점 배열의 길이가 셀 격자 범위와 맞지 않습니다.");
    }

    // 담아 둔 정점 개수. 캐시가 실제로 얼마나 커졌는지 확인할 때 쓴다.
    public int RegionCount => m_Regions != null ? m_Regions.Length : 0;

    // 주어진 셀 좌표의 정점이 담겨 있으면 꺼내 준다. 범위 밖이면 false를 반환해 호출자가 직접 계산하게 한다.
    public bool TryGetRegion(int cellX, int cellY, out BiomeRegion region)
    {
        int localX = cellX - m_MinCellX;
        int localY = cellY - m_MinCellY;

        if (m_Regions == null || localX < 0 || localX >= m_CellCountX || localY < 0 || localY >= m_CellCountY)
        {
            region = default;
            return false;
        }

        region = m_Regions[localY * m_CellCountX + localX];
        return true;
    }
}
