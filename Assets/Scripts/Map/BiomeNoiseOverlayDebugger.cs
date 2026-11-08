using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using HistoricScience.Test;

// 소환된 청크마다 바이옴 판정용 고도/습도 노이즈를 흑백 텍스처로 구워, 지형 표면을 따라가는 반투명 메쉬로 겹쳐 보여주는 에디터 전용 디버그 툴.
// 고도와 습도는 별도 레이어라 따로 켜고 끄고 색을 정할 수 있고, 레이어 색에 가까울수록 노이즈 값이 1에, 검을수록 0에 가깝다. 노이즈는 MapData의 에디터 전용 함수로 읽으므로 빌드에서는 아무 동작도 하지 않는다.
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
    // 고도 노이즈 레이어의 색. 노이즈 값이 1에 가까울수록 이 색에 가깝고, 0에 가까울수록 검게 표시된다. 투명도는 Opacity로 정한다.
    [SerializeField, ColorUsage(false)] private Color m_ElevationColor = new Color(1f, 1f, 0f);
    // 습도 노이즈 레이어의 색. 노이즈 값이 1에 가까울수록 이 색에 가깝고, 0에 가까울수록 검게 표시된다. 투명도는 Opacity로 정한다.
    [SerializeField, ColorUsage(false)] private Color m_MoistureColor = new Color(0f, 1f, 1f);
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

#if UNITY_EDITOR
    // URP 언릿 셰이더 경로
    private const string k_UnlitShaderName = "Universal Render Pipeline/Unlit";
    // 두 레이어를 함께 켰을 때 서로 겹쳐 깜빡이지 않도록 습도 레이어를 고도 레이어보다 더 띄울 높이(월드 단위)
    private const float k_MoistureLayerGap = 0.5f;
    // 매 프레임 불투명도를 적용할 때 문자열 조회를 피하기 위한 셰이더 프로퍼티 ID
    private static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");

    // 노이즈 한 종류를 그리는 레이어 하나의 오브젝트와 리소스를 묶어 두는 클래스
    private sealed class OverlayLayer
    {
        public GameObject Root;
        public Texture2D Texture;
        public Material Material;
    }

    // 청크 하나에 붙은 오버레이(고도/습도 레이어와 공유 메쉬)를 묶어 두는 클래스
    private sealed class OverlayEntry
    {
        public Mesh Mesh;
        public OverlayLayer Elevation;
        public OverlayLayer Moisture;
        // 이 오버레이를 구울 때 사용한 맵 데이터와 해상도. 하나라도 달라지면 오버레이를 다시 굽는다.
        public MapData MapData;
        public int TextureResolution;
        public int MeshResolution;
    }

    // 현재 소환되어 있는 청크들의 터레인 페인터. 소환 이벤트로 추가되고 제거 이벤트로 빠진다.
    private readonly List<TerrainPainter> m_TrackedPainters = new List<TerrainPainter>();
    // 청크별로 만들어 둔 오버레이
    private readonly Dictionary<TerrainPainter, OverlayEntry> m_Overlays = new Dictionary<TerrainPainter, OverlayEntry>();

    private void OnEnable()
    {
        HandleSubscribe();
    }

    private void OnDisable()
    {
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

    // 굽기가 필요한 청크의 오버레이를 최대 maxBuilds개까지 굽는다. 두 레이어가 모두 꺼져 있으면 굽지 않는다.
    private void HandleBuildPendingOverlays(int maxBuilds)
    {
        if (!m_ShowElevation && !m_ShowMoisture)
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
            && entry.MapData == mapData
            && entry.TextureResolution == m_TextureResolution
            && entry.MeshResolution == m_MeshResolution;
    }

    // 청크 하나의 표면 메쉬와 고도/습도 레이어를 굽고 청크 터레인의 자식으로 오버레이 오브젝트를 만든다. 굽지 못했으면 false를 반환한다.
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

        // 켜기/끄기를 다시 굽지 않고 바로 반영할 수 있도록 두 레이어를 항상 함께 굽는다. 메쉬는 두 레이어가 공유한다.
        Mesh mesh = HandleCreateSurfaceMesh(mapData, mapArea, terrainSize);
        OverlayEntry entry = new OverlayEntry
        {
            Mesh = mesh,
            Elevation = HandleCreateLayer("BiomeNoiseOverlay_Elevation", terrain.transform, mesh, shader, mapData, mapArea, BiomeNoiseOverlayMode.Elevation),
            Moisture = HandleCreateLayer("BiomeNoiseOverlay_Moisture", terrain.transform, mesh, shader, mapData, mapArea, BiomeNoiseOverlayMode.Moisture),
            MapData = mapData,
            TextureResolution = m_TextureResolution,
            MeshResolution = m_MeshResolution,
        };

        m_Overlays[painter] = entry;
        return true;
    }

    // 노이즈 한 종류의 텍스처와 머티리얼, 오브젝트를 만들어 레이어 하나로 묶는다.
    private OverlayLayer HandleCreateLayer(string objectName, Transform terrainTransform, Mesh mesh, Shader shader, MapData mapData, Rect mapArea, BiomeNoiseOverlayMode mode)
    {
        OverlayLayer layer = new OverlayLayer
        {
            Texture = HandleCreateNoiseTexture(mapData, mapArea, mode),
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

        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
        {
            name = "BiomeNoiseOverlayTexture",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    // 지정한 종류의 바이옴 판정 노이즈 값을 샘플링한다.
    private float HandleSampleNoise(MapData mapData, Vector2 position, BiomeNoiseOverlayMode mode)
    {
        return mode == BiomeNoiseOverlayMode.Elevation
            ? mapData.EditorSampleElevationNoise(position)
            : mapData.EditorSampleMoistureNoise(position);
    }

    // 맵 데이터의 표면 높이를 따라가는 격자 메쉬를 터레인 로컬 좌표로 만든다. 터레인 높이맵이 아직 적용되기 전이어도 같은 높이가 나온다.
    private Mesh HandleCreateSurfaceMesh(MapData mapData, Rect mapArea, Vector3 terrainSize)
    {
        int quadCount = m_MeshResolution;
        int vertexCountPerSide = quadCount + 1;
        Vector3[] vertices = new Vector3[vertexCountPerSide * vertexCountPerSide];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[quadCount * quadCount * 6];

        // 메쉬 정점마다 정점 영역을 다시 계산하지 않도록 청크 영역의 정점을 한 번만 계산해 둔다.
        ChunkRegionField field = mapData.CreateRegionField(mapArea);

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

        foreach (OverlayEntry entry in m_Overlays.Values)
        {
            HandleApplyLayerSettings(entry.Elevation, m_ShowElevation, m_HeightOffset, elevationColor);
            HandleApplyLayerSettings(entry.Moisture, m_ShowMoisture, m_HeightOffset + k_MoistureLayerGap, moistureColor);
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

    // 오버레이 하나가 가진 두 레이어와 공유 메쉬를 파괴한다.
    private void HandleDestroyEntry(OverlayEntry entry)
    {
        HandleDestroyLayer(entry.Elevation);
        HandleDestroyLayer(entry.Moisture);
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
    }
#endif
}
