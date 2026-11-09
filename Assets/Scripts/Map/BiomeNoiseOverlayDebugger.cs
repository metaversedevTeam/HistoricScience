using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using HistoricScience.Test;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 소환된 청크마다 바이옴 판정용 고도/습도 노이즈를 흑백 텍스처로 구워, 지형 표면을 따라가는 반투명 메쉬로 겹쳐 보여주는 에디터 전용 디버그 툴.
// 고도, 습도, 내륙 노이즈는 별도 레이어라 따로 켜고 끄고 색을 정할 수 있고, 레이어 색에 가까울수록 노이즈 값이 1에, 검을수록 0에 가깝다.
// 바이옴 레이어는 노이즈 대신 최종 판정된 바이옴을 각 바이옴의 GizmoColor로 칠해 보여준다. 노이즈는 MapData의 에디터 전용 함수로 읽으므로 빌드에서는 아무 동작도 하지 않는다.
[ExecuteAlways]
public class BiomeNoiseOverlayDebugger : MonoBehaviour
{
    // 청크 소환/제거를 알려줄 맵 청크 관리자. 인스펙터에서 씬의 관리자를 지정한다.
    [SerializeField] private MapChunkManager m_ChunkManager;

    [Header("Overlay Settings")]
    // 고도 노이즈 레이어를 표시할지 여부
    [SerializeField] private bool m_ShowElevation = false;
    // 습도 노이즈 레이어를 표시할지 여부
    [SerializeField] private bool m_ShowMoisture = false;
    // 내륙 노이즈 레이어를 표시할지 여부
    [SerializeField] private bool m_ShowInland = false;
    // 최종 판정된 바이옴을 각 바이옴의 GizmoColor로 칠한 레이어를 표시할지 여부
    [SerializeField] private bool m_ShowBiome = false;
    // 고도 노이즈 레이어의 색. 노이즈 값이 1에 가까울수록 이 색에 가깝고, 0에 가까울수록 검게 표시된다. 투명도는 Opacity로 정한다.
    [SerializeField, ColorUsage(false)] private Color m_ElevationColor = new Color(1f, 1f, 0f);
    // 습도 노이즈 레이어의 색. 노이즈 값이 1에 가까울수록 이 색에 가깝고, 0에 가까울수록 검게 표시된다. 투명도는 Opacity로 정한다.
    [SerializeField, ColorUsage(false)] private Color m_MoistureColor = new Color(0f, 1f, 1f);
    // 내륙 노이즈 레이어의 색. 노이즈 값이 1에 가까울수록 이 색에 가깝고, 0에 가까울수록 검게 표시된다. 투명도는 Opacity로 정한다.
    [SerializeField, ColorUsage(false)] private Color m_InlandColor = new Color(1f, 0f, 1f);
    // 오버레이의 불투명도 (0이면 투명, 1이면 지형이 완전히 가려진다)
    [SerializeField, Range(0f, 1f)] private float m_Opacity = 0.6f;
    // 오버레이를 지형 표면에서 위로 띄울 높이(월드 단위). 지형과 겹쳐 깜빡이는 것을 막는다.
    [SerializeField, Min(0f)] private float m_HeightOffset = 10f;
    // 청크 하나에 굽는 노이즈 텍스처의 한 변 픽셀 수
    [SerializeField, Range(16, 512)] private int m_TextureResolution = 128;
    // 청크 하나의 오버레이 메쉬를 나눌 한 변 격자 수. 클수록 지형 굴곡을 잘 따라가지만 굽는 비용이 커진다.
    [SerializeField, Range(2, 128)] private int m_MeshResolution = 48;
    // 한 프레임에 새로 구울 최대 청크 수. 청크가 한꺼번에 소환될 때 프레임이 튀는 것을 막는다.
    [SerializeField, Min(1)] private int m_MaxBuildsPerFrame = 2;

