using UnityEngine;

namespace HistoricScience.Test
{
    // 터레인에 TerrainData가 없으면 인메모리로 새로 만들어 할당하고, MapData가 계산한 보로노이 바이옴 정보를 이용해 터레인의 알파맵과 높이맵을 굽는 클래스
    public class TerrainPainter : MonoBehaviour
    {
        // 절벽 판정에 쓰는 경사의 중앙 차분 간격(높이 격자 칸). 좁게 잡아 능선의 잔 바위까지 드러낸다.
        private const int k_CliffSlopeStencil = 1;
        // 눈 판정에 쓰는 경사의 중앙 차분 간격(높이 격자 칸). 넓게 잡아 잔 요철의 평평한 점마다 눈이 얼룩지는 것을 막는다.
        private const int k_SnowSlopeStencil = ChunkHeightGrid.MaxSlopeStencil;

        [Header("Terrain Settings")]
        // 칠할 대상 터레인
        [SerializeField] private Terrain m_Terrain;
        // 터레인에 칠할 때 바이옴 경계를 부드럽게 섞을 블러 반경(알파맵 셀 단위). 0이면 경계가 그대로 딱딱 떨어진다.
        [SerializeField] private int m_BlendRadius = 3;
        // 터레인에 출력할 맵 영역의 좌하단 원점 (정규화 맵 좌표)
        [SerializeField] private Vector2 m_MapViewOrigin = Vector2.zero;
        // 에디터의 Paint 컨텍스트 메뉴에서 지형 굽기를 확인할 때 사용할 고정 시드
        [SerializeField] private int m_EditorPaintSeed = 0;

        // 터레인에 출력할 맵 영역의 한 변 길이 (정규화 맵 좌표). 1이면 맵의 1x1 영역이 터레인 전체에 칠해지고, 클수록 더 넓은 영역이 축소되어 보인다.
        // 맵 좌표계는 MapTerrainSettings가 단독으로 소유하므로 인스펙터에서 따로 설정하지 않고, 굽기 직전에 주입받은 맵 데이터에서 받아 캐시한다.
        private float m_MapViewSize;

        // 청크 관리자가 지정한 청크 좌표. 지정되면 굽기 직전에 이 좌표로 출력 원점을 다시 계산하고, 없으면 인스펙터의 원점을 그대로 쓴다.
        private Vector2Int? m_ChunkCoordinate;

        // 굽기를 요청받을 때 주입받은 맵 데이터. 맵 전체를 담당하는 불변 객체라 모든 청크가 같은 인스턴스를 공유한다.
        private MapData m_MapData;

        // 마지막으로 터레인에 적용한 품질 단계. 굽기 전에는 Lod0이다.
        private ChunkLod m_CurrentLod = ChunkLod.Lod0;

        [Header("Gizmo Settings")]
        // 선택 시 바이옴 정점 기즈모를 씬 뷰에 표시할지 여부
        [SerializeField] private bool m_ShowBiomeGizmos = true;
        // 기즈모 구체의 기본 반지름(가중치에 곱해져 크기가 결정됨)
        [SerializeField] private float m_GizmoBaseRadius = 5f;
        // 터레인 기준점이 맵 데이터상 어떤 좌표에 대응되는지 기즈모로 표시할지 여부. 켜두면 오브젝트를 선택하지 않아도 항상 표시된다.
        [SerializeField] private bool m_ShowOriginGizmo = true;
        // 기준점 기즈모에서 위로 뻗는 기둥의 길이(월드 단위). 멀리서도 기준점을 찾을 수 있게 한다.
        [SerializeField] private float m_OriginGizmoPoleHeight = 30f;

        // 씬의 제너레이터로 MapData를 만들어 곧바로 터레인을 굽는다. 평소에는 굽기를 지휘하는 쪽이 맵을 주입하지만,
        // 이 에디터 버튼은 그 경로 없이 혼자 결과를 확인하는 용도라 씬에서 제너레이터를 직접 찾아 쓴다.
        [ContextMenu("Paint")]
        private void PaintButton()
        {
            MapDataGenerator generator = FindFirstObjectByType<MapDataGenerator>();
            if (generator == null)
            {
                Debug.LogError("TerrainPainter: 씬에 MapDataGenerator가 없어 지형을 구울 수 없습니다.", this);
                return;
            }

            PaintVoronoiTerrain(generator.GenerateMapData(m_EditorPaintSeed));
        }

