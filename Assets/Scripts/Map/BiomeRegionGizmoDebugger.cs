using System.Collections.Generic;
using UnityEngine;
using HistoricScience.Test;

// 맵 청크 관리자의 청크 소환/제거 이벤트를 구독해, 현재 소환되어 있는 모든 청크의 바이옴 정점을 바이옴 색 기즈모 구체로 씬 뷰에 표시하는 디버그 툴.
// 청크를 하나씩 선택해야 보이는 TerrainPainter의 기즈모와 달리, 소환된 청크 전체의 바이옴 배치를 한눈에 확인하는 용도다.
public class BiomeRegionGizmoDebugger : MonoBehaviour
{
    // 청크 소환/제거를 알려줄 맵 청크 관리자. 인스펙터에서 씬의 관리자를 지정한다.
    [SerializeField] private MapChunkManager m_ChunkManager;

    [Header("Gizmo Settings")]
    // 바이옴 정점 기즈모를 표시할지 여부
    [SerializeField] private bool m_ShowGizmos = true;
    // 기즈모 구체의 기본 반지름(월드 단위)
    [SerializeField] private float m_GizmoBaseRadius = 5f;
    // 정점의 가중치를 반지름에 곱해 영역이 차지하는 비중까지 크기로 보여줄지 여부
    [SerializeField] private bool m_ScaleByWeight = true;
    // 각 정점 위에 배정된 바이옴 이름을 라벨로 표시할지 여부
    [SerializeField] private bool m_ShowBiomeLabels = true;
    // 정점 기즈모를 지형 표면에서 위로 띄울 높이(월드 단위). 지형에 파묻혀 보이지 않는 것을 막는다.
    [SerializeField] private float m_GizmoHeightOffset = 0f;

    // 현재 소환되어 있는 청크들의 터레인 페인터. 소환 이벤트로 추가되고 제거 이벤트로 빠진다.
    private readonly List<TerrainPainter> m_TrackedPainters = new List<TerrainPainter>();

    private void OnEnable()
    {
        HandleSubscribe();
    }

    private void OnDisable()
    {
        HandleUnsubscribe();
    }

    private void OnDrawGizmos()
    {
        if (!m_ShowGizmos)
            return;

        HandleDrawTrackedChunks();
    }

    // 청크 관리자의 소환/제거 이벤트를 구독하고, 구독 전에 이미 소환되어 있던 청크들도 추적 목록에 담는다.
    private void HandleSubscribe()
    {
        if (m_ChunkManager == null)
        {
            Debug.LogError("BiomeRegionGizmoDebugger: 맵 청크 관리자가 지정되지 않아 바이옴 기즈모를 표시할 수 없습니다.", this);
            return;
        }

        m_ChunkManager.ChunkSpawned += HandleChunkSpawned;
        m_ChunkManager.ChunkErased += HandleChunkErased;

        m_TrackedPainters.Clear();
        m_TrackedPainters.AddRange(m_ChunkManager.GetComponentsInChildren<TerrainPainter>());
    }

    // 구독을 해제하고 추적 목록을 비운다. 컴포넌트를 껐을 때 파괴된 청크를 계속 들고 있지 않도록 한다.
    private void HandleUnsubscribe()
    {
        if (m_ChunkManager == null)
            return;

        m_ChunkManager.ChunkSpawned -= HandleChunkSpawned;
        m_ChunkManager.ChunkErased -= HandleChunkErased;

        m_TrackedPainters.Clear();
    }

    // 새로 소환된 청크의 페인터를 추적 목록에 추가한다. 아직 지형이 구워지기 전 시점이라 실제 그리기는 맵 데이터가 준비된 뒤부터 이루어진다.
    private void HandleChunkSpawned(TerrainPainter painter)
    {
        if (painter == null || m_TrackedPainters.Contains(painter))
            return;

        m_TrackedPainters.Add(painter);
    }

    // 제거되는 청크의 페인터를 추적 목록에서 뺀다.
    private void HandleChunkErased(TerrainPainter painter)
    {
        m_TrackedPainters.Remove(painter);
    }

    // 추적 중인 모든 청크의 바이옴 정점 기즈모를 그린다.
    private void HandleDrawTrackedChunks()
    {
        for (int i = 0; i < m_TrackedPainters.Count; i++)
        {
            TerrainPainter painter = m_TrackedPainters[i];
            if (painter == null)
                continue;

            HandleDrawChunkRegions(painter);
        }
    }

    // 청크 하나가 출력 중인 맵 영역에 속한 정점들을 모두 그린다. 아직 굽기 전이라 맵 데이터나 터레인 데이터가 없으면 건너뛴다.
    private void HandleDrawChunkRegions(TerrainPainter painter)
    {
        MapData mapData = painter.CurrentMapData;
        if (mapData == null || !HandleTryGetChunkMapArea(painter, out Rect mapArea))
            return;

        foreach (BiomeRegion region in mapData.GetRegions(mapArea))
            HandleDrawRegionGizmo(painter, region);
    }

    // 청크 터레인의 좌하단/우상단 월드 모서리를 맵 좌표로 되돌려, 그 청크가 담당하는 맵 영역을 구한다.
    private bool HandleTryGetChunkMapArea(TerrainPainter painter, out Rect mapArea)
    {
        mapArea = default;

        Terrain terrain = painter.GetComponent<Terrain>();
        if (terrain == null || terrain.terrainData == null)
            return false;

        Vector3 terrainSize = terrain.terrainData.size;
        Vector2 min = painter.WorldToMapPosition(terrain.transform.position);
        Vector2 max = painter.WorldToMapPosition(terrain.transform.position + new Vector3(terrainSize.x, 0f, terrainSize.z));

        mapArea = new Rect(min, max - min);
        return true;
    }

    // 바이옴 정점 하나를 배정된 바이옴 색 구체로 그리고, 설정에 따라 바이옴 이름을 라벨로 함께 표시한다.
    private void HandleDrawRegionGizmo(TerrainPainter painter, BiomeRegion region)
    {
        if (region.Biome == null)
            return;

        Vector3 worldPosition = painter.MapToWorldPosition(region.Position) + Vector3.up * m_GizmoHeightOffset;
        float radius = m_ScaleByWeight ? m_GizmoBaseRadius * region.Weight : m_GizmoBaseRadius;

        Gizmos.color = region.Biome.GizmoColor;
        Gizmos.DrawSphere(worldPosition, radius);

#if UNITY_EDITOR
        if (m_ShowBiomeLabels)
            UnityEditor.Handles.Label(worldPosition + Vector3.up * (radius + 1f), region.Biome.Name);
#endif
    }
}
