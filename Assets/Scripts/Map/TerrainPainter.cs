using System.Collections.Generic;
using UnityEngine;

namespace HistoricScience.Test
{
    // 터레인에 TerrainData가 없으면 인메모리로 새로 만들어 할당하고, MapData가 계산한 보로노이 바이옴 정보를 이용해 터레인의 알파맵과 높이맵을 굽고 기즈모를 출력하는 클래스
    public class TerrainPainter : MonoBehaviour
    {
        // 터레인에 TerrainData가 없을 때 새로 생성할 높이맵 해상도
        private const int k_HeightmapResolution = 129;
        // 터레인에 TerrainData가 없을 때 새로 생성할 알파맵 해상도
        private const int k_AlphamapResolution = 512;
        // 터레인에 TerrainData가 없을 때 새로 생성할 터레인 크기
        private static readonly Vector3 k_TerrainSize = new Vector3(500f, 100f, 500f);

        // 맵 바이옴 데이터를 생성하는 제공자
        [SerializeField] private MapDataGenerator m_MapDataGenerator;
        // 칠할 대상 터레인
        [SerializeField] private Terrain m_Terrain;
        // 기즈모 구체의 기본 반지름(가중치에 곱해져 크기가 결정됨)
        [SerializeField] private float m_GizmoBaseRadius = 5f;
        // 터레인에 칠할 때 바이옴 경계를 부드럽게 섞을 블러 반경(알파맵 셀 단위). 0이면 경계가 그대로 딱딱 떨어진다.
        [SerializeField] private int m_BlendRadius = 3;
        // 터레인에 출력할 맵 영역의 한 변 길이 (정규화 맵 좌표). 1이면 맵의 1x1 영역이 터레인 전체에 칠해지고, 클수록 더 넓은 영역이 축소되어 보인다.
        [SerializeField, Min(0.1f)] private float m_MapViewSize = 3f;
        // 터레인에 출력할 맵 영역의 좌하단 원점 (정규화 맵 좌표)
        [SerializeField] private Vector2 m_MapViewOrigin = Vector2.zero;

        // MapData로 보로노이 바이옴 정보를 생성하고, 그 결과로 터레인 알파맵과 높이맵을 굽는다.
        [ContextMenu("Paint")]
        private void PaintButton()
        {
            PaintVoronoiTerrain(false);
        }

        public void PaintVoronoiTerrain(bool useRandom = true)
        {
            if (m_MapDataGenerator == null)
            {
                Debug.LogError("TerrainPainter: MapDataGenerator is not assigned.");
                return;
            }

            if (m_Terrain == null)
            {
                Debug.LogError("TerrainPainter: Terrain is not assigned.");
                return;
            }

            HandleAssignTerrainData();

            MapData mapData = m_MapDataGenerator.GenerateMapData(useRandom);
            if (mapData == null) return;

            MapBiome[] biomes = m_MapDataGenerator.Biomes;
            TerrainLayer[] layers = HandleCollectTerrainLayers(biomes);

            TerrainData terrainData = m_Terrain.terrainData;
            terrainData.terrainLayers = layers;

            float[,,] alphamap = HandleBuildAlphamap(terrainData, mapData, biomes, layers);
            alphamap = HandleSmoothAlphamap(alphamap, m_BlendRadius);
            terrainData.SetAlphamaps(0, 0, alphamap);

            float[,] heightmap = HandleBuildHeightmap(terrainData, mapData);
            terrainData.SetHeights(0, 0, heightmap);
        }

        // 터레인에 TerrainData가 없으면 에셋으로 저장하지 않는 인메모리 TerrainData를 새로 만들어 터레인과 터레인 콜라이더에 할당한다.
        private void HandleAssignTerrainData()
        {
            if (m_Terrain.terrainData != null)
            {
                return;
            }

            TerrainData terrainData = new TerrainData
            {
                heightmapResolution = k_HeightmapResolution,
                alphamapResolution = k_AlphamapResolution,
                size = k_TerrainSize,
            };

            m_Terrain.terrainData = terrainData;

            TerrainCollider terrainCollider = m_Terrain.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                terrainCollider.terrainData = terrainData;
            }
        }

        // 바이옴 목록에서 터레인 레이어만 순서대로 추출한다.
        private TerrainLayer[] HandleCollectTerrainLayers(MapBiome[] biomes)
        {
            TerrainLayer[] layers = new TerrainLayer[biomes.Length];
            for (int i = 0; i < biomes.Length; i++)
            {
                layers[i] = biomes[i].TerrainLayer;
            }
            return layers;
        }