    [Header("Hover Info")]
    // 씬 뷰에서 마우스 아래 오버레이 칸을 표시하고, 최종 지형 높이와 표시가 켜진 레이어(고도/습도/바이옴)의 값을 라벨로 보여줄지 여부
    [SerializeField] private bool m_ShowHoverInfo = false;

#if UNITY_EDITOR
    // URP 언릿 셰이더 경로
    private const string k_UnlitShaderName = "Universal Render Pipeline/Unlit";
    // 여러 레이어를 함께 켰을 때 서로 겹쳐 깜빡이지 않도록 레이어마다 더 띄울 높이 간격(월드 단위). 고도, 습도, 내륙, 바이옴 순으로 쌓인다.
    private const float k_LayerGap = 0.5f;
    // 매 프레임 불투명도를 적용할 때 문자열 조회를 피하기 위한 셰이더 프로퍼티 ID
    private static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");

    // 노이즈 한 종류를 그리는 레이어 하나의 오브젝트와 리소스를 묶어 두는 클래스
    private sealed class OverlayLayer
    {
        public GameObject Root;
        public Texture2D Texture;
        public Material Material;
    }

    // 청크 하나에 붙은 오버레이(고도/습도/내륙/바이옴 레이어와 공유 메쉬)를 묶어 두는 클래스
    private sealed class OverlayEntry
    {
        public Mesh Mesh;
        public OverlayLayer Elevation;
        public OverlayLayer Moisture;
        public OverlayLayer Inland;
        public OverlayLayer Biome;
        // 이 오버레이를 구울 때 사용한 맵 데이터와 해상도. 하나라도 달라지면 오버레이를 다시 굽는다.
        public MapData MapData;
        public int TextureResolution;
        public int MeshResolution;
    }

    // 현재 소환되어 있는 청크들의 터레인 페인터. 소환 이벤트로 추가되고 제거 이벤트로 빠진다.
    private readonly List<TerrainPainter> m_TrackedPainters = new List<TerrainPainter>();
    // 청크별로 만들어 둔 오버레이
    private readonly Dictionary<TerrainPainter, OverlayEntry> m_Overlays = new Dictionary<TerrainPainter, OverlayEntry>();

    // 마우스 아래 칸을 찾을 광선의 최대 거리(월드 단위)
    private const float k_HoverRayDistance = 100000f;
    // 마우스 아래 칸 사각형의 채우기 색
    private static readonly Color s_HoverFillColor = new Color(1f, 1f, 1f, 0.15f);
    // 마우스 아래 칸의 네 모서리. 매 프레임 배열을 새로 만들지 않도록 재사용한다.
    private readonly Vector3[] m_HoverCorners = new Vector3[4];
    // 값 라벨을 마우스 위치보다 위로 띄울 화면 거리(GUI 포인트)
    private const float k_HoverLabelOffset = 40f;
    // 마우스 위치와 라벨을 잇는 선의 두께(GUI 포인트)
    private const float k_HoverLineThickness = 2f;
    // 마우스 아래 칸 값 라벨의 스타일. EditorStyles는 GUI 이벤트 안에서만 쓸 수 있어 처음 그릴 때 만든다.
    private GUIStyle m_HoverLabelStyle;
    // 값 라벨의 내용. 매 프레임 새로 만들지 않도록 재사용한다.
    private readonly GUIContent m_HoverLabelContent = new GUIContent();

    // 마우스 아래에서 찾은 오버레이 칸 하나의 정보를 묶어 두는 구조체
    private struct HoverCell
    {
        public MapData MapData;
        // 칸 중심의 맵 좌표. 오버레이 텍스처가 픽셀 중심을 샘플링하는 것과 같은 위치다.
        public Vector2 MapPosition;
        // 마우스가 가리키는 오버레이(레이어가 모두 꺼져 있으면 지형) 표면 위의 월드 좌표
        public Vector3 MouseWorldPosition;
    }

    private void OnEnable()
    {
        HandleSubscribe();
        SceneView.duringSceneGui += HandleSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= HandleSceneGUI;
        HandleUnsubscribe();
        HandleDestroyAllOverlays();
    }

    private void Update()
    {
        HandleBuildPendingOverlays();
        HandleApplyDisplaySettings();
    }

    // 감시할 맵 청크 관리자를 지정하고 구독을 다시 연결한다. 런타임에 디버거를 소환하는 쪽에서 쓴다.
    public void SetChunkManager(MapChunkManager chunkManager)
    {
        HandleUnsubscribe();
        HandleDestroyAllOverlays();

        m_ChunkManager = chunkManager;

        if (isActiveAndEnabled)
            HandleSubscribe();
    }

