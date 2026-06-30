using UnityEngine;

namespace HistoricScience.Test
{
    // MapData가 계산한 보로노이 바이옴 정보를 이용해 터레인의 알파맵을 칠하는 클래스
    public class VoronoiTerrainPainter : MonoBehaviour
    {
        // 칠할 대상 터레인
        [SerializeField] private Terrain m_Terrain;
        // BiomeType 순서와 동일하게 매핑되는 터레인 레이어(바이옴) 목록
        [SerializeField] private TerrainLayer[] m_TerrainLayers;
        // 생성할 보로노이 정점(영역)의 개수
        [SerializeField] private int m_RegionCount = 12;
        // 각 정점에 랜덤으로 부여할 가중치의 최소/최대 범위
        [SerializeField] private Vector2 m_WeightRange = new Vector2(0.5f, 2f);
        // true면 매번 랜덤 시드를 사용하고, false면 m_RandomSeed 값을 고정 시드로 사용
        [SerializeField] private bool m_UseRandomSeed = true;
        // m_UseRandomSeed가 false일 때 사용할 고정 랜덤 시드 값
        [SerializeField] private int m_RandomSeed = 0;
        // 기즈모 구체의 기본 반지름(가중치에 곱해져 크기가 결정됨)
        [SerializeField] private float m_GizmoBaseRadius = 5f;

        // 마지막으로 칠할 때 생성된 맵 바이옴 데이터 (기즈모 표시에 사용)
        private MapData m_LastMapData;

        // MapData로 보로노이 바이옴 정보를 생성하고, 그 결과로 터레인 알파맵을 칠한다.
        [ContextMenu("Paint Voronoi Terrain")]
        public void PaintVoronoiTerrain()
        {
            if (m_Terrain == null || m_Terrain.terrainData == null)
            {
                Debug.LogError("VoronoiTerrainPainter: Terrain is not assigned.");
                return;
            }

            if (m_TerrainLayers == null || m_TerrainLayers.Length == 0)
            {
                Debug.LogError("VoronoiTerrainPainter: No terrain layers assigned.");
                return;
            }

            TerrainData terrainData = m_Terrain.terrainData;
            terrainData.terrainLayers = m_TerrainLayers;

            int seed = m_UseRandomSeed ? System.Environment.TickCount : m_RandomSeed;
            MapData mapData = new MapData(seed, m_RegionCount, m_WeightRange.x, m_WeightRange.y);

            float[,,] alphamap = HandleBuildAlphamap(terrainData, mapData);
            terrainData.SetAlphamaps(0, 0, alphamap);

            m_LastMapData = mapData;
        }

        // 각 알파맵 셀의 정규화 좌표에 대해 MapData가 반환하는 바이옴에 해당하는 터레인 레이어로 100% 칠한 알파맵을 만든다.
        private float[,,] HandleBuildAlphamap(TerrainData terrainData, MapData mapData)
        {
            int width = terrainData.alphamapWidth;
            int height = terrainData.alphamapHeight;
            int layerCount = m_TerrainLayers.Length;
            float[,,] alphamap = new float[height, width, layerCount];

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 normalizedPosition = new Vector2((float)x / width, (float)z / height);
                    MapBiome biome = mapData.GetBiome(normalizedPosition);
                    int layer = (int)biome;

                    for (int l = 0; l < layerCount; l++)
                    {
                        alphamap[z, x, l] = l == layer ? 1f : 0f;
                    }
                }
            }

            return alphamap;
        }

        // 이 게임오브젝트가 선택되었을 때 마지막으로 칠한 바이옴 영역들을 기즈모로 표시한다.
        private void OnDrawGizmosSelected()
        {
            if (m_Terrain == null || m_LastMapData == null)
            {
                return;
            }

            foreach (MapData.BiomeRegion region in m_LastMapData.GetRegions())
            {
                HandleDrawRegionGizmo(region);
            }
        }

        // 바이옴 영역 하나를 가중치 크기에 비례한 구체로 그리고, 배정된 바이옴 이름을 라벨로 표시한다.
        private void HandleDrawRegionGizmo(MapData.BiomeRegion region)
        {
            Vector3 worldPosition = HandleNormalizedToWorldPosition(region.Position);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(worldPosition, m_GizmoBaseRadius * region.Weight);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                worldPosition + Vector3.up * (m_GizmoBaseRadius * region.Weight + 1f),
                $"biome: {region.Biome}");
#endif
        }

        // 0~1로 정규화된 좌표를 터레인 표면 위의 월드 좌표로 변환한다.
        private Vector3 HandleNormalizedToWorldPosition(Vector2 normalizedPosition)
        {
            TerrainData terrainData = m_Terrain.terrainData;
            float worldX = normalizedPosition.x * terrainData.size.x;
            float worldZ = normalizedPosition.y * terrainData.size.z;
            float worldY = m_Terrain.SampleHeight(m_Terrain.transform.position + new Vector3(worldX, 0f, worldZ));

            return m_Terrain.transform.position + new Vector3(worldX, worldY, worldZ);
        }
    }
}
