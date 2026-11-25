using UnityEngine;

// 맵 좌표계와 청크 지형의 크기·해상도·기준 높이, 내륙 노이즈 규칙을 한곳에 모아 두는 설정 에셋. 같은 값을 쓰는 모든 쪽(맵 데이터, 청크 굽기, 좌표 변환, 내브메시)이
// 이 에셋 하나만 바라보게 해, 값이 여러 곳에 흩어져 서로 어긋나는 일을 막는다. 런타임 계산에는 이 에셋 대신 CreateConfig/CreateInlandConfig로 만든 불변 값 묶음을 쓴다.
[CreateAssetMenu(fileName = "MapTerrainSettings", menuName = "HistoricScience/Map/Terrain Settings")]
public class MapTerrainSettings : ScriptableObject
{
    // Unity 터레인이 허용하는 높이맵 해상도 범위. 실제 값은 2의 거듭제곱 + 1로 맞춰진다.
    private const int k_MinHeightmapResolution = 33;
    private const int k_MaxHeightmapResolution = 4097;
    // Unity 터레인이 허용하는 알파맵 해상도 범위
    private const int k_MinAlphamapResolution = 16;
    private const int k_MaxAlphamapResolution = 2048;
    // Unity 터레인이 허용하는 디테일(풀) 해상도 상한과 패치당 해상도 범위
    private const int k_MaxDetailResolution = 4048;
    private const int k_MinDetailResolutionPerPatch = 8;
    private const int k_MaxDetailResolutionPerPatch = 128;
    // 내륙 노이즈 높이 커브를 미리 샘플링할 지점 수
    private const int k_InlandHeightCurveSampleCount = 256;

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
    // 원경용 LOD1 청크의 알파맵 해상도. 알파맵 생성과 경사·고도 페인팅 비용이 해상도의 제곱에 비례하므로 낮출수록 LOD1 로딩이 빨라진다. 알파맵 해상도 이하로 맞춰진다.
    [SerializeField, Delayed] private int m_Lod1AlphamapResolution = 256;
    // 청크 터레인의 디테일(풀) 해상도. 청크 한 변을 이 수만큼의 셀로 나눠 셀마다 풀 개수를 정한다. 0이면 풀을 출력하지 않는다.
    [SerializeField, Delayed] private int m_DetailResolution = 512;
    // 디테일 해상도를 렌더링 패치로 나누는 단위(셀). 패치 단위로 풀 메시가 묶여 컬링된다.
    [SerializeField, Delayed] private int m_DetailResolutionPerPatch = 32;

    [Header("기준 높이 (m)")]
    // 이 높이(월드 Y) 미만의 지형은 해수면 아래로 잠겨 걸을 수 없다. 씬의 바다 평면 높이와 같아야 한다.
    [SerializeField, Min(0f), Delayed] private float m_SeaLevel = 12f;
    // 이 높이(월드 Y)를 초과하는 지형은 산/절벽 지대로 보고 걸을 수 없다. 경사 기반 판정으로 바뀌기 전까지 쓰는 임시 기준이다.
    [SerializeField, Min(0f), Delayed] private float m_WalkableMaxHeight = 28f;

