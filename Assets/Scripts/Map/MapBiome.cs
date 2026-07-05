using UnityEngine;

// 맵 생성 규칙이 바이옴을 구분하는 데 사용하는 바이옴 종류
public enum MapBiomeType
{
    // 평원
    Plains,
    // 사막
    Desert,
    // 산
    Mountain,
    // 바다
    Sea,
}

// 맵에 배치될 수 있는 바이옴 한 종류의 정보를 담는 스크립터블 오브젝트
[CreateAssetMenu(fileName = "NewBiome", menuName = "HistoricScience/Map/Biome")]
public class MapBiome : ScriptableObject
{
    // 바이옴 이름 (디버그, 기즈모 라벨 표시에 사용)
    [SerializeField] private string m_BiomeName;
    // 이 바이옴의 종류 (생성 규칙이 정점에 바이옴을 배정할 때 사용)
    [SerializeField] private MapBiomeType m_BiomeType = MapBiomeType.Plains;
    // 이 바이옴을 터레인에 칠할 때 사용할 터레인 레이어
    [SerializeField] private TerrainLayer m_TerrainLayer;
    // 기즈모로 이 바이옴 영역을 표시할 때 사용할 색상
    [SerializeField] private Color m_GizmoColor = Color.red;

    public string Name => m_BiomeName;
    public MapBiomeType BiomeType => m_BiomeType;
    public TerrainLayer TerrainLayer => m_TerrainLayer;
    public Color GizmoColor => m_GizmoColor;
}