    // 모든 오버레이를 지우고 현재 설정으로 다시 굽는다. 에디터 모드에서 Paint 직후 오버레이가 갱신되지 않았을 때 쓴다.
    [ContextMenu("Rebuild Overlays")]
    private void RebuildOverlaysButton()
    {
        HandleDestroyAllOverlays();
        HandleBuildPendingOverlays(int.MaxValue);
        HandleApplyDisplaySettings();
    }

    // 청크 관리자의 소환/제거 이벤트를 구독하고, 구독 전에 이미 소환되어 있던 청크들도 추적 목록에 담는다.
    private void HandleSubscribe()
    {
        if (m_ChunkManager == null)
        {
            Debug.LogError("BiomeNoiseOverlayDebugger: 맵 청크 관리자가 지정되지 않아 노이즈 오버레이를 표시할 수 없습니다.", this);
            return;
        }

        m_ChunkManager.ChunkSpawned += HandleChunkSpawned;
        m_ChunkManager.ChunkErased += HandleChunkErased;

        m_TrackedPainters.Clear();
        m_TrackedPainters.AddRange(m_ChunkManager.GetComponentsInChildren<TerrainPainter>());
    }

    // 구독을 해제하고 추적 목록을 비운다.
    private void HandleUnsubscribe()
    {
        if (m_ChunkManager == null)
            return;

        m_ChunkManager.ChunkSpawned -= HandleChunkSpawned;
        m_ChunkManager.ChunkErased -= HandleChunkErased;

        m_TrackedPainters.Clear();
    }

    // 새로 소환된 청크의 페인터를 추적 목록에 추가한다. 오버레이는 청크에 맵 데이터가 주입된 뒤 Update에서 굽는다.
    private void HandleChunkSpawned(TerrainPainter painter)
    {
        if (painter == null || m_TrackedPainters.Contains(painter))
            return;

        m_TrackedPainters.Add(painter);
    }

    // 제거되는 청크의 페인터를 추적 목록에서 빼고 그 청크의 오버레이를 지운다.
    private void HandleChunkErased(TerrainPainter painter)
    {
        m_TrackedPainters.Remove(painter);
        HandleDestroyOverlay(painter);
    }

    // 아직 오버레이가 없거나 맵 데이터·해상도가 바뀐 청크의 오버레이를 프레임당 최대 개수만큼 굽는다.
    private void HandleBuildPendingOverlays()
    {
        HandleBuildPendingOverlays(m_MaxBuildsPerFrame);
    }

    // 굽기가 필요한 청크의 오버레이를 최대 maxBuilds개까지 굽는다. 모든 레이어가 꺼져 있으면 굽지 않는다.
    private void HandleBuildPendingOverlays(int maxBuilds)
    {
        if (!m_ShowElevation && !m_ShowMoisture && !m_ShowInland && !m_ShowBiome)
            return;

        int builtCount = 0;
        for (int i = 0; i < m_TrackedPainters.Count && builtCount < maxBuilds; i++)
        {
            TerrainPainter painter = m_TrackedPainters[i];
            if (painter == null || painter.CurrentMapData == null)
                continue;

            if (m_Overlays.TryGetValue(painter, out OverlayEntry entry) && HandleIsUpToDate(entry, painter.CurrentMapData))
                continue;

            if (HandleBuildOverlay(painter))
                builtCount++;
        }
    }

    // 오버레이가 아직 살아 있고 현재 맵 데이터와 해상도 설정으로 구워진 것인지 확인한다.
    private bool HandleIsUpToDate(OverlayEntry entry, MapData mapData)
    {
        return entry.Elevation.Root != null
            && entry.Moisture.Root != null
            && entry.Inland.Root != null
            && entry.Biome.Root != null
            && entry.MapData == mapData
            && entry.TextureResolution == m_TextureResolution
            && entry.MeshResolution == m_MeshResolution;
    }

