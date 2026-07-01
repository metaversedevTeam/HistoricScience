using UnityEngine;

// 맵에 배치될 수 있는 바이옴 한 종류의 정보를 담는 스크립터블 오브젝트
[CreateAssetMenu(fileName = "NewBiome", menuName = "HistoricScience/Map/Biome")]
public class MapBiome : ScriptableObject
{
    // 바이옴 이름 (디버그, 기즈모 라벨 표시에 사용)
    [SerializeField] private string m_BiomeName;
    // 이 바이옴을 터레인에 칠할 때 사용할 터레인 레이어
    [SerializeField] private TerrainLayer m_TerrainLayer;
    // 기즈모로 이 바이옴 영역을 표시할 때 사용할 색상
    [SerializeField] private Color m_GizmoColor = Color.red;

    public string Name => m_BiomeName;
    public TerrainLayer TerrainLayer => m_TerrainLayer;
    public Color GizmoColor => m_GizmoColor;
}
