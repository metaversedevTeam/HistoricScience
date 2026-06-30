using UnityEngine;

namespace HistoricScience.Test
{
    // MapData가 계산한 보로노이 바이옴 정보를 이용해 터레인의 알파맵을 칠하고 기즈모를 출력하는 클래스
    public class TerrainPainter : MonoBehaviour
    {
        // 맵 바이옴 데이터를 생성하는 제공자
        [SerializeField] private MapDataGenerator m_MapDataGenerator;
        // 칠할 대상 터레인
        [SerializeField] private Terrain m_Terrain;
        // BiomeType 순서와 동일하게 매핑되는 터레인 레이어(바이옴) 목록
        [SerializeField] private TerrainLayer[] m_TerrainLayers;
        // 기즈모 구체의 기본 반지름(가중치에 곱해져 크기가 결정됨)
        [SerializeField] private float m_GizmoBaseRadius = 5f;
        // 터레인에 칠할 때 바이옴 경계를 부드럽게 섞을 블러 반경(알파맵 셀 단위). 0이면 경계가 그대로 딱딱 떨어진다.
        [SerializeField] private int m_BlendRadius = 3;

        // MapData로 보로노이 바이옴 정보를 생성하고, 그 결과로 터레인 알파맵을 칠한다.
        [ContextMenu("Paint")]
        private void PaintButton()
        {
            PaintVoronoiTerrain(false);
        }

        public void PaintVoronoiTerrain(bool useRandom = true)
        {
            if (m_MapDataGenerator == null)
            {
                Debug.LogError("VoronoiTerrainPainter: MapDataGenerator is not assigned.");
                return;
            }

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

            MapData mapData = m_MapDataGenerator.GenerateMapData(useRandom);

            float[,,] alphamap = HandleBuildAlphamap(terrainData, mapData);
            alphamap = HandleSmoothAlphamap(alphamap, m_BlendRadius);
            terrainData.SetAlphamaps(0, 0, alphamap);
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

        // 알파맵에 가로/세로 박스 블러를 차례로 적용해 바이옴 경계가 그라데이션으로 자연스럽게 섞이도록 만든다.
        // 맵 데이터(MapData) 자체의 경계는 그대로 유지되고, 터레인에 칠해지는 결과물만 부드러워진다.
        private float[,,] HandleSmoothAlphamap(float[,,] alphamap, int radius)
        {
            if (radius <= 0)
            {
                return alphamap;
            }

            int height = alphamap.GetLength(0);
            int width = alphamap.GetLength(1);
            int layerCount = alphamap.GetLength(2);

            float[,,] horizontalPass = HandleBoxBlurPass(alphamap, width, height, layerCount, radius, true);
            float[,,] verticalPass = HandleBoxBlurPass(horizontalPass, width, height, layerCount, radius, false);

            return verticalPass;
        }

        // 가로 또는 세로 한 방향으로 박스 블러 한 번을 적용한다.
        private float[,,] HandleBoxBlurPass(float[,,] source, int width, int height, int layerCount, int radius, bool horizontal)
        {
            float[,,] result = new float[height, width, layerCount];

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int l = 0; l < layerCount; l++)
                    {
                        float sum = 0f;
                        int count = 0;

                        for (int offset = -radius; offset <= radius; offset++)
                        {
                            int sampleX = horizontal ? x + offset : x;
                            int sampleZ = horizontal ? z : z + offset;

                            if (sampleX < 0 || sampleX >= width || sampleZ < 0 || sampleZ >= height)
                            {
                                continue;
                            }

                            sum += source[sampleZ, sampleX, l];
                            count++;
                        }

                        result[z, x, l] = sum / count;
                    }
                }
            }

            return result;
        }

        // 이 게임오브젝트가 선택되었을 때 마지막으로 칠한 바이옴 영역들을 기즈모로 표시한다.
        private void OnDrawGizmosSelected()
        {
            if (m_Terrain == null || m_MapDataGenerator == null || m_MapDataGenerator.LastMapData == null)
            {
                return;
            }

            foreach (MapData.BiomeRegion region in m_MapDataGenerator.LastMapData.GetRegions())
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