    [Header("내륙 노이즈")]
    // 내륙 노이즈가 이 값 이상이면 무조건 산(Mountain 분류) 바이옴이 배치된다. (0~1, 바다 기준 이상)
    [SerializeField, Delayed] private float m_InlandMountainThreshold = 0.7f;
    // 내륙 노이즈가 이 값 이하이면 무조건 바다(Sea 분류) 바이옴이 배치된다. (0~1, 산 기준 이하)
    [SerializeField, Delayed] private float m_InlandSeaThreshold = 0.3f;
    // 두 기준 사이에서 내륙 노이즈가 판정용 고도를 보정하는 세기. 고도에 (내륙 노이즈 - 0.5) × 이 값이 더해져, 클수록 산/바다가 내륙 노이즈를 더 따른다.
    [SerializeField, Min(0f)] private float m_InlandElevationBias = 0.5f;
    // 산 바이옴 높이에 더할 값의 배율(m). 산 바이옴 높이에 높이 커브(내륙 노이즈) × 이 값이 더해진다.
    [SerializeField, Min(0f)] private float m_MountainInlandHeight = 30f;
    // 내륙 노이즈(가로축, 0~1)를 산 높이 보정값(세로축)으로 바꾸는 커브. 세로축 값에 Mountain Inland Height가 곱해진다. 바이옴 판정에는 영향을 주지 않는다.
    [SerializeField] private AnimationCurve m_InlandHeightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    // 산 높이(높이 커브 × Mountain Inland Height)에 능선 노이즈 배율을 곱해 날카로운 능선과 봉우리를 만든다. (정규화 맵 좌표 1 = 청크 크기 ÷ Map View Size m)
    [Header("산 형태 (능선 노이즈)")]
    // 합산할 옥타브 수. 많을수록 큰 능선 위에 작은 바위 능선까지 겹쳐 거칠어진다.
    [SerializeField, Range(1, 8)] private int m_MountainRidgeOctaves = 5;
    // 첫 옥타브의 주파수. 클수록 능선 사이 간격이 좁아져 봉우리가 촘촘해진다.
    [SerializeField, Min(0.0001f)] private float m_MountainRidgeFrequency = 0.4f;
    // 옥타브마다 주파수에 곱해지는 값
    [SerializeField, Min(1f)] private float m_MountainRidgeLacunarity = 2f;
    // 옥타브마다 세기에 곱해지는 값. 클수록 잔 능선이 도드라진다.
    [SerializeField, Range(0f, 1f)] private float m_MountainRidgePersistence = 0.45f;
    // 산 높이 중 능선 노이즈가 깎아 내는 비율. 0이면 기존처럼 매끈한 언덕, 1이면 계곡이 산 바닥까지 깊게 파인다.
    [SerializeField, Range(0f, 1f)] private float m_MountainRidgeStrength = 0.7f;
    // 능선 노이즈에 거는 지수. 1보다 크면 봉우리가 뾰족해지고 계곡이 넓어진다.
    [SerializeField, Range(0.25f, 4f)] private float m_MountainRidgeSharpness = 1.3f;
    // 샘플 좌표를 비트는 세기. 클수록 능선이 곧게 반복되지 않고 휘어지며 갈라진다.
    [SerializeField, Min(0f)] private float m_MountainRidgeWarp = 0.5f;

