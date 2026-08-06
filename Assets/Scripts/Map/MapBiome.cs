using UnityEngine;

// 맵에 배치될 수 있는 바이옴 한 종류의 배치 규칙과 표현 정보를 담는 스크립터블 오브젝트.
// 에셋을 만들어 MapDataGenerator의 바이옴 목록에 넣기만 하면 생성 규칙에 자동으로 참여한다.
[CreateAssetMenu(fileName = "NewBiome", menuName = "HistoricScience/Map/Biome")]
public class MapBiome : ScriptableObject
{
    // 바이옴 이름 (디버그, 기즈모 라벨 표시, 높이 노이즈 패턴 구분에 사용)
    [SerializeField] private string m_BiomeName;
    // 이 바이옴을 터레인에 칠할 때 사용할 터레인 레이어
    [SerializeField] private TerrainLayer m_TerrainLayer;
    // 이 바이옴 위에 놓이는 바닥에 사용할 머티리얼
    [SerializeField] private Material m_BottomMaterial;
    // 기즈모로 이 바이옴 영역을 표시할 때 사용할 색상
    [SerializeField] private Color m_GizmoColor = Color.red;
    // 이 바이옴이 배치되는 고도 노이즈 범위(x=최소, y=최대, 0~1). 바이옴 목록의 앞선 바이옴부터 범위를 검사해 처음 맞는 바이옴이 배치된다.
    [SerializeField] private Vector2 m_ElevationRange = new Vector2(0f, 1f);
    // 이 바이옴이 배치되는 습도 노이즈 범위(x=최소, y=최대, 0~1)
    [SerializeField] private Vector2 m_MoistureRange = new Vector2(0f, 1f);
    // 이 바이옴과 인접할 수 없는 바이옴 목록. 주변 정점에 이 중 하나가 배치되면 대체 바이옴으로 바뀐다.
    [SerializeField] private MapBiome[] m_IncompatibleBiomes;
    // 인접 금지 규칙에 걸렸을 때 대신 배치될 바이옴. 비어 있으면 인접 금지 규칙이 무시된다.
    [SerializeField] private MapBiome m_FallbackBiome;
    // 이 바이옴 지형의 기준 높이 (터레인 최대 높이 대비 0~1)
    [SerializeField, Range(0f, 1f)] private float m_BaseHeight = 0.25f;
    // 기준 높이 위에 더해질 굴곡 노이즈의 최대 높이 (터레인 최대 높이 대비 0~1)
    [SerializeField, Range(0f, 1f)] private float m_HeightNoiseAmplitude = 0.05f;
    // 굴곡 노이즈의 스케일 (정규화 맵 좌표 기준). 클수록 더 잘게, 자주 굴곡진다.
    [SerializeField, Min(0f)] private float m_HeightNoiseScale = 3f;

    public string Name => m_BiomeName;
    public TerrainLayer TerrainLayer => m_TerrainLayer;
    public Material BottomMaterial => m_BottomMaterial;
    public Color GizmoColor => m_GizmoColor;
    public MapBiome FallbackBiome => m_FallbackBiome;
    public float BaseHeight => m_BaseHeight;
    public float HeightNoiseAmplitude => m_HeightNoiseAmplitude;
    public float HeightNoiseScale => m_HeightNoiseScale;

    // 인접 금지 규칙이 유효한지(금지 목록과 대체 바이옴이 모두 지정됐는지) 여부. false면 주변 정점 검사를 건너뛸 수 있다.
    public bool HasIncompatibleRule => m_IncompatibleBiomes != null && m_IncompatibleBiomes.Length > 0 && m_FallbackBiome != null;

    // 주어진 고도/습도 노이즈 값이 이 바이옴의 배치 범위에 들어가는지 검사한다.
    public bool Matches(float elevation, float moisture)
    {
        return elevation >= m_ElevationRange.x && elevation <= m_ElevationRange.y
            && moisture >= m_MoistureRange.x && moisture <= m_MoistureRange.y;
    }

    // 주어진 바이옴이 이 바이옴과 인접할 수 없는 바이옴인지 검사한다.
    public bool IsIncompatibleWith(MapBiome other)
    {
        if (m_IncompatibleBiomes == null || other == null)
            return false;

        for (int i = 0; i < m_IncompatibleBiomes.Length; i++)
        {
            if (m_IncompatibleBiomes[i] == other)
                return true;
        }

        return false;
    }
}