        // 이 터레인이 출력할 청크 좌표를 지정한다. 출력 원점(= 좌표 × MapViewSize)은 맵 데이터를 받는 굽기 직전에 계산된다. 청크 관리자가 여러 청크를 이어 붙일 때 사용한다.
        public void SetChunkCoordinate(Vector2Int chunkCoordinate)
        {
            m_ChunkCoordinate = chunkCoordinate;
        }

        // 이 터레인이 마지막으로 구운 맵 데이터. 굽기 전에는 null이다.
        public MapData CurrentMapData => m_MapData;

        // 이 터레인에 마지막으로 적용한 품질 단계. Lod1이면 풀과 자원이 없고 알파맵이 블러되지 않았다(LOD0에서 낮춘 경우 블러는 남아 있다).
        // Lod2면 풀과 자원이 없고 알파맵을 칠하지 않았다(더 높은 LOD에서 낮춘 경우 그때 칠한 알파맵이 남아 있다).
        public ChunkLod CurrentLod => m_CurrentLod;

        // 월드 좌표를 이 터레인의 출력 설정 기준 정규화 맵 좌표로 변환한다. HandleMapToWorldPosition의 역함수로, 건축 위치 지정처럼 월드 좌표에서 걷기 가능 여부를 판정해야 할 때 쓴다.
        public Vector2 WorldToMapPosition(Vector3 worldPosition)
        {
            Vector3 local = worldPosition - m_Terrain.transform.position;
            Vector3 size = m_Terrain.terrainData.size;
            Vector2 normalizedPosition = new Vector2(local.x / size.x, local.z / size.z);
            return m_MapViewOrigin + normalizedPosition * m_MapViewSize;
        }

        // 월드 공간 거리를 이 터레인의 출력 설정 기준 정규화 맵 좌표 거리로 변환한다 (X축 크기 기준).
        public float WorldToMapDistance(float worldDistance)
        {
            return worldDistance * (m_MapViewSize / m_Terrain.terrainData.size.x);
        }

        // 정규화 맵 좌표를 이 터레인의 출력 설정 기준 터레인 표면 월드 좌표로 변환한다. WorldToMapPosition의 역함수로,
        // 맵 좌표 기준으로 계산한 결과(예: 가장 가까운 걸을 수 있는 위치)를 다시 씬에 표시해야 할 때 쓴다.
        public Vector3 MapToWorldPosition(Vector2 mapPosition)
        {
            return HandleMapToWorldPosition(mapPosition);
        }

        // 주입받은 맵 데이터로 준비부터 굽기까지 전체 과정을 메인 스레드에서 동기적으로 실행한다. 에디터의 Paint 버튼처럼 즉시 결과가 필요할 때 사용한다.
        public void PaintVoronoiTerrain(MapData mapData)
        {
            var context = PrepareForPaint(mapData);
            if (context == null) return;

            var result = ComputePaint(context.Value);
            ApplyPaint(result);
        }