    // 청크 하나의 표면 메쉬와 고도/습도/내륙/바이옴 레이어를 굽고 청크 터레인의 자식으로 오버레이 오브젝트를 만든다. 굽지 못했으면 false를 반환한다.
    private bool HandleBuildOverlay(TerrainPainter painter)
    {
        MapData mapData = painter.CurrentMapData;
        Terrain terrain = painter.GetComponent<Terrain>();
        if (mapData == null || terrain == null || terrain.terrainData == null)
            return false;

        Vector3 terrainSize = terrain.terrainData.size;
        Rect mapArea = HandleGetChunkMapArea(painter, terrain.transform.position, terrainSize);
        if (mapArea.width <= 0f || mapArea.height <= 0f)
            return false;

        Shader shader = Shader.Find(k_UnlitShaderName);
        if (shader == null)
        {
            Debug.LogError($"BiomeNoiseOverlayDebugger: '{k_UnlitShaderName}' 셰이더를 찾을 수 없습니다.", this);
            return false;
        }

        HandleDestroyOverlay(painter);

        // 메쉬 높이와 바이옴 판정이 정점을 매번 다시 계산하지 않도록 청크 영역의 정점을 한 번만 계산해 둔다.
        ChunkRegionField field = mapData.CreateRegionField(mapArea);

        // 켜기/끄기를 다시 굽지 않고 바로 반영할 수 있도록 모든 레이어를 항상 함께 굽는다. 메쉬는 모든 레이어가 공유한다.
        Mesh mesh = HandleCreateSurfaceMesh(mapData, mapArea, terrainSize, field);
        OverlayEntry entry = new OverlayEntry
        {
            Mesh = mesh,
            Elevation = HandleCreateLayer("BiomeNoiseOverlay_Elevation", terrain.transform, mesh, shader, HandleCreateNoiseTexture(mapData, mapArea, BiomeNoiseOverlayMode.Elevation)),
            Moisture = HandleCreateLayer("BiomeNoiseOverlay_Moisture", terrain.transform, mesh, shader, HandleCreateNoiseTexture(mapData, mapArea, BiomeNoiseOverlayMode.Moisture)),
            Inland = HandleCreateLayer("BiomeNoiseOverlay_Inland", terrain.transform, mesh, shader, HandleCreateNoiseTexture(mapData, mapArea, BiomeNoiseOverlayMode.Inland)),
            Biome = HandleCreateLayer("BiomeNoiseOverlay_Biome", terrain.transform, mesh, shader, HandleCreateBiomeTexture(mapData, mapArea, field)),
            MapData = mapData,
            TextureResolution = m_TextureResolution,
            MeshResolution = m_MeshResolution,
        };

        m_Overlays[painter] = entry;
        return true;
    }

    // 구운 텍스처로 머티리얼과 오브젝트를 만들어 레이어 하나로 묶는다.
    private OverlayLayer HandleCreateLayer(string objectName, Transform terrainTransform, Mesh mesh, Shader shader, Texture2D texture)
    {
        OverlayLayer layer = new OverlayLayer
        {
            Texture = texture,
        };
        layer.Material = HandleCreateMaterial(shader, layer.Texture);
        layer.Root = HandleCreateOverlayObject(objectName, terrainTransform, mesh, layer.Material);
        return layer;
    }

    // 청크 터레인의 좌하단/우상단 월드 모서리를 맵 좌표로 되돌려, 그 청크가 담당하는 맵 영역을 구한다.
    private Rect HandleGetChunkMapArea(TerrainPainter painter, Vector3 terrainPosition, Vector3 terrainSize)
    {
        Vector2 min = painter.WorldToMapPosition(terrainPosition);
        Vector2 max = painter.WorldToMapPosition(terrainPosition + new Vector3(terrainSize.x, 0f, terrainSize.z));
        return new Rect(min, max - min);
    }

