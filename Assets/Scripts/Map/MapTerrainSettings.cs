using UnityEngine;

// 맵 좌표계와 청크 지형의 크기·해상도·기준 높이를 한곳에 모아 두는 설정 에셋. 같은 값을 쓰는 모든 쪽(맵 데이터, 청크 굽기, 좌표 변환, 내브메시)이
// 이 에셋 하나만 바라보게 해, 값이 여러 곳에 흩어져 서로 어긋나는 일을 막는다. 런타임 계산에는 이 에셋 대신 CreateConfig로 만든 불변 값 묶음을 쓴다.
[CreateAssetMenu(fileName = "MapTerrainSettings", menuName = "HistoricScience/Map/Terrain Settings")]
public class MapTerrainSettings : ScriptableObject
{
    // Unity 터레인이 허용하는 높이맵 해상도 범위. 실제 값은 2의 거듭제곱 + 1로 맞춰진다.
    private const int k_MinHeightmapResolution = 33;
    private const int k_MaxHeightmapResolution = 4097;
    // Unity 터레인이 허용하는 알파맵 해상도 범위
    private const int k_MinAlphamapResolution = 16;
    private const int k_MaxAlphamapResolution = 2048;

    [Header("좌표계")]
    // 청크 하나가 월드에서 차지하는 가로/세로 길이(m). 청크는 정사각형이다.
    [SerializeField, Min(1f)] private float m_ChunkWorldSize = 500f;
    // 청크 하나가 출력하는 정규화 맵 영역의 한 변 길이. 클수록 같은 청크에 더 넓은 맵 영역이 축소되어 담긴다.
    [SerializeField, Min(0.0001f)] private float m_MapViewSize = 3f;

    // 아래 필드들은 OnValidate가 서로 묶어 보정하므로, 입력 도중의 중간값(예: 28을 치려다 누른 2)에 보정이 걸리지 않도록
    // Delayed로 Enter를 누르거나 칸을 벗어날 때만 값을 적용한다.
    [Header("지형 해상도")]
    // 청크 지형의 최대 높이(m). 터레인 높이맵의 1이 이 높이에 해당한다.
    [SerializeField, Min(1f), Delayed] private float m_MaxTerrainHeight = 100f;
    // 청크 터레인의 높이맵 해상도 (2의 거듭제곱 + 1)
    [SerializeField, Delayed] private int m_HeightmapResolution = 129;
    // 청크 터레인의 알파맵(바이옴 페인팅) 해상도
    [SerializeField, Delayed] private int m_AlphamapResolution = 512;

    [Header("기준 높이 (m)")]
    // 이 높이(월드 Y) 미만의 지형은 해수면 아래로 잠겨 걸을 수 없다. 씬의 바다 평면 높이와 같아야 한다.
    [SerializeField, Min(0f), Delayed] private float m_SeaLevel = 12f;
    // 이 높이(월드 Y)를 초과하는 지형은 산/절벽 지대로 보고 걸을 수 없다. 경사 기반 판정으로 바뀌기 전까지 쓰는 임시 기준이다.
    [SerializeField, Min(0f), Delayed] private float m_WalkableMaxHeight = 28f;

    // 청크 하나가 월드에서 차지하는 가로/세로 길이(m)
    public float ChunkWorldSize => m_ChunkWorldSize;
    // 청크 하나가 출력하는 정규화 맵 영역의 한 변 길이
    public float MapViewSize => m_MapViewSize;
    // 청크 지형의 최대 높이(m)
    public float MaxTerrainHeight => m_MaxTerrainHeight;
    // 해수면 높이(m)
    public float SeaLevel => m_SeaLevel;

    // 현재 설정값을 복사한 불변 값 묶음을 만든다. 백그라운드 스레드가 읽는 값은 에셋이 아니라 이 묶음에서 가져가야 한다.
    public MapTerrainConfig CreateConfig()
    {
        return new MapTerrainConfig(m_ChunkWorldSize, m_MapViewSize, m_MaxTerrainHeight, m_HeightmapResolution, m_AlphamapResolution, m_SeaLevel, m_WalkableMaxHeight);
    }

    // 인스펙터에서 값을 바꿀 때 해상도를 터레인이 허용하는 값으로, 기준 높이를 최대 높이 안으로 보정한다.
    private void OnValidate()
    {
        m_HeightmapResolution = HandleSnapHeightmapResolution(m_HeightmapResolution);
        m_AlphamapResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(m_AlphamapResolution), k_MinAlphamapResolution, k_MaxAlphamapResolution);
        m_SeaLevel = Mathf.Min(m_SeaLevel, m_MaxTerrainHeight);
        m_WalkableMaxHeight = Mathf.Clamp(m_WalkableMaxHeight, m_SeaLevel, m_MaxTerrainHeight);
    }

    // 높이맵 해상도를 가장 가까운 (2의 거듭제곱 + 1) 값으로 맞춘다.
    private static int HandleSnapHeightmapResolution(int resolution)
    {
        int snapped = Mathf.ClosestPowerOfTwo(Mathf.Max(resolution - 1, 1)) + 1;
        return Mathf.Clamp(snapped, k_MinHeightmapResolution, k_MaxHeightmapResolution);
    }
}

// MapTerrainSettings의 값을 복사해 둔 불변 값 묶음. 맵 데이터가 들고 다니며 백그라운드 계산에서도 안전하게 읽는다.
public readonly struct MapTerrainConfig
{
    // 청크 하나가 월드에서 차지하는 가로/세로 길이(m)
    public readonly float ChunkWorldSize;
    // 청크 하나가 출력하는 정규화 맵 영역의 한 변 길이
    public readonly float MapViewSize;
    // 청크 지형의 최대 높이(m)
    public readonly float MaxTerrainHeight;
    // 청크 터레인의 높이맵 해상도
    public readonly int HeightmapResolution;
    // 청크 터레인의 알파맵 해상도
    public readonly int AlphamapResolution;
    // 해수면 높이(m)
    public readonly float SeaLevel;
    // 걸을 수 있는 최대 높이(m)
    public readonly float WalkableMaxHeight;

    public MapTerrainConfig(float chunkWorldSize, float mapViewSize, float maxTerrainHeight, int heightmapResolution, int alphamapResolution, float seaLevel, float walkableMaxHeight)
    {
        ChunkWorldSize = chunkWorldSize;
        MapViewSize = mapViewSize;
        MaxTerrainHeight = maxTerrainHeight;
        HeightmapResolution = heightmapResolution;
        AlphamapResolution = alphamapResolution;
        SeaLevel = seaLevel;
        WalkableMaxHeight = walkableMaxHeight;
    }

    // 청크 터레인의 월드 크기 (x/z = 청크 크기, y = 최대 높이)
    public Vector3 ChunkTerrainSize => new Vector3(ChunkWorldSize, MaxTerrainHeight, ChunkWorldSize);

    // 높이 샘플 격자 한 칸의 길이 (정규화 맵 좌표). 청크 하나의 맵 영역을 (높이맵 해상도 - 1)칸으로 나눠, 격자점이 청크 높이맵 정점과 1:1로 겹치게 한다.
    // 청크 경계의 정점은 이웃 청크와 같은 격자점을 가리키므로 경계 높이가 정확히 일치한다.
    public float HeightSampleSpacing => MapViewSize / (HeightmapResolution - 1);

    // 청크 하나의 한 변에 놓이는 높이 격자 칸 수 (= 높이맵 해상도 - 1). 청크 좌표에 곱하면 그 청크 원점의 격자 좌표가 된다.
    public int HeightCellsPerChunk => HeightmapResolution - 1;
}