        // 주입받은 맵 데이터를 보관하고 백그라운드 계산에 필요한 데이터를 모아 반환한다. Unity API를 사용하므로 반드시 메인 스레드에서 호출해야 한다.
        // lod가 Lod1이면 풀 디테일 프로토타입을 비워 두고, 이후 계산과 적용에서도 풀·자원·알파맵 블러를 건너뛴다. Lod2는 여기에 더해 알파맵 계산과 적용도 건너뛴다.
        // Lod0이고 자원 스포너가 있으면 자원 소환 계획도 함께 만들어, 무거운 자리 계산을 ComputePaint(백그라운드)에서 하게 한다.
        public (MapData MapData, MapBiome[] Biomes, TerrainLayer[] Layers, int CliffLayerIndex, int SnowLayerIndex, int AlphamapWidth, int AlphamapHeight, int HeightmapResolution, ChunkGrassLayout GrassLayout, int DetailResolution, ChunkLod Lod, ChunkResourcePlan ResourcePlan)? PrepareForPaint(MapData mapData, ChunkLod lod = ChunkLod.Lod0)
        {
            if (mapData == null)
            {
                Debug.LogError("TerrainPainter: 주입받은 맵 데이터가 없습니다.", this);
                return null;
            }

            if (m_Terrain == null)
            {
                Debug.LogError("TerrainPainter: Terrain is not assigned.");
                return null;
            }

            m_MapData = mapData;

            HandleApplyMapViewArea(mapData.TerrainConfig);
            HandleAssignTerrainData(mapData.TerrainConfig);

            // 알파맵 레이어 순서는 맵이 바이옴을 판정할 때 쓴 목록 순서와 반드시 같아야 하므로, 목록도 맵에서 그대로 받아 온다.
            // 경사·고도 레이어(절벽·눈)는 바이옴 레이어 뒤에 덧붙여, 바이옴 인덱스 = 레이어 인덱스 관계를 깨지 않는다.
            MapBiome[] biomes = mapData.CopyBiomes();
            TerrainLayer[] layers = HandleCollectTerrainLayers(biomes, mapData.TerrainConfig.SurfaceOverlay, out int cliffLayerIndex, out int snowLayerIndex);

            TerrainData terrainData = m_Terrain.terrainData;
            // 해상도를 여기서 바꾸면 기존 알파맵이 곧바로 지워져, 백그라운드 계산이 끝날 때까지 청크 텍스처가 사라진다.
            // 그래서 계산할 해상도만 정해 두고, 실제 터레인 해상도는 계산 결과를 적용하는 ApplyPaint에서 바꾼다.
            int alphamapResolution = mapData.TerrainConfig.GetAlphamapResolution(lod);
            terrainData.terrainLayers = layers;

            // 디테일 레이어 순서도 같은 바이옴 목록에서 정해지므로, 바이옴에 등록된 풀 종류를 여기서 모아 프로토타입으로 할당한다.
            // LOD1은 풀을 심지 않으므로 레이아웃을 만들지 않고 프로토타입을 비운다.
            ChunkGrassLayout grassLayout = null;
            ChunkResourcePlan resourcePlan = null;
            if (lod == ChunkLod.Lod0)
            {
                grassLayout = ChunkGrassLayout.Create(biomes);
                HandleApplyGrassDetailSettings(terrainData, grassLayout, mapData.TerrainConfig);
                resourcePlan = HandleCreateResourcePlan();
            }
            else
            {
                HandleClearGrassDetail(terrainData);
            }

            return (mapData, biomes, layers, cliffLayerIndex, snowLayerIndex, alphamapResolution, alphamapResolution, terrainData.heightmapResolution, grassLayout, terrainData.detailResolution, lod, resourcePlan);
        }

        // 같은 오브젝트에 ChunkResourceSpawner가 있으면 자원 소환 계획을 만든다. 스포너는 선택적 구성이라 없으면 null을 반환한다.
        private ChunkResourcePlan HandleCreateResourcePlan()
        {
            ChunkResourceSpawner spawner = GetComponent<ChunkResourceSpawner>();
            if (spawner == null)
                return null;

            return spawner.CreatePlan();
        }

        // 준비된 데이터로 알파맵과 높이맵을 계산한다. Terrain을 직접 건드리지 않는 순수 계산이라 백그라운드 스레드에서 호출해도 안전하다.
        // 굽는 동안 조회할 보로노이 정점을 먼저 한 번에 계산해 두고 모든 조회에 넘기므로, 같은 정점을 텍셀마다 다시 만들지 않는다.
        // context.Lod가 Lod1이면 알파맵 블러와 풀 밀도 계산을 건너뛰고, Lod2면 알파맵(절벽·눈 포함)을 아예 계산하지 않아 Alphamap이 null이다.
        // Lod0이면 자원 소환 자리도 여기서 계산해, 메인 스레드는 소환만 하게 한다.
        public (float[,,] Alphamap, float[,] Heightmap, int[][,] GrassLayers, ChunkResourcePlan ResourcePlan, ChunkLod Lod) ComputePaint((MapData MapData, MapBiome[] Biomes, TerrainLayer[] Layers, int CliffLayerIndex, int SnowLayerIndex, int AlphamapWidth, int AlphamapHeight, int HeightmapResolution, ChunkGrassLayout GrassLayout, int DetailResolution, ChunkLod Lod, ChunkResourcePlan ResourcePlan) context)
        {
            bool isFullDetail = context.Lod == ChunkLod.Lod0;
            bool paintsAlphamap = context.Lod != ChunkLod.Lod2;

            // 블러가 터레인 밖(이웃 청크가 칠할 영역)까지 참고할 수 있도록 반경만큼 여백을 두고 계산한 뒤, 블러가 끝나면 여백을 잘라낸다.
            // 여백이 없으면 가장자리 셀이 자기 청크 안쪽 값만 평균내어, 이웃 청크와 그라데이션이 어긋난 이음매가 생긴다. LOD1·LOD2는 블러하지 않으므로 여백도 두지 않는다.
            int blendRadius = isFullDetail ? m_BlendRadius : 0;
            int padding = Mathf.Max(blendRadius, 0);

            ChunkRegionField regionField = HandleCreateRegionField(context.MapData, context.AlphamapWidth, padding);

            float[,,] alphamap = null;
            if (paintsAlphamap)
            {
                float[,,] paddedAlphamap = HandleBuildAlphamap(context.AlphamapWidth, context.AlphamapHeight, padding, context.MapData, context.Biomes, context.Layers, regionField);
                paddedAlphamap = HandleSmoothAlphamap(paddedAlphamap, blendRadius);
                alphamap = HandleCropAlphamapPadding(paddedAlphamap, context.AlphamapWidth, context.AlphamapHeight, padding);
            }

            // 높이맵과 경사 판정이 같은 높이 격자점을 쓰도록 한 번만 계산해 둔다.
            ChunkHeightGrid heightGrid = HandleCreateHeightGrid(context.MapData, regionField);
            float[,] heightmap = HandleBuildHeightmap(context.HeightmapResolution, context.MapData, heightGrid);

            // 절벽·눈은 경계를 스스로 부드럽게 섞으므로 블러를 거치지 않고, 잘라낸 최종 알파맵에 바로 덮는다.
            if (paintsAlphamap)
                HandleApplySurfaceOverlay(alphamap, context.Biomes.Length, context.CliffLayerIndex, context.SnowLayerIndex, context.MapData.TerrainConfig.SurfaceOverlay, heightGrid);

            // 풀 밀도는 블러된 알파맵과 완성된 높이맵을 그대로 읽어, 바이옴 경계의 그라데이션과 지형 표면 높이를 따로 계산하지 않는다.
            int[][,] grassLayers = isFullDetail
                ? GrassDensityBuilder.Build(context.GrassLayout, alphamap, heightmap, context.DetailResolution, m_MapViewOrigin, m_MapViewSize, context.MapData.TerrainConfig, context.MapData.Seed)
                : null;

            // 소환 자리 판정도 같은 청크 영역을 조회하므로, 굽는 데 쓴 정점 필드를 그대로 넘겨 재계산을 피한다.
            if (isFullDetail && context.ResourcePlan != null)
                context.ResourcePlan.ComputePlacements(m_MapViewOrigin, m_MapViewSize, context.MapData, regionField);

            return (alphamap, heightmap, grassLayers, context.ResourcePlan, context.Lod);
        }