        // 각 알파맵 셀의 정규화 좌표에 대해 MapData가 반환하는 바이옴에 해당하는 터레인 레이어로 100% 칠한 알파맵을 만든다.
        private float[,,] HandleBuildAlphamap(TerrainData terrainData, MapData mapData, MapBiome[] biomes, TerrainLayer[] layers)
        {
            int width = terrainData.alphamapWidth;
            int height = terrainData.alphamapHeight;
            int layerCount = layers.Length;
            float[,,] alphamap = new float[height, width, layerCount];

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 mapPosition = HandleTerrainToMapPosition(new Vector2((float)x / width, (float)z / height));
                    MapBiome biome = mapData.GetBiome(mapPosition);
                    int layer = System.Array.IndexOf(biomes, biome);

                    for (int l = 0; l < layerCount; l++)
                    {
                        alphamap[z, x, l] = l == layer ? 1f : 0f;
                    }
                }
            }

            return alphamap;
        }

        // 각 높이맵 셀의 높이를 MapData의 높이 샘플 격자에서 받아와 높이맵을 만든다. 높이 계산 로직은 MapData가 담당한다.
        private float[,] HandleBuildHeightmap(TerrainData terrainData, MapData mapData)
        {
            int resolution = terrainData.heightmapResolution;
            float[,] heightmap = new float[resolution, resolution];
            // 이웃 셀들이 같은 격자점을 공유하므로 격자점 높이를 캐시해 중복 계산을 줄인다.
            var heightCache = new Dictionary<Vector2Int, float>();

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // 높이맵은 양 끝 셀이 터레인 가장자리에 정확히 걸치므로 resolution-1로 나눈다.
                    Vector2 mapPosition = HandleTerrainToMapPosition(new Vector2((float)x / (resolution - 1), (float)z / (resolution - 1)));
                    heightmap[z, x] = HandleSampleHeight(mapData, mapPosition, heightCache);
                }
            }

            return heightmap;
        }

        // 맵 좌표를 둘러싼 높이 격자점 4개를 MapData에서 받아와 이중선형 보간한 높이를 반환한다.
        private float HandleSampleHeight(MapData mapData, Vector2 mapPosition, Dictionary<Vector2Int, float> heightCache)
        {
            Vector2 gridPosition = mapPosition * MapData.HeightSamplesPerUnit;
            int gridX = Mathf.FloorToInt(gridPosition.x);
            int gridY = Mathf.FloorToInt(gridPosition.y);
            float tx = gridPosition.x - gridX;
            float ty = gridPosition.y - gridY;

            float h00 = HandleGetGridHeight(mapData, new Vector2Int(gridX, gridY), heightCache);
            float h10 = HandleGetGridHeight(mapData, new Vector2Int(gridX + 1, gridY), heightCache);
            float h01 = HandleGetGridHeight(mapData, new Vector2Int(gridX, gridY + 1), heightCache);
            float h11 = HandleGetGridHeight(mapData, new Vector2Int(gridX + 1, gridY + 1), heightCache);

            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
        }

        // 격자점 높이를 캐시에서 찾고, 없으면 MapData.GetHeight로 계산해 캐시에 저장한다.
        private float HandleGetGridHeight(MapData mapData, Vector2Int gridPosition, Dictionary<Vector2Int, float> heightCache)
        {
            if (!heightCache.TryGetValue(gridPosition, out float height))
            {
                height = mapData.GetHeight(gridPosition);
                heightCache[gridPosition] = height;
            }

            return height;
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

            // 터레인에 출력 중인 맵 영역 안의 정점만 기즈모로 표시한다.
            Rect mapViewArea = new Rect(m_MapViewOrigin.x, m_MapViewOrigin.y, m_MapViewSize, m_MapViewSize);
            foreach (BiomeRegion region in m_MapDataGenerator.LastMapData.GetRegions(mapViewArea))
            {
                HandleDrawRegionGizmo(region);
            }
        }

        // 바이옴 영역 하나를 가중치 크기에 비례한 구체로 그리고, 배정된 바이옴 이름을 라벨로 표시한다.
        private void HandleDrawRegionGizmo(BiomeRegion region)
        {
            Vector3 worldPosition = HandleMapToWorldPosition(region.Position);

            Gizmos.color = region.Biome.GizmoColor;
            Gizmos.DrawSphere(worldPosition, m_GizmoBaseRadius * region.Weight);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                worldPosition + Vector3.up * (m_GizmoBaseRadius * region.Weight + 1f),
                $"biome: {region.Biome.Name}");
#endif
        }

        // 0~1 정규화 터레인 좌표를 현재 출력 설정(원점/크기)에 따른 맵 좌표로 변환한다.
        private Vector2 HandleTerrainToMapPosition(Vector2 normalizedTerrainPosition)
        {
            return m_MapViewOrigin + normalizedTerrainPosition * m_MapViewSize;
        }

        // 맵 좌표를 현재 출력 중인 맵 영역 기준의 터레인 표면 월드 좌표로 변환한다.
        private Vector3 HandleMapToWorldPosition(Vector2 mapPosition)
        {
            TerrainData terrainData = m_Terrain.terrainData;
            Vector2 normalizedPosition = (mapPosition - m_MapViewOrigin) / m_MapViewSize;
            float worldX = normalizedPosition.x * terrainData.size.x;
            float worldZ = normalizedPosition.y * terrainData.size.z;
            float worldY = m_Terrain.SampleHeight(m_Terrain.transform.position + new Vector3(worldX, 0f, worldZ));

            return m_Terrain.transform.position + new Vector3(worldX, worldY, worldZ);
        }
    }
}