    // 바이옴 페인팅 위에 경사·고도에 따라 덮는 레이어. 바이옴과 상관없이 가파른 곳은 바위, 높은 곳은 눈으로 덮여 산의 굴곡이 드러난다.
    [Header("경사·고도 페인팅")]
    // 가파른 경사에 덮을 절벽 레이어. 비워 두면 절벽 덮기를 하지 않는다.
    [SerializeField] private TerrainLayer m_CliffLayer;
    // 절벽 레이어가 나타나기 시작하는 경사(x, 도)와 완전히 덮는 경사(y, 도)
    [SerializeField] private Vector2 m_CliffSlopeRange = new Vector2(45f, 58f);
    // 높은 곳에 덮을 눈 레이어. 비워 두면 눈 덮기를 하지 않는다.
    [SerializeField] private TerrainLayer m_SnowLayer;
    // 눈이 나타나기 시작하는 높이(x, m)와 완전히 덮는 높이(y, m)
    [SerializeField] private Vector2 m_SnowHeightRange = new Vector2(270f, 330f);
    // 눈이 옅어지기 시작하는 경사(x, 도)와 완전히 사라지는 경사(y, 도). 너무 가파른 곳에는 눈이 쌓이지 않는다.
    [SerializeField] private Vector2 m_SnowSlopeRange = new Vector2(50f, 65f);
    // 눈 경계 높이를 위치마다 흔드는 폭(m). 경계가 등고선처럼 곧게 그어지지 않게 한다.
    [SerializeField, Min(0f)] private float m_SnowLineJitter = 25f;

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
        MapSurfaceOverlayConfig surfaceOverlay = new MapSurfaceOverlayConfig(m_CliffLayer, m_CliffSlopeRange, m_SnowLayer, m_SnowHeightRange, m_SnowSlopeRange, m_SnowLineJitter);
        return new MapTerrainConfig(m_ChunkWorldSize, m_MapViewSize, m_MaxTerrainHeight, m_HeightmapResolution, m_AlphamapResolution, m_Lod1AlphamapResolution, m_DetailResolution, m_DetailResolutionPerPatch, m_SeaLevel, m_WalkableMaxHeight, surfaceOverlay);
    }

    // 현재 내륙 노이즈 설정값을 복사하고 높이 커브를 미리 샘플링한 불변 값 묶음을 만든다. 커브를 읽으므로 메인 스레드에서 호출해야 한다.
    public MapInlandConfig CreateInlandConfig()
    {
        float[] heightCurveSamples = MapInlandConfig.BakeHeightCurve(m_InlandHeightCurve, k_InlandHeightCurveSampleCount);
        MountainShapeConfig mountainShape = new MountainShapeConfig(m_MountainRidgeOctaves, m_MountainRidgeFrequency, m_MountainRidgeLacunarity, m_MountainRidgePersistence, m_MountainRidgeStrength, m_MountainRidgeSharpness, m_MountainRidgeWarp);
        return new MapInlandConfig(m_InlandElevationBias, m_InlandMountainThreshold, m_InlandSeaThreshold, m_MountainInlandHeight, heightCurveSamples, mountainShape);
    }

    // 인스펙터에서 값을 바꿀 때 해상도를 터레인이 허용하는 값으로, 기준 높이를 최대 높이 안으로, 내륙 노이즈 기준을 0~1 안에서 바다 ≤ 산이 되도록 보정한다.
    private void OnValidate()
    {
        m_HeightmapResolution = HandleSnapHeightmapResolution(m_HeightmapResolution);
        m_AlphamapResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(m_AlphamapResolution), k_MinAlphamapResolution, k_MaxAlphamapResolution);
        m_Lod1AlphamapResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(m_Lod1AlphamapResolution), k_MinAlphamapResolution, m_AlphamapResolution);
        m_DetailResolution = Mathf.Clamp(m_DetailResolution, 0, k_MaxDetailResolution);
        m_DetailResolutionPerPatch = Mathf.Clamp(m_DetailResolutionPerPatch, k_MinDetailResolutionPerPatch, k_MaxDetailResolutionPerPatch);
        m_SeaLevel = Mathf.Min(m_SeaLevel, m_MaxTerrainHeight);
        m_WalkableMaxHeight = Mathf.Clamp(m_WalkableMaxHeight, m_SeaLevel, m_MaxTerrainHeight);
        m_InlandMountainThreshold = Mathf.Clamp01(m_InlandMountainThreshold);
        m_InlandSeaThreshold = Mathf.Clamp(m_InlandSeaThreshold, 0f, m_InlandMountainThreshold);
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
    // 원경용 LOD1 청크의 알파맵 해상도 (알파맵 해상도 이하)
    public readonly int Lod1AlphamapResolution;
    // 청크 터레인의 디테일(풀) 해상도. 0이면 풀을 출력하지 않는다.
    public readonly int DetailResolution;
    // 디테일 해상도를 렌더링 패치로 나누는 단위(셀)
    public readonly int DetailResolutionPerPatch;
    // 해수면 높이(m)
    public readonly float SeaLevel;
    // 걸을 수 있는 최대 높이(m)
    public readonly float WalkableMaxHeight;
    // 바이옴 페인팅 위에 경사·고도에 따라 절벽·눈 레이어를 덮는 규칙
    public readonly MapSurfaceOverlayConfig SurfaceOverlay;

    public MapTerrainConfig(float chunkWorldSize, float mapViewSize, float maxTerrainHeight, int heightmapResolution, int alphamapResolution, int lod1AlphamapResolution, int detailResolution, int detailResolutionPerPatch, float seaLevel, float walkableMaxHeight, MapSurfaceOverlayConfig surfaceOverlay)
    {
        SurfaceOverlay = surfaceOverlay;
        ChunkWorldSize = chunkWorldSize;
        MapViewSize = mapViewSize;
        MaxTerrainHeight = maxTerrainHeight;
        HeightmapResolution = heightmapResolution;
        AlphamapResolution = alphamapResolution;
        Lod1AlphamapResolution = lod1AlphamapResolution;
        DetailResolution = detailResolution;
        DetailResolutionPerPatch = detailResolutionPerPatch;
        SeaLevel = seaLevel;
        WalkableMaxHeight = walkableMaxHeight;
    }

    // 주어진 품질 단계의 청크가 쓸 알파맵 해상도를 반환한다.
    public int GetAlphamapResolution(ChunkLod lod)
    {
        return lod == ChunkLod.Lod0 ? AlphamapResolution : Lod1AlphamapResolution;
    }

    // 청크 터레인의 월드 크기 (x/z = 청크 크기, y = 최대 높이)
    public Vector3 ChunkTerrainSize => new Vector3(ChunkWorldSize, MaxTerrainHeight, ChunkWorldSize);

    // 높이 샘플 격자 한 칸의 길이 (정규화 맵 좌표). 청크 하나의 맵 영역을 (높이맵 해상도 - 1)칸으로 나눠, 격자점이 청크 높이맵 정점과 1:1로 겹치게 한다.
    // 청크 경계의 정점은 이웃 청크와 같은 격자점을 가리키므로 경계 높이가 정확히 일치한다.
    public float HeightSampleSpacing => MapViewSize / (HeightmapResolution - 1);

    // 청크 하나의 한 변에 놓이는 높이 격자 칸 수 (= 높이맵 해상도 - 1). 청크 좌표에 곱하면 그 청크 원점의 격자 좌표가 된다.
    public int HeightCellsPerChunk => HeightmapResolution - 1;
}