        // 이 청크를 굽는 동안 조회할 맵 영역의 정점 필드를 만든다. 알파맵은 블러 여백만큼, 높이맵은 격자 보간 때문에 한 격자 칸만큼
        // 출력 영역을 넘어 샘플링하므로 그만큼 넉넉히 잡는다. 여유가 모자라도 그 자리에서 정점을 계산하는 경로가 있어 결과는 달라지지 않는다.
        private ChunkRegionField HandleCreateRegionField(MapData mapData, int alphamapWidth, int padding)
        {
            float alphamapSlack = padding / (float)Mathf.Max(alphamapWidth - 1, 1) * m_MapViewSize;
            // 높이 격자는 경사(중앙 차분)를 구하려고 출력 영역 밖으로 여백만큼 더 읽는다.
            float heightGridSlack = mapData.HeightSampleSpacing * (ChunkHeightGrid.Padding + 1);
            float slack = alphamapSlack + heightGridSlack;

            Rect area = new Rect(
                m_MapViewOrigin.x - slack,
                m_MapViewOrigin.y - slack,
                m_MapViewSize + slack * 2f,
                m_MapViewSize + slack * 2f);

            return mapData.CreateRegionField(area);
        }

        // 계산된 알파맵과 높이맵을 실제 터레인에 적용하고, LOD0이면 풀과 자원까지 채운다. Unity API를 사용하므로 반드시 메인 스레드에서 호출해야 한다.
        // applyHeights가 false면 높이맵 적용(콜라이더 재구축 포함)을 건너뛴다. 높이는 LOD와 무관하게 같으므로 이미 구운 청크를 더 높은 LOD로 올릴 때 쓴다.
        // 알파맵이 없으면(LOD2) 터레인의 알파맵을 건드리지 않는다.
        public void ApplyPaint((float[,,] Alphamap, float[,] Heightmap, int[][,] GrassLayers, ChunkResourcePlan ResourcePlan, ChunkLod Lod) result, bool applyHeights = true)
        {
            TerrainData terrainData = m_Terrain.terrainData;
            if (result.Alphamap != null)
            {
                // 알파맵 배열은 [y, x, 레이어] 순서라 1번 차원이 너비다. 새 알파맵을 바로 덮어쓰므로 해상도 변경으로 지워지는 순간이 화면에 보이지 않는다.
                HandleApplyAlphamapResolution(terrainData, result.Alphamap.GetLength(1));
                terrainData.SetAlphamaps(0, 0, result.Alphamap);
            }
            if (applyHeights)
                terrainData.SetHeights(0, 0, result.Heightmap);
            HandleApplyGrassLayers(terrainData, result.GrassLayers);

            if (result.Lod == ChunkLod.Lod0)
                HandleSpawnResources(result.ResourcePlan);
            else
                HandleClearResources();

            m_CurrentLod = result.Lod;
        }