    // 맵 영역 전체를 픽셀 중심마다 샘플링해, 지정한 노이즈 값을 밝기로 담은 흑백 텍스처를 만든다.
    private Texture2D HandleCreateNoiseTexture(MapData mapData, Rect mapArea, BiomeNoiseOverlayMode mode)
    {
        int resolution = m_TextureResolution;
        Color32[] pixels = new Color32[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Vector2 position = new Vector2(
                    mapArea.xMin + (x + 0.5f) / resolution * mapArea.width,
                    mapArea.yMin + (y + 0.5f) / resolution * mapArea.height);

                byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(HandleSampleNoise(mapData, position, mode)) * 255f);
                pixels[y * resolution + x] = new Color32(value, value, value, 255);
            }
        }

        return HandleCreateTexture(pixels, resolution, FilterMode.Bilinear);
    }

    // 맵 영역 전체를 픽셀 중심마다 판정해, 최종 바이옴의 GizmoColor를 담은 텍스처를 만든다. 경계가 번지지 않도록 점 필터를 쓴다.
    private Texture2D HandleCreateBiomeTexture(MapData mapData, Rect mapArea, ChunkRegionField field)
    {
        int resolution = m_TextureResolution;
        Color32[] pixels = new Color32[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Vector2 position = new Vector2(
                    mapArea.xMin + (x + 0.5f) / resolution * mapArea.width,
                    mapArea.yMin + (y + 0.5f) / resolution * mapArea.height);

                MapBiome biome = mapData.GetBiome(position, field);
                Color color = biome != null ? biome.GizmoColor : Color.black;
                pixels[y * resolution + x] = new Color(color.r, color.g, color.b, 1f);
            }
        }

        return HandleCreateTexture(pixels, resolution, FilterMode.Point);
    }

    // 픽셀 배열로 씬에 저장되지 않는 정사각형 텍스처를 만든다.
    private Texture2D HandleCreateTexture(Color32[] pixels, int resolution, FilterMode filterMode)
    {
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
        {
            name = "BiomeNoiseOverlayTexture",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = filterMode,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    // 지정한 종류의 바이옴 판정 노이즈 값을 샘플링한다.
    private float HandleSampleNoise(MapData mapData, Vector2 position, BiomeNoiseOverlayMode mode)
    {
        switch (mode)
        {
            case BiomeNoiseOverlayMode.Elevation:
                return mapData.EditorSampleElevationNoise(position);
            case BiomeNoiseOverlayMode.Moisture:
                return mapData.EditorSampleMoistureNoise(position);
            default:
                return mapData.SampleInlandNoise(position);
        }
    }

    // 맵 데이터의 표면 높이를 따라가는 격자 메쉬를 터레인 로컬 좌표로 만든다. 터레인 높이맵이 아직 적용되기 전이어도 같은 높이가 나온다.
    private Mesh HandleCreateSurfaceMesh(MapData mapData, Rect mapArea, Vector3 terrainSize, ChunkRegionField field)
    {
        int quadCount = m_MeshResolution;
        int vertexCountPerSide = quadCount + 1;
        Vector3[] vertices = new Vector3[vertexCountPerSide * vertexCountPerSide];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[quadCount * quadCount * 6];

        for (int y = 0; y < vertexCountPerSide; y++)
        {
            for (int x = 0; x < vertexCountPerSide; x++)
            {
                Vector2 uv = new Vector2((float)x / quadCount, (float)y / quadCount);
                Vector2 mapPosition = mapArea.min + Vector2.Scale(uv, mapArea.size);
                float height = mapData.GetSurfaceHeight(mapPosition, field);

                int index = y * vertexCountPerSide + x;
                vertices[index] = new Vector3(uv.x * terrainSize.x, height, uv.y * terrainSize.z);
                uvs[index] = uv;
            }
        }

        int triangleIndex = 0;
        for (int y = 0; y < quadCount; y++)
        {
            for (int x = 0; x < quadCount; x++)
            {
                int bottomLeft = y * vertexCountPerSide + x;
                int topLeft = bottomLeft + vertexCountPerSide;

                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = bottomLeft + 1;
                triangles[triangleIndex++] = bottomLeft + 1;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topLeft + 1;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "BiomeNoiseOverlayMesh",
            hideFlags = HideFlags.DontSave,
            vertices = vertices,
            uv = uvs,
            triangles = triangles,
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    // 노이즈 텍스처를 그릴 URP 언릿 반투명 머티리얼을 만든다.
    private Material HandleCreateMaterial(Shader shader, Texture2D texture)
    {
        Material material = new Material(shader)
        {
            name = "BiomeNoiseOverlayMaterial",
            hideFlags = HideFlags.DontSave,
            renderQueue = (int)RenderQueue.Transparent,
        };

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetTexture("_BaseMap", texture);

        return material;
    }

    // 메쉬와 머티리얼을 그릴 오버레이 오브젝트를 터레인의 자식으로 만든다. 씬에 저장되지 않도록 DontSave로 둔다.
    private GameObject HandleCreateOverlayObject(string objectName, Transform terrainTransform, Mesh mesh, Material material)
    {
        GameObject overlay = new GameObject(objectName)
        {
            hideFlags = HideFlags.DontSave,
        };
        overlay.transform.SetParent(terrainTransform, false);

        overlay.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer meshRenderer = overlay.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        return overlay;
    }

    // 다시 굽지 않아도 되는 표시 설정(레이어별 켜기/끄기와 색, 불투명도, 띄울 높이)을 모든 오버레이에 적용한다.
    private void HandleApplyDisplaySettings()
    {
        Color elevationColor = HandleGetLayerColor(m_ElevationColor);
        Color moistureColor = HandleGetLayerColor(m_MoistureColor);
        Color inlandColor = HandleGetLayerColor(m_InlandColor);
        // 바이옴 레이어는 텍스처에 이미 GizmoColor가 담겨 있으므로 흰색을 곱해 색을 그대로 쓴다.
        Color biomeColor = HandleGetLayerColor(Color.white);

        foreach (OverlayEntry entry in m_Overlays.Values)
        {
            HandleApplyLayerSettings(entry.Elevation, m_ShowElevation, m_HeightOffset, elevationColor);
            HandleApplyLayerSettings(entry.Moisture, m_ShowMoisture, m_HeightOffset + k_LayerGap, moistureColor);
            HandleApplyLayerSettings(entry.Inland, m_ShowInland, m_HeightOffset + k_LayerGap * 2f, inlandColor);
            HandleApplyLayerSettings(entry.Biome, m_ShowBiome, m_HeightOffset + k_LayerGap * 3f, biomeColor);
        }
    }

    // 레이어 색에 Opacity를 투명도로 넣어, 흑백 텍스처에 곱해질 머티리얼 색을 만든다.
    private Color HandleGetLayerColor(Color layerColor)
    {
        return new Color(layerColor.r, layerColor.g, layerColor.b, m_Opacity);
    }

    // 레이어 하나에 표시 여부, 띄울 높이, 색(불투명도)을 적용한다.
    private void HandleApplyLayerSettings(OverlayLayer layer, bool isVisible, float heightOffset, Color color)
    {
        if (layer == null || layer.Root == null)
            return;

        layer.Root.SetActive(isVisible);
        layer.Root.transform.localPosition = Vector3.up * heightOffset;

        if (layer.Material != null)
            layer.Material.SetColor(s_BaseColorId, color);
    }

    // 씬 뷰가 GUI를 그릴 때마다 마우스 아래 칸을 찾아 표시가 켜진 레이어의 값을 보여준다.
    private void HandleSceneGUI(SceneView sceneView)
    {
        if (!m_ShowHoverInfo)
            return;

        Event current = Event.current;
        // 씬 뷰는 마우스 이동만으로는 다시 그리지 않으므로, 움직일 때마다 다시 그리게 해 라벨이 마우스를 따라오게 한다.
        if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)
        {
            sceneView.Repaint();
            return;
        }

        if (current.type != EventType.Repaint)
            return;

        if (!HandleTryPickHoverCell(current.mousePosition, out HoverCell cell))
            return;

        HandleDrawHoverCell(cell, current.mousePosition);
    }

    // 마우스 위치에서 쏜 광선이 닿는 오버레이 칸을 찾는다. 칸 사각형의 네 모서리는 m_HoverCorners에 채운다. 닿은 청크가 없으면 false를 반환한다.
    private bool HandleTryPickHoverCell(Vector2 guiPosition, out HoverCell cell)
    {
        cell = default;

        // 오버레이는 지형에서 위로 떠 있으므로, 광선을 그만큼 내려 터레인과 교차시키면 화면에 보이는 오버레이 위의 지점과 같은 수평 위치를 얻는다.
        float heightOffset = HandleGetTopVisibleLayerOffset();
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        Ray shiftedRay = new Ray(ray.origin - Vector3.up * heightOffset, ray.direction);

        TerrainPainter hitPainter = null;
        Vector3 hitPoint = Vector3.zero;
        float nearestDistance = float.MaxValue;

        foreach (TerrainPainter painter in m_TrackedPainters)
        {
            if (painter == null || painter.CurrentMapData == null)
                continue;

            TerrainCollider terrainCollider = painter.GetComponent<TerrainCollider>();
            if (terrainCollider == null || !terrainCollider.Raycast(shiftedRay, out RaycastHit hit, k_HoverRayDistance))
                continue;

            if (hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            hitPainter = painter;
            hitPoint = hit.point;
        }

        if (hitPainter == null)
            return false;

        Terrain terrain = hitPainter.GetComponent<Terrain>();
        if (terrain == null || terrain.terrainData == null)
            return false;

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        int resolution = m_TextureResolution;

        // 오버레이 텍스처와 같은 격자로 칸 번호를 구한다.
        Vector3 local = hitPoint - terrainPosition;
        int cellX = Mathf.Clamp(Mathf.FloorToInt(local.x / terrainSize.x * resolution), 0, resolution - 1);
        int cellY = Mathf.Clamp(Mathf.FloorToInt(local.z / terrainSize.z * resolution), 0, resolution - 1);

        Rect mapArea = HandleGetChunkMapArea(hitPainter, terrainPosition, terrainSize);
        Vector2 mapPosition = new Vector2(
            mapArea.xMin + (cellX + 0.5f) / resolution * mapArea.width,
            mapArea.yMin + (cellY + 0.5f) / resolution * mapArea.height);

        float cellWidth = terrainSize.x / resolution;
        float cellDepth = terrainSize.z / resolution;
        float cellHeight = hitPoint.y + heightOffset;
        Vector3 cellMin = new Vector3(terrainPosition.x + cellX * cellWidth, cellHeight, terrainPosition.z + cellY * cellDepth);

        m_HoverCorners[0] = cellMin;
        m_HoverCorners[1] = cellMin + new Vector3(cellWidth, 0f, 0f);
        m_HoverCorners[2] = cellMin + new Vector3(cellWidth, 0f, cellDepth);
        m_HoverCorners[3] = cellMin + new Vector3(0f, 0f, cellDepth);

        cell = new HoverCell
        {
            MapData = hitPainter.CurrentMapData,
            MapPosition = mapPosition,
            // 내린 광선이 터레인에 닿은 지점을 다시 띄우면 원래 광선이 오버레이에 닿는 지점이 된다.
            MouseWorldPosition = hitPoint + Vector3.up * heightOffset,
        };
        return true;
    }

    // 표시가 켜진 레이어 중 가장 위에 쌓인 레이어가 지형에서 떠 있는 높이를 반환한다. 켜진 레이어가 없으면 지형 표면 그대로인 0을 반환한다.
    private float HandleGetTopVisibleLayerOffset()
    {
        if (m_ShowBiome)
            return m_HeightOffset + k_LayerGap * 2f;

        if (m_ShowMoisture)
            return m_HeightOffset + k_LayerGap;

        if (m_ShowElevation)
            return m_HeightOffset;

        return 0f;
    }

    // 마우스 아래 칸의 사각형과, 마우스보다 조금 위에 띄운 값 라벨, 그리고 마우스 위치와 라벨을 잇는 선을 씬 뷰에 그린다.
    private void HandleDrawHoverCell(HoverCell cell, Vector2 mouseGuiPosition)
    {
        Handles.DrawSolidRectangleWithOutline(m_HoverCorners, s_HoverFillColor, Color.white);

        // GUI 좌표는 아래로 갈수록 y가 커지므로, y를 빼야 화면 위쪽이 된다.
        Vector2 labelAnchor = mouseGuiPosition + new Vector2(0f, -k_HoverLabelOffset);

        HandleDrawHoverLine(cell.MouseWorldPosition, mouseGuiPosition, labelAnchor);
        HandleDrawHoverLabel(HandleBuildHoverText(cell), labelAnchor);
    }

    // 마우스가 가리키는 월드 지점에서 라벨 아래쪽 가운데까지 선을 그린다. 라벨 지점은 마우스 지점과 같은 깊이로 월드 좌표를 잡아 화면에서 곧은 선이 되게 한다.
    private void HandleDrawHoverLine(Vector3 mouseWorldPosition, Vector2 mouseGuiPosition, Vector2 labelAnchor)
    {
        Ray mouseRay = HandleUtility.GUIPointToWorldRay(mouseGuiPosition);
        float depth = Vector3.Distance(mouseRay.origin, mouseWorldPosition);
        Vector3 anchorWorldPosition = HandleUtility.GUIPointToWorldRay(labelAnchor).GetPoint(depth);

        Handles.color = Color.white;
        Handles.DrawLine(mouseWorldPosition, anchorWorldPosition, k_HoverLineThickness);
    }

    // 라벨 아래쪽 가운데가 기준점에 오도록 값 라벨을 화면 좌표로 그린다.
    private void HandleDrawHoverLabel(string text, Vector2 labelAnchor)
    {
        if (m_HoverLabelStyle == null)
        {
            m_HoverLabelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
            };
        }

        m_HoverLabelContent.text = text;
        Vector2 size = m_HoverLabelStyle.CalcSize(m_HoverLabelContent);
        Rect labelRect = new Rect(labelAnchor.x - size.x * 0.5f, labelAnchor.y - size.y, size.x, size.y);

        Handles.BeginGUI();
        GUI.Label(labelRect, m_HoverLabelContent, m_HoverLabelStyle);
        Handles.EndGUI();
    }

    // 최종 지형 높이는 항상, 레이어 값은 표시가 켜진 것만 한 줄씩 담은 라벨 문자열을 만든다.
    private string HandleBuildHoverText(HoverCell cell)
    {
        List<string> lines = new List<string>(4);

        lines.Add($"높이: {cell.MapData.GetSurfaceHeight(cell.MapPosition):F2}m");

        if (m_ShowElevation)
            lines.Add($"고도: {cell.MapData.EditorSampleElevationNoise(cell.MapPosition):F3}");

        if (m_ShowMoisture)
            lines.Add($"습도: {cell.MapData.EditorSampleMoistureNoise(cell.MapPosition):F3}");

        if (m_ShowBiome)
        {
            MapBiome biome = cell.MapData.GetBiome(cell.MapPosition);
            lines.Add($"바이옴: {(biome != null ? biome.Name : "없음")}");
        }

        return string.Join("\n", lines);
    }

    // 청크 하나의 오버레이를 이루는 오브젝트와 리소스를 모두 파괴한다.
    private void HandleDestroyOverlay(TerrainPainter painter)
    {
        if (painter == null || !m_Overlays.TryGetValue(painter, out OverlayEntry entry))
            return;

        HandleDestroyEntry(entry);
        m_Overlays.Remove(painter);
    }

    // 모든 청크의 오버레이를 파괴한다.
    private void HandleDestroyAllOverlays()
    {
        foreach (OverlayEntry entry in m_Overlays.Values)
            HandleDestroyEntry(entry);

        m_Overlays.Clear();
    }

    // 오버레이 하나가 가진 모든 레이어와 공유 메쉬를 파괴한다.
    private void HandleDestroyEntry(OverlayEntry entry)
    {
        HandleDestroyLayer(entry.Elevation);
        HandleDestroyLayer(entry.Moisture);
        HandleDestroyLayer(entry.Inland);
        HandleDestroyLayer(entry.Biome);
        HandleDestroyObject(entry.Mesh);
    }

    // 레이어 하나가 가진 오브젝트, 텍스처, 머티리얼을 파괴한다.
    private void HandleDestroyLayer(OverlayLayer layer)
    {
        if (layer == null)
            return;

        HandleDestroyObject(layer.Root);
        HandleDestroyObject(layer.Texture);
        HandleDestroyObject(layer.Material);
    }

    // 플레이 모드에서는 Destroy, 에디터 모드에서는 DestroyImmediate로 오브젝트를 파괴한다.
    private void HandleDestroyObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    // 굽는 노이즈 레이어의 종류
    private enum BiomeNoiseOverlayMode
    {
        Elevation,
        Moisture,
        Inland,
    }
#endif
}