        // 이미 구워진 터레인을 다시 계산하지 않고 더 낮은 LOD로 낮춘다. LOD0에서 낮추면 풀과 자원을 지우고, 알파맵은 이미 칠한 그대로 둔다.
        // 지금 LOD 이하로 낮추라는 요청은 무시한다.
        public void ReduceTo(ChunkLod lod)
        {
            if (lod <= m_CurrentLod)
                return;

            if (m_CurrentLod == ChunkLod.Lod0)
            {
                if (m_Terrain != null && m_Terrain.terrainData != null)
                    HandleClearGrassDetail(m_Terrain.terrainData);

                HandleClearResources();
            }

            m_CurrentLod = lod;
        }

        // 터레인의 풀 디테일 프로토타입을 비워, 심어 둔 풀 레이어까지 함께 지운다.
        private void HandleClearGrassDetail(TerrainData terrainData)
        {
            if (terrainData.detailPrototypes.Length == 0)
                return;

            terrainData.detailPrototypes = new DetailPrototype[0];
        }

        // 같은 오브젝트에 ChunkResourceSpawner가 있으면 소환해 둔 자원을 모두 지운다.
        private void HandleClearResources()
        {
            ChunkResourceSpawner spawner = GetComponent<ChunkResourceSpawner>();
            if (spawner == null)
                return;

            spawner.ClearResources();
        }

        // 맵 설정의 디테일 해상도와 풀 레이아웃의 프로토타입을 터레인에 맞춘다. 해상도를 바꾸면 기존 디테일이 지워지므로 값이 다를 때만 바꾼다.
        private void HandleApplyGrassDetailSettings(TerrainData terrainData, ChunkGrassLayout grassLayout, MapTerrainConfig config)
        {
            if (terrainData.detailResolution != config.DetailResolution || terrainData.detailResolutionPerPatch != config.DetailResolutionPerPatch)
                terrainData.SetDetailResolution(config.DetailResolution, config.DetailResolutionPerPatch);

            // 디테일 레이어 값을 셀당 풀 개수로 해석하도록 맞춘다. GrassDensityBuilder가 이 기준으로 개수를 계산한다.
            terrainData.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            terrainData.detailPrototypes = grassLayout.CopyPrototypes();
        }

        // 계산된 풀 종류별 디테일 레이어를 터레인에 적용한다. 프로토타입 개수와 어긋난 레이어는 건너뛴다.
        private void HandleApplyGrassLayers(TerrainData terrainData, int[][,] grassLayers)
        {
            if (grassLayers == null)
                return;

            int layerCount = Mathf.Min(grassLayers.Length, terrainData.detailPrototypes.Length);
            for (int i = 0; i < layerCount; i++)
                terrainData.SetDetailLayer(0, 0, i, grassLayers[i]);
        }

        // ComputePaint에서 자리 계산까지 끝낸 소환 계획대로 같은 오브젝트의 ChunkResourceSpawner에 소환을 맡긴다. 스포너나 계획이 없으면 아무것도 하지 않는다.
        private void HandleSpawnResources(ChunkResourcePlan resourcePlan)
        {
            if (resourcePlan == null)
                return;

            ChunkResourceSpawner spawner = GetComponent<ChunkResourceSpawner>();
            if (spawner == null)
                return;

            spawner.SpawnPlan(resourcePlan);
        }

        // 맵 설정에서 출력 영역 크기를 받아 캐시하고, 청크 좌표가 지정되어 있으면 출력 원점을 그 좌표에 맞춘다.
        // ComputePaint가 백그라운드 스레드에서 이 값들을 읽으므로, 계산에 들어가기 전 메인 스레드에서 반드시 미리 맞춰 두어야 한다.
        private void HandleApplyMapViewArea(MapTerrainConfig config)
        {
            m_MapViewSize = config.MapViewSize;

            if (m_ChunkCoordinate.HasValue)
                m_MapViewOrigin = new Vector2(m_ChunkCoordinate.Value.x, m_ChunkCoordinate.Value.y) * m_MapViewSize;
        }

        // 터레인에 TerrainData가 없으면 에셋으로 저장하지 않는 인메모리 TerrainData를 맵 설정의 크기·해상도로 새로 만들어 터레인과 터레인 콜라이더에 할당한다.
        private void HandleAssignTerrainData(MapTerrainConfig config)
        {
            if (m_Terrain.terrainData != null)
            {
                return;
            }

            TerrainData terrainData = new TerrainData
            {
                heightmapResolution = config.HeightmapResolution,
                alphamapResolution = config.AlphamapResolution,
                size = config.ChunkTerrainSize,
            };

            m_Terrain.terrainData = terrainData;

            TerrainCollider terrainCollider = m_Terrain.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                terrainCollider.terrainData = terrainData;
            }
        }

        // 터레인의 알파맵 해상도를 계산된 알파맵의 해상도로 맞춘다. 해상도를 바꾸면 기존 알파맵이 지워지므로 값이 다를 때만, 새 알파맵을 적용하기 직전에 바꾼다.
        // LOD1은 낮은 해상도로 계산해 로딩을 줄이고, LOD0로 올릴 때 원래 해상도로 되돌린다. LOD0에서 낮춘 청크는 다시 계산하지 않으므로 높은 해상도를 그대로 둔다.
        private void HandleApplyAlphamapResolution(TerrainData terrainData, int resolution)
        {
            if (terrainData.alphamapResolution == resolution)
                return;

            terrainData.alphamapResolution = resolution;
        }

        // 바이옴 목록에서 터레인 레이어를 순서대로 추출하고, 지정된 절벽·눈 레이어를 그 뒤에 덧붙인다. 지정되지 않은 레이어의 인덱스는 -1이다.
        private TerrainLayer[] HandleCollectTerrainLayers(MapBiome[] biomes, MapSurfaceOverlayConfig overlay, out int cliffLayerIndex, out int snowLayerIndex)
        {
            int layerCount = biomes.Length;
            cliffLayerIndex = overlay.CliffLayer != null ? layerCount++ : -1;
            snowLayerIndex = overlay.SnowLayer != null ? layerCount++ : -1;

            TerrainLayer[] layers = new TerrainLayer[layerCount];
            for (int i = 0; i < biomes.Length; i++)
            {
                layers[i] = biomes[i].TerrainLayer;
            }

            if (cliffLayerIndex >= 0)
                layers[cliffLayerIndex] = overlay.CliffLayer;
            if (snowLayerIndex >= 0)
                layers[snowLayerIndex] = overlay.SnowLayer;

            return layers;
        }

        // 이 청크의 출력 영역을 덮는 높이 격자를 만든다. 청크 좌표로 배치된 청크는 높이맵 정점과 같은 격자점 범위를 쓴다.
        private ChunkHeightGrid HandleCreateHeightGrid(MapData mapData, ChunkRegionField regionField)
        {
            Vector2 mapMax = m_MapViewOrigin + Vector2.one * m_MapViewSize;
            Vector2Int min;
            Vector2Int max;

            if (m_ChunkCoordinate.HasValue)
            {
                // 정수 격자 좌표로 직접 계산해, 맵 좌표 나눗셈의 부동소수 오차로 범위가 한 칸 어긋나는 일을 막는다.
                int cellsPerChunk = mapData.TerrainConfig.HeightCellsPerChunk;
                min = m_ChunkCoordinate.Value * cellsPerChunk;
                max = min + Vector2Int.one * cellsPerChunk;
            }
            else
            {
                ChunkHeightGrid.GetGridBounds(m_MapViewOrigin, mapMax, mapData.HeightSampleSpacing, out min, out max);
            }

            return new ChunkHeightGrid(mapData, regionField, min, max);
        }

        // 알파맵 셀마다 경사와 높이를 보고 절벽·눈 레이어의 비율을 덮고, 남은 비율만큼 바이옴 레이어를 줄인다.
        // 바이옴 레이어 가중치가 함께 줄어들어, 알파맵을 읽는 풀 밀도도 바위·눈 위에서는 자연스럽게 줄어든다.
        private void HandleApplySurfaceOverlay(float[,,] alphamap, int biomeLayerCount, int cliffLayerIndex, int snowLayerIndex, MapSurfaceOverlayConfig overlay, ChunkHeightGrid heightGrid)
        {
            bool hasCliff = cliffLayerIndex >= 0;
            bool hasSnow = snowLayerIndex >= 0;
            if (!hasCliff && !hasSnow)
                return;

            int height = alphamap.GetLength(0);
            int width = alphamap.GetLength(1);

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 mapPosition = HandleTerrainToMapPosition(new Vector2(
                        HandleAlphamapCellToNormalized(x, width),
                        HandleAlphamapCellToNormalized(z, height)));

                    float cliffSlope = hasCliff ? heightGrid.SampleSlopeDegrees(mapPosition, k_CliffSlopeStencil) : 0f;
                    float snowSlope = hasSnow ? heightGrid.SampleSlopeDegrees(mapPosition, k_SnowSlopeStencil) : 0f;
                    float surfaceHeight = heightGrid.SampleHeight(mapPosition);
                    overlay.Evaluate(mapPosition, cliffSlope, snowSlope, surfaceHeight, hasCliff, hasSnow, out float cliff, out float snow);

                    float remaining = 1f - cliff - snow;
                    for (int l = 0; l < biomeLayerCount; l++)
                        alphamap[z, x, l] *= remaining;

                    if (hasCliff)
                        alphamap[z, x, cliffLayerIndex] = cliff;
                    if (hasSnow)
                        alphamap[z, x, snowLayerIndex] = snow;
                }
            }
        }

        // 각 알파맵 셀의 정규화 좌표에 대해 MapData가 반환하는 바이옴에 해당하는 터레인 레이어로 100% 칠한 알파맵을,
        // 상하좌우로 padding 셀만큼 여백을 덧붙인 크기로 만든다. 여백 영역은 터레인 밖의 맵 좌표를 그대로 샘플링한다.
        private float[,,] HandleBuildAlphamap(int width, int height, int padding, MapData mapData, MapBiome[] biomes, TerrainLayer[] layers, ChunkRegionField regionField)
        {
            int layerCount = layers.Length;
            int paddedWidth = width + padding * 2;
            int paddedHeight = height + padding * 2;
            float[,,] alphamap = new float[paddedHeight, paddedWidth, layerCount];

            for (int z = 0; z < paddedHeight; z++)
            {
                for (int x = 0; x < paddedWidth; x++)
                {
                    Vector2 mapPosition = HandleTerrainToMapPosition(new Vector2(
                        HandleAlphamapCellToNormalized(x - padding, width),
                        HandleAlphamapCellToNormalized(z - padding, height)));

                    MapBiome biome = mapData.GetBiome(mapPosition, regionField);
                    int layer = System.Array.IndexOf(biomes, biome);

                    for (int l = 0; l < layerCount; l++)
                    {
                        alphamap[z, x, l] = l == layer ? 1f : 0f;
                    }
                }
            }

            return alphamap;
        }

        // 알파맵 셀 좌표를 0~1 정규화 터레인 좌표로 변환한다. 터레인 셰이더가 컨트롤 텍스처를 (uv × (해상도-1) + 0.5) / 해상도 로 샘플링해
        // 양 끝 셀이 터레인 가장자리에 정확히 걸치므로, 높이맵과 똑같이 resolution-1로 나눈다. 그래야 이웃 청크와 경계 셀의 맵 좌표가 일치한다.
        private float HandleAlphamapCellToNormalized(int cell, int resolution)
        {
            return (float)cell / Mathf.Max(resolution - 1, 1);
        }

        // 여백을 두고 계산한 알파맵에서 실제 터레인에 해당하는 가운데 영역만 잘라낸다.
        private float[,,] HandleCropAlphamapPadding(float[,,] paddedAlphamap, int width, int height, int padding)
        {
            if (padding <= 0)
            {
                return paddedAlphamap;
            }

            int layerCount = paddedAlphamap.GetLength(2);
            float[,,] alphamap = new float[height, width, layerCount];

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int l = 0; l < layerCount; l++)
                    {
                        alphamap[z, x, l] = paddedAlphamap[z + padding, x + padding, l];
                    }
                }
            }

            return alphamap;
        }

        // 각 높이맵 셀의 높이(m)를 MapData에서 받아와, 터레인이 쓰는 최대 높이 대비 비율(0~1)의 높이맵을 만든다. 높이 계산 로직은 MapData가 담당한다.
        // 청크 좌표로 배치된 청크는 높이맵 정점이 높이 격자점과 1:1로 겹치므로 격자점을 그대로 읽고, 그 밖의 경우(에디터에서 원점을 직접 지정 등)는 격자를 보간한다.
        private float[,] HandleBuildHeightmap(int resolution, MapData mapData, ChunkHeightGrid heightGrid)
        {
            float[,] heightmap = new float[resolution, resolution];
            // 터레인은 맵 설정의 최대 높이로 만들어지므로(HandleAssignTerrainData), 같은 값으로 나눠 미터를 높이맵 비율로 바꾼다.
            float inverseMaxHeight = 1f / mapData.TerrainConfig.MaxTerrainHeight;

            bool isGridAligned = m_ChunkCoordinate.HasValue && resolution == mapData.TerrainConfig.HeightmapResolution;
            if (isGridAligned)
                HandleFillGridAlignedHeightmap(heightmap, resolution, mapData, heightGrid, inverseMaxHeight);
            else
                HandleFillInterpolatedHeightmap(heightmap, resolution, heightGrid, inverseMaxHeight);

            return heightmap;
        }

        // 청크 원점의 격자 좌표부터 높이맵 정점마다 대응하는 격자점 높이를 그대로 채운다. 경계 정점은 이웃 청크와 같은 격자점을 읽어 높이가 정확히 일치한다.
        private void HandleFillGridAlignedHeightmap(float[,] heightmap, int resolution, MapData mapData, ChunkHeightGrid heightGrid, float inverseMaxHeight)
        {
            int cellsPerChunk = mapData.TerrainConfig.HeightCellsPerChunk;
            int gridOriginX = m_ChunkCoordinate.Value.x * cellsPerChunk;
            int gridOriginZ = m_ChunkCoordinate.Value.y * cellsPerChunk;

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    heightmap[z, x] = heightGrid.GetGridHeight(gridOriginX + x, gridOriginZ + z) * inverseMaxHeight;
                }
            }
        }

        // 높이맵 정점의 맵 좌표에서 격자를 이중선형 보간한 표면 높이를 채운다. 정점과 격자가 어긋나는 경우(에디터에서 원점을 직접 지정 등)에만 쓴다.
        private void HandleFillInterpolatedHeightmap(float[,] heightmap, int resolution, ChunkHeightGrid heightGrid, float inverseMaxHeight)
        {
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // 높이맵은 양 끝 셀이 터레인 가장자리에 정확히 걸치므로 resolution-1로 나눈다.
                    Vector2 mapPosition = HandleTerrainToMapPosition(new Vector2((float)x / (resolution - 1), (float)z / (resolution - 1)));
                    heightmap[z, x] = heightGrid.SampleHeight(mapPosition) * inverseMaxHeight;
                }
            }
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

        // 선택 여부와 관계없이 터레인 기준점 기즈모를 표시한다.
        private void OnDrawGizmos()
        {
            if (m_Terrain == null || m_Terrain.terrainData == null)
            {
                return;
            }

            HandleDrawOriginGizmo();
        }

        // 이 게임오브젝트가 선택되었을 때 마지막으로 칠한 바이옴 영역들을 기즈모로 표시한다.
        private void OnDrawGizmosSelected()
        {
            if (m_Terrain == null || m_Terrain.terrainData == null)
            {
                return;
            }

            HandleDrawBiomeGizmos();
        }

        // 터레인의 기준점(좌하단 원점)에 구체와 기둥을 그리고, 그 지점이 대응되는 맵 좌표를 라벨로 표시한다.
        private void HandleDrawOriginGizmo()
        {
            if (!m_ShowOriginGizmo)
            {
                return;
            }

            Vector3 worldPosition = HandleMapToWorldPosition(m_MapViewOrigin);
            Vector3 poleTop = worldPosition + Vector3.up * m_OriginGizmoPoleHeight;

            Gizmos.color = Color.white;
            Gizmos.DrawSphere(worldPosition, m_GizmoBaseRadius);
            Gizmos.DrawLine(worldPosition, poleTop);

#if UNITY_EDITOR
            Vector2 mapViewMax = m_MapViewOrigin + Vector2.one * m_MapViewSize;
            Vector2 chunkCoordinate = m_MapViewOrigin / m_MapViewSize;

            UnityEditor.Handles.Label(
                poleTop + Vector3.up * 1f,
                $"origin: ({m_MapViewOrigin.x:F3}, {m_MapViewOrigin.y:F3})\n" +
                $"max: ({mapViewMax.x:F3}, {mapViewMax.y:F3})\n" +
                $"chunk: ({chunkCoordinate.x:F1}, {chunkCoordinate.y:F1}) / view size: {m_MapViewSize:F3}");
#endif
        }

        // 터레인에 출력 중인 맵 영역 안의 바이옴 정점들을 기즈모로 표시한다.
        private void HandleDrawBiomeGizmos()
        {
            if (!m_ShowBiomeGizmos)
            {
                return;
            }

            if (m_MapData == null)
            {
                return;
            }

            Rect mapViewArea = new Rect(m_MapViewOrigin.x, m_MapViewOrigin.y, m_MapViewSize, m_MapViewSize);
            foreach (BiomeRegion region in m_MapData.GetRegions(mapViewArea))
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
